using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using paperframe_server.Helpers;

namespace paperframe_server.Services;

public class PaperframeLogService : IPaperframeLogService
{
    private readonly List<PaperframeLogEntry> _checkIns = new();
    private readonly List<PaperframeLogEntry> _deviceLines = new();
    private readonly Dictionary<string, DeviceStatus> _deviceStatuses = new();
    private readonly IOptionsMonitor<AppSettings> _options;
    private readonly PaperframeLogFile _file;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    /// <summary>
    /// Check-in verdicts kept in memory for the manager's log view.
    ///
    /// Held apart from the lines devices deliver, because the two arrive at wildly different
    /// rates: one delivery can carry dozens of device lines, and sharing a single ring meant
    /// two chatty check-ins could flush every verdict out of view. The durable copy on disk
    /// holds the full history of both.
    /// </summary>
    public const int MaxLogs = 100;

    /// <summary>Lines delivered from devices' own logs, retained independently of check-ins.</summary>
    public const int MaxDeviceLines = 400;

    /// <summary>Config id recorded for entries that have no configuration behind them.</summary>
    private const string NoConfig = "None";

    /// <summary>Service name entries carried in from a device's own log are filed under.</summary>
    public const string DeviceService = "Client";

    /// <summary>Status entries carried in from a device's own log are filed under.</summary>
    public const string DeviceStatusName = "Device";

    public PaperframeLogService(
        IOptionsMonitor<AppSettings> options,
        PaperframeLogFile file,
        TimeProvider? timeProvider = null)
    {
        _options = options;
        _file = file;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void LogCheckIn(CheckInRequest request)
    {
        lock (_lock)
        {
            var now = _timeProvider.GetLocalNow().DateTime;
            Append(_checkIns, MaxLogs, new PaperframeLogEntry
            {
                Timestamp = now,
                DeviceId = request.DeviceId,
                Battery = request.Battery,
                ScreenResolution = request.ScreenResolution,
                Service = request.Service,
                ConfigId = request.ConfigId,
                Status = request.Status,
                Message = request.Message,
                ScriptVersion = request.ScriptVersion
            });

            RecordStatus(request, now);
        }
    }

    private readonly Dictionary<string, string> _lastDiagnostics = new();

    public void LogDeviceDiagnostics(DeviceRequest device)
    {
        lock (_lock)
        {
            // wget repeats its headers when it follows the server's redirect, so the same
            // lines arrive again on the second hop while the device clears its outbox only
            // once. Recording a delivery identical to the last one from this device would
            // double every line, so the repeat is dropped rather than stored.
            var signature = string.Join('\n', device.ClientLog);
            if (_lastDiagnostics.TryGetValue(device.DeviceId, out var previous) && previous == signature)
            {
                return;
            }

            _lastDiagnostics[device.DeviceId] = signature;

            foreach (var line in device.ClientLog)
            {
                Append(_deviceLines, MaxDeviceLines, new PaperframeLogEntry
                {
                    // The device's own clock is not trusted enough to order the log by, so
                    // the line keeps its device timestamp in the text and is filed under
                    // the moment it actually arrived.
                    Timestamp = _timeProvider.GetLocalNow().DateTime,
                    DeviceId = device.DeviceId,
                    Battery = device.Battery,
                    ScreenResolution = device.ScreenResolution,
                    Service = DeviceService,
                    ConfigId = NoConfig,
                    Status = DeviceStatusName,
                    Message = line,
                    ScriptVersion = device.ScriptVersion
                });
            }
        }
    }

    /// <summary>
    /// Adds one entry to its own stream and enforces that stream's ceiling. Every entry
    /// reaches the durable copy first, which is the one that is not allowed to forget.
    /// Callers hold the lock.
    /// </summary>
    private void Append(List<PaperframeLogEntry> stream, int max, PaperframeLogEntry entry)
    {
        _file.Append(entry);

        stream.Add(entry);

        if (stream.Count > max)
        {
            stream.RemoveAt(0);
        }
    }

    /// <summary>
    /// Mirrors the status map onto the configured device list, which is the single rule
    /// governing it: ids the server does not know are logged but never tracked (otherwise
    /// any caller could grow this map without bound), and ids that have left the
    /// configuration are dropped. Enforcing it on the one path that mutates the map
    /// leaves <see cref="GetDeviceStatuses"/> a straight projection.
    /// </summary>
    private void RecordStatus(CheckInRequest request, DateTime now)
    {
        var configured = _options.CurrentValue.Devices;

        // O(devices) on every check-in; fine at today's device counts, but if that list
        // ever grows large, move this prune to a config-reload hook instead of the hot path.
        foreach (var staleId in _deviceStatuses.Keys.Where(id => configured?.ContainsKey(id) != true).ToList())
        {
            _deviceStatuses.Remove(staleId);
            _lastDiagnostics.Remove(staleId);
        }

        if (configured?.ContainsKey(request.DeviceId) != true)
        {
            return;
        }

        if (!_deviceStatuses.TryGetValue(request.DeviceId, out var devStatus))
        {
            devStatus = new DeviceStatus { DeviceId = request.DeviceId };
            _deviceStatuses[request.DeviceId] = devStatus;
        }

        devStatus.LastUpdate = now;
        devStatus.Status = request.Status;
        devStatus.ScriptVersion = request.ScriptVersion;
        if (request.Battery.HasValue)
        {
            devStatus.Battery = request.Battery;
        }
    }

    public List<PaperframeLogEntry> GetLogs()
    {
        lock (_lock)
        {
            // Merged on the way out: callers want one chronological view, not two streams.
            return _checkIns.Concat(_deviceLines).OrderByDescending(l => l.Timestamp).ToList();
        }
    }

    public Dictionary<string, DeviceStatus> GetDeviceStatuses()
    {
        lock (_lock)
        {
            return _deviceStatuses.ToDictionary(k => k.Key, v => new DeviceStatus
            {
                DeviceId = v.Value.DeviceId,
                Battery = v.Value.Battery,
                LastUpdate = v.Value.LastUpdate,
                Status = v.Value.Status,
                ScriptVersion = v.Value.ScriptVersion
            });
        }
    }
}

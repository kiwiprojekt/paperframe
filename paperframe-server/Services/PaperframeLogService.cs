using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;

namespace paperframe_server.Services;

public class PaperframeLogService : IPaperframeLogService
{
    private readonly List<PaperframeLogEntry> _logs = new();
    private readonly Dictionary<string, DeviceStatus> _deviceStatuses = new();
    private readonly IOptionsMonitor<AppSettings> _options;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private const int MaxLogs = 100;

    public PaperframeLogService(IOptionsMonitor<AppSettings> options, TimeProvider? timeProvider = null)
    {
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void LogCheckIn(CheckInRequest request)
    {
        lock (_lock)
        {
            var now = _timeProvider.GetLocalNow().DateTime;
            _logs.Add(new PaperframeLogEntry
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

            if (_logs.Count > MaxLogs)
            {
                _logs.RemoveAt(0);
            }

            RecordStatus(request, now);
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

        foreach (var staleId in _deviceStatuses.Keys.Where(id => configured?.ContainsKey(id) != true).ToList())
        {
            _deviceStatuses.Remove(staleId);
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
            return _logs.OrderByDescending(l => l.Timestamp).ToList();
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

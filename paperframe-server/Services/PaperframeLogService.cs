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

            // Device status backs the dashboard, which only ever shows configured devices.
            // Check-ins from unknown ids stay in the (capped) log but must not accumulate
            // here, or any caller could grow this dictionary without bound.
            if (_options.CurrentValue.Devices?.ContainsKey(request.DeviceId) != true)
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

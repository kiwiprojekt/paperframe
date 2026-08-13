using System;
using System.Collections.Generic;
using System.Linq;

namespace paperframe_server.Services;

public class PaperframeLogService : IPaperframeLogService
{
    private readonly List<PaperframeLogEntry> _logs = new();
    private readonly Dictionary<string, DeviceStatus> _deviceStatuses = new();
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private const int MaxLogs = 100;

    public PaperframeLogService(TimeProvider? timeProvider = null)
    {
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

            // Track latest status per device
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

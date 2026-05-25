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

    public void LogCheckIn(string deviceId, int? battery, string screenResolution, string service, string configId, string status, string message)
    {
        lock (_lock)
        {
            var now = _timeProvider.GetLocalNow().DateTime;
            _logs.Add(new PaperframeLogEntry
            {
                Timestamp = now,
                DeviceId = deviceId,
                Battery = battery,
                ScreenResolution = screenResolution,
                Service = service,
                ConfigId = configId,
                Status = status,
                Message = message
            });

            if (_logs.Count > MaxLogs)
            {
                _logs.RemoveAt(0);
            }

            // Track latest status per device
            if (!_deviceStatuses.TryGetValue(deviceId, out var devStatus))
            {
                devStatus = new DeviceStatus { DeviceId = deviceId };
                _deviceStatuses[deviceId] = devStatus;
            }
            devStatus.LastUpdate = now;
            devStatus.Status = status;
            if (battery.HasValue)
            {
                devStatus.Battery = battery;
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
                Status = v.Value.Status
            });
        }
    }
}

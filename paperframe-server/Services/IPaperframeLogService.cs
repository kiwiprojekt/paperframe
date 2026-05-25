using System;
using System.Collections.Generic;

namespace paperframe_server.Services;

public class PaperframeLogEntry
{
    public DateTime Timestamp { get; set; }
    public string DeviceId { get; set; }
    public int? Battery { get; set; }
    public string ScreenResolution { get; set; }
    public string Service { get; set; }
    public string ConfigId { get; set; }
    public string Status { get; set; } // "Redirect", "Success", "Error"
    public string Message { get; set; }
}

public class DeviceStatus
{
    public string DeviceId { get; set; }
    public int? Battery { get; set; }
    public DateTime LastUpdate { get; set; }
    public string Status { get; set; }
}

public interface IPaperframeLogService
{
    void LogCheckIn(string deviceId, int? battery, string screenResolution, string service, string configId, string status, string message);
    List<PaperframeLogEntry> GetLogs();
    Dictionary<string, DeviceStatus> GetDeviceStatuses();
}

using System;
using System.Collections.Generic;

namespace paperframe_server.Services;

public class PaperframeLogEntry
{
    public DateTime Timestamp { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int? Battery { get; set; }
    public string ScreenResolution { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string ConfigId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Redirect", "Success", "Error"
    public string Message { get; set; } = string.Empty;
}

public class DeviceStatus
{
    public string DeviceId { get; set; } = string.Empty;
    public int? Battery { get; set; }
    public DateTime LastUpdate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public interface IPaperframeLogService
{
    void LogCheckIn(string deviceId, int? battery, string screenResolution, string service, string configId, string status, string message);
    List<PaperframeLogEntry> GetLogs();
    Dictionary<string, DeviceStatus> GetDeviceStatuses();
}

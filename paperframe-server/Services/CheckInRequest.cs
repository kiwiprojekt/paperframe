using paperframe_server.Helpers;

namespace paperframe_server.Services;

public record CheckInRequest(
    string DeviceId,
    string Service,
    string ConfigId,
    string Status,
    string Message,
    string ScreenResolution,
    string ScriptVersion,
    int? Battery = null)
{
    /// <summary>
    /// Builds a check-in from what the device reported about itself, so callers only
    /// have to supply what actually varies between them.
    /// </summary>
    public static CheckInRequest From(DeviceRequest device, string service, string configId, string status, string message) =>
        new(
            DeviceId: device.DeviceId,
            Service: service,
            ConfigId: configId,
            Status: status,
            Message: message,
            ScreenResolution: device.ScreenResolution,
            ScriptVersion: device.ScriptVersion,
            Battery: device.Battery);
}

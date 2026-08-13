namespace paperframe_server.Services;

public record CheckInRequest(
    string DeviceId,
    string Service,
    string ConfigId,
    string Status,
    string Message,
    string ScreenResolution,
    string ScriptVersion,
    int? Battery = null);

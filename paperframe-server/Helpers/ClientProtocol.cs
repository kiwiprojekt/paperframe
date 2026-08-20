namespace paperframe_server.Helpers;

/// <summary>
/// The contract between this server and the launcher shell script running on the
/// device. Both sides of each value live here: the C# that produces it and the
/// template that consumes it reference the same constant, so a change cannot
/// silently desynchronise them.
/// </summary>
public static class ClientProtocol
{
    /// <summary>Reported by the client as <c>script_version</c>. Bump when the launcher changes.</summary>
    public const string Version = "1.2";

    /// <summary>Response header carrying the seconds until the device's next wake.</summary>
    public const string SleepHeader = "X-Sleep-Time";

    /// <summary>
    /// Response header telling the device to stop its loop. A header rather than a
    /// script the device has to download and run, so "stop" is decided before the
    /// device commits to executing anything.
    /// </summary>
    public const string DisabledHeader = "X-Paperframe-Disabled";
}

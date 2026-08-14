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
    public const string Version = "1.1";

    /// <summary>Response header carrying the seconds until the device's next wake.</summary>
    public const string SleepHeader = "X-Sleep-Time";

    /// <summary>
    /// Exit code the disable script returns, so the launcher can tell "the server told
    /// me to stop" apart from "the rendering script crashed".
    /// </summary>
    public const int DisabledExitCode = 42;
}

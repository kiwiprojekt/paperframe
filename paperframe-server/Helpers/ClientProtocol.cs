namespace paperframe_server.Helpers;

/// <summary>
/// The contract between this server and the shell scripts running on the device. Both
/// sides of each value live here: the C# that produces it and the template that consumes
/// it reference the same constant, so a change cannot silently desynchronise them.
/// </summary>
public static class ClientProtocol
{
    /// <summary>Reported by the client as <c>script_version</c>. Bump when the launcher changes.</summary>
    public const string Version = "1.3";

    /// <summary>
    /// Oldest launcher the provisioning script knows how to replace. Anything older is left
    /// alone: it would download the new launcher, keep running the old code from its open
    /// shell, report the old version again on the next wake, and loop forever without ever
    /// rendering a frame.
    /// </summary>
    public const string ProvisioningFloorVersion = "1.2";

    /// <summary>
    /// Oldest launcher that understands <see cref="ProvisionedExitCode"/> and can restart
    /// itself. Older ones have to be taken over from the outside instead.
    /// </summary>
    public const string RestartProtocolVersion = "1.3";

    /// <summary>Response header carrying the seconds until the device's next wake.</summary>
    public const string SleepHeader = "X-Sleep-Time";

    /// <summary>
    /// Response header telling the device to stop its loop. A header rather than a
    /// script the device has to download and run, so "stop" is decided before the
    /// device commits to executing anything.
    /// </summary>
    public const string DisabledHeader = "X-Paperframe-Disabled";

    /// <summary>Where the launcher lives on the device. The provisioning script replaces this path.</summary>
    public const string LauncherDevicePath = "/mnt/us/documents/paperframe.sh";

    /// <summary>Directory the client keeps its own log and state in.</summary>
    public const string DeviceStateDir = "/mnt/us/paperframe";

    /// <summary>
    /// Last line of a complete launcher. A truncated download is otherwise valid shell
    /// right up to the point it stops, so the device checks for this before installing.
    /// </summary>
    public const string Sentinel = "# EOF-PAPERFRAME";

    /// <summary>
    /// Exit code the provisioning script uses to tell the launcher it has been replaced and
    /// the loop should restart on the new file.
    /// </summary>
    public const int ProvisionedExitCode = 42;

    /// <summary>
    /// Exit code the provisioning script uses to give up, asking the launcher to fetch its
    /// normal rendering script instead of sleeping on a blank screen.
    /// </summary>
    public const int ProvisionDeclinedExitCode = 43;

    private static readonly Version Current = new(Version);
    private static readonly Version ProvisioningFloor = new(ProvisioningFloorVersion);
    private static readonly Version RestartProtocolFloor = new(RestartProtocolVersion);

    /// <summary>Whether a reported <c>script_version</c> is the launcher this server ships.</summary>
    public static bool IsCurrent(string scriptVersion) => scriptVersion == Version;

    /// <summary>
    /// Whether a reported <c>script_version</c> belongs to a client that can be updated
    /// remotely. Unparseable and absent versions answer false — an unknown client is the
    /// one case where guessing wrong costs a device.
    /// </summary>
    public static bool SupportsProvisioning(string scriptVersion) =>
        AtLeast(scriptVersion, ProvisioningFloor);

    /// <summary>
    /// Whether a reported <c>script_version</c> can restart itself when the provisioning
    /// script signals success, rather than having to be taken over from the outside.
    /// </summary>
    public static bool SupportsSelfRestart(string scriptVersion) =>
        AtLeast(scriptVersion, RestartProtocolFloor);

    private static bool AtLeast(string scriptVersion, Version floor) =>
        System.Version.TryParse(scriptVersion, out var reported) && reported >= floor;

    /// <summary>Sanity check on the constants above, so a bad edit fails here rather than on a device.</summary>
    static ClientProtocol()
    {
        if (RestartProtocolFloor < ProvisioningFloor || Current < RestartProtocolFloor)
        {
            throw new InvalidOperationException(
                "Client protocol versions are inconsistent: the provisioning floor must not exceed the "
                + "restart-protocol floor, and neither may exceed the shipped launcher version.");
        }
    }
}

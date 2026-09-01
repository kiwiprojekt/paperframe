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
    /// Oldest launcher that can be updated remotely: the first one that understands the exit
    /// codes below and can restart itself onto a replacement.
    ///
    /// Anything older is served normally and flagged for a manual re-install. Driving those
    /// from the outside was tried and abandoned: a launcher that predates
    /// <see cref="ProvisionDeclinedExitCode"/> reads it as a script failure and stops, so
    /// every recoverable hiccup during an update — a failed download, a truncated file, a
    /// spent attempt budget — became a frame that stayed dark until someone walked over with
    /// a USB cable. Copying the file across by hand once costs a great deal less.
    /// </summary>
    public const string ProvisioningFloorVersion = "1.3";

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

    /// <summary>Whether a reported <c>script_version</c> is the launcher this server ships.</summary>
    public static bool IsCurrent(string scriptVersion) => scriptVersion == Version;

    /// <summary>
    /// Whether a reported <c>script_version</c> belongs to a client that can be updated
    /// remotely. Unparseable and absent versions answer false — an unknown client is the
    /// one case where guessing wrong costs a device.
    /// </summary>
    public static bool SupportsProvisioning(string scriptVersion) =>
        AtLeast(scriptVersion, ProvisioningFloor);


    private static bool AtLeast(string scriptVersion, Version floor) =>
        System.Version.TryParse(scriptVersion, out var reported) && reported >= floor;

    /// <summary>Sanity check on the constants above, so a bad edit fails here rather than on a device.</summary>
    static ClientProtocol()
    {
        if (Current < ProvisioningFloor)
        {
            throw new InvalidOperationException(
                "Client protocol versions are inconsistent: the provisioning floor must not exceed the "
                + "shipped launcher version.");
        }
    }
}

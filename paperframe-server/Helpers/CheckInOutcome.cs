using paperframe_server.Services;

namespace paperframe_server.Helpers;

/// <summary>
/// What the server has decided to do about a device that just checked in.
///
/// Keeping the decision separate from acting on it is what stops the entry point from
/// growing a branch per rule: every outcome carries its own log message, so the caller
/// dispatches rather than re-deriving why it is doing what it does.
/// </summary>
public enum CheckInVerdict
{
    /// <summary>The device id is absent from the configuration.</summary>
    NotConfigured,

    /// <summary>The device is configured but switched off in the manager.</summary>
    Disabled,

    /// <summary>The device runs an outdated launcher and can be updated in place.</summary>
    Provision,

    /// <summary>Business as usual: hand the device its rendering script.</summary>
    Serve
}

/// <param name="Verdict">What to do.</param>
/// <param name="Service">Rendering service to redirect to. Only meaningful for <see cref="CheckInVerdict.Serve"/>.</param>
/// <param name="ConfigId">Configuration the service should render.</param>
/// <param name="Message">What to write to the check-in log, whatever the verdict.</param>
public readonly record struct CheckInOutcome(
    CheckInVerdict Verdict,
    string Service,
    string ConfigId,
    string Message)
{
    /// <summary>Service name recorded for a device the configuration does not know.</summary>
    public const string UnknownService = "Unknown";

    /// <summary>Service name recorded for a check-in that is answered with a launcher update.</summary>
    public const string ProvisionService = "Provision";

    /// <summary>Config id recorded when the outcome has no configuration to render.</summary>
    public const string NoConfig = "None";

    /// <summary>
    /// The route this outcome redirects to, lowercased to match the client routes. A
    /// service with nothing to render — provisioning — is addressed by its bare route
    /// rather than carrying a placeholder id through the URL.
    /// </summary>
    public string RedirectPath => ConfigId == NoConfig
        ? $"/{Service.ToLowerInvariant()}"
        : $"/{Service.ToLowerInvariant()}/{ConfigId}";

    /// <summary>
    /// Decides a check-in from configuration alone. Pure, so the rules can be read and
    /// tested in one place instead of being inferred from the order of an if-chain.
    /// </summary>
    public static CheckInOutcome Resolve(DeviceRequest device, AppSettings config)
    {
        if (config.Devices == null || !config.Devices.TryGetValue(device.DeviceId, out var deviceConfig))
        {
            return new(CheckInVerdict.NotConfigured, UnknownService, NoConfig,
                "Device not found in server configuration.");
        }

        var service = deviceConfig.ServiceName ?? UnknownService;
        var configId = deviceConfig.ConfigId ?? NoConfig;

        if (deviceConfig.Disabled == true)
        {
            return new(CheckInVerdict.Disabled, service, configId, "Device is disabled on server.");
        }

        if (ClientProtocol.IsCurrent(device.ScriptVersion))
        {
            return Serve(service, configId);
        }

        // Reasons an outdated device is still just served. Each one is a case where sending
        // it to provisioning would cost more than leaving it a version behind.
        var declineReason = !ClientProtocol.SupportsProvisioning(device.ScriptVersion)
            // Its running shell would keep executing the old code, report the old version
            // on the next wake, and be sent straight back — a frame that never renders.
            ? $"Client {device.ScriptVersion} predates remote provisioning; re-install the launcher by hand."
            : device.SkipProvisioning
                // It tried, could not, and gave up. Sending it back would leave it cycling
                // on a blank screen instead of showing the frame it can still render.
                ? $"Client {device.ScriptVersion} could not update itself; serving normally."
                : deviceConfig.AutoUpdate != true
                    ? $"Client {device.ScriptVersion} is outdated but autoUpdate is off."
                    : null;

        return declineReason == null
            ? new(CheckInVerdict.Provision, ProvisionService, NoConfig,
                $"Client {device.ScriptVersion} is outdated; sending launcher {ClientProtocol.Version}.")
            : Serve(service, configId, declineReason);
    }

    private static CheckInOutcome Serve(string service, string configId, string? reason = null) =>
        new(CheckInVerdict.Serve, service, configId,
            reason == null
                ? $"Redirected to /{service}/{configId}"
                : $"{reason} Redirected to /{service}/{configId}");

    /// <summary>Builds the check-in record for this outcome.</summary>
    public CheckInRequest ToCheckIn(DeviceRequest device) =>
        CheckInRequest.From(device, Service, ConfigId, StatusOf(Verdict), Message);

    /// <summary>
    /// The log status each verdict is recorded under. These strings predate the verdicts
    /// and are what the manager's log view displays, so the mapping lives here rather than
    /// being taken from the enum's name.
    /// </summary>
    private static string StatusOf(CheckInVerdict verdict) => verdict switch
    {
        CheckInVerdict.NotConfigured => "Error",
        CheckInVerdict.Disabled => "Disabled",
        CheckInVerdict.Provision => "Provision",
        _ => "Redirect"
    };
}

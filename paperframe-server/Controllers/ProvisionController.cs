using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using paperframe_server.Filters;
using paperframe_server.Helpers;

namespace paperframe_server.Controllers;

/// <summary>
/// Replaces the launcher on a device running an outdated one.
///
/// Shaped like every other device-facing service — a small script the device downloads
/// and runs, which then fetches what it needs from a route beneath this one — so an
/// update travels the same path as a rendering job rather than needing its own protocol.
///
/// Routed outside <c>/api</c> with the other client routes: anything under <c>/api</c>
/// requires an admin session cookie that a Kindle does not have.
/// </summary>
[ApiController]
[Route("provision")]
public class ProvisionController : ControllerBase
{
    private readonly IOptionsMonitor<AppSettings> _options;

    public ProvisionController(IOptionsMonitor<AppSettings> options)
    {
        _options = options;
    }

    [HttpGet]
    [DeviceCheckIn]
    public string Get() =>
        ShellScript.Render(ShellScript.Provision,
            // The launcher lives directly under this request's own route.
            ShellScript.Text("LAUNCHER_PATH_URL", $"{Request.Path.Value?.TrimEnd('/')}/launcher"),
            // Whether the running launcher can restart itself is a version comparison, and
            // this is where versions are compared. The script only reads the answer.
            ShellScript.Raw("LEGACY_HANDOVER",
                ClientProtocol.SupportsSelfRestart(HttpContext.Device().ScriptVersion) ? "" : "1"));

    [HttpGet("launcher")]
    [IdentifiedDevice]
    public IActionResult GetLauncher()
    {
        var device = HttpContext.Device();

        // Same rule as every other device route: an id the configuration does not know gets
        // nothing. This one hands out a script carrying the device's own identity, so it is
        // not somewhere to relax that.
        if (_options.CurrentValue.Devices?.ContainsKey(device.DeviceId) != true)
        {
            return NotFound("Device not configured.");
        }

        return Content(
            LauncherScript.Render(device.DeviceId, $"{Request.Scheme}://{Request.Host}"),
            "application/x-sh");
    }
}

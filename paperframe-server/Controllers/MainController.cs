using paperframe_server.Services;
using paperframe_server.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System;

namespace paperframe_server.Controllers;

[ApiController]
[Route("/")]
public class MainController : ControllerBase
{
    private readonly IHomeAssistantService _homeAssistantService;
    private readonly IPaperframeLogService _logService;
    private readonly IWebHostEnvironment _env;
    private readonly AppSettings _config;

    public MainController(
        IOptionsSnapshot<AppSettings> appSettings,
        IHomeAssistantService homeAssistantService,
        IPaperframeLogService logService,
        IWebHostEnvironment env)
    {
        _homeAssistantService = homeAssistantService;
        _logService = logService;
        _env = env;
        _config = appSettings.Value;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var device = HttpContext.Device();

        // A browser sends no device_id, so this same route serves the admin UI.
        if (!device.IsIdentified)
        {
            return AdminUi();
        }

        if (_config.Devices == null || !_config.Devices.TryGetValue(device.DeviceId, out var deviceConfig))
        {
            _logService.LogCheckIn(CheckInRequest.From(
                device, "Unknown", "None", "Error", "Device not found in server configuration."));

            return NotFound("Device not configured.");
        }

        var serviceName = deviceConfig.ServiceName ?? "Unknown";
        var configId = deviceConfig.ConfigId ?? "None";

        if (deviceConfig.Disabled == true)
        {
            _logService.LogCheckIn(CheckInRequest.From(
                device, serviceName, configId, "Disabled", "Device is disabled on server."));

            // The header is what stops the loop; the body is an inert comment so that a
            // client which somehow ran it anyway would still do nothing.
            Response.Headers[ClientProtocol.DisabledHeader] = "1";

            return Content("# Device is disabled on the Paperframe server.\n", "text/plain");
        }

        _ = _homeAssistantService.UpdateEntities(device.DeviceId, device.Battery)
            .ContinueWith(t =>
            {
                if (t.Exception != null)
                    Console.WriteLine($"HA update failed for {device.DeviceId}: {t.Exception.InnerException?.Message}");
            }, TaskContinuationOptions.OnlyOnFaulted);

        _logService.LogCheckIn(CheckInRequest.From(
            device, serviceName, configId, "Redirect", $"Redirected to /{serviceName}/{configId}"));

        return Redirect($"/{serviceName.ToLower()}/{configId}");
    }

    private IActionResult AdminUi()
    {
        var indexPath = Path.Combine(_env.ContentRootPath, "StaticAssets", "index.html");

        return System.IO.File.Exists(indexPath)
            ? PhysicalFile(indexPath, "text/html")
            : Ok("Paperframe Server is active. Admin UI is missing from StaticAssets/index.html.");
    }
}

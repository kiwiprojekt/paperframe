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
    public IActionResult Get([FromHeader(Name = "device_id")] string? deviceId = null)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            // Serve Administration UI if no device_id header is present
            var indexPath = Path.Combine(_env.ContentRootPath, "StaticAssets", "index.html");
            return System.IO.File.Exists(indexPath)
                ? PhysicalFile(indexPath, "text/html")
                : Ok("Paperframe Server is active. Admin UI is missing from StaticAssets/index.html.");
        }

        var device = DeviceRequestReader.Read(Request.Headers);

        if (_config.Devices == null || !_config.Devices.TryGetValue(device.DeviceId, out var deviceConfig))
        {
            _logService.LogCheckIn(new CheckInRequest(
                DeviceId: device.DeviceId,
                Battery: device.Battery,
                ScreenResolution: device.ScreenResolution,
                Service: "Unknown",
                ConfigId: "None",
                Status: "Error",
                Message: "Device not found in server configuration.",
                ScriptVersion: device.ScriptVersion));

            return NotFound("Device not configured.");
        }

        var serviceName = deviceConfig.ServiceName ?? "Unknown";
        var configId = deviceConfig.ConfigId ?? "None";

        if (deviceConfig.Disabled == true)
        {
            _logService.LogCheckIn(new CheckInRequest(
                DeviceId: device.DeviceId,
                Battery: device.Battery,
                ScreenResolution: device.ScreenResolution,
                Service: serviceName,
                ConfigId: configId,
                Status: "Disabled",
                Message: "Device is disabled on server.",
                ScriptVersion: device.ScriptVersion));

            var disableScript = $@"#!/bin/sh
# Name: DisableDevice
# Author: Paperframe Server
# Device is disabled on the Paperframe Server

echo ""Device {device.DeviceId} is disabled.""
lipc-set-prop com.lab126.powerd preventScreenSaver 0
exit 1
";
            return Content(disableScript, "text/plain");
        }

        _ = _homeAssistantService.UpdateEntities(device.DeviceId, device.Battery)
            .ContinueWith(t =>
            {
                if (t.Exception != null)
                    Console.WriteLine($"HA update failed for {device.DeviceId}: {t.Exception.InnerException?.Message}");
            }, TaskContinuationOptions.OnlyOnFaulted);

        _logService.LogCheckIn(new CheckInRequest(
            DeviceId: device.DeviceId,
            Battery: device.Battery,
            ScreenResolution: device.ScreenResolution,
            Service: serviceName,
            ConfigId: configId,
            Status: "Redirect",
            Message: $"Redirected to /{serviceName}/{configId}",
            ScriptVersion: device.ScriptVersion));

        return Redirect($"/{serviceName.ToLower()}/{configId}");
    }
}
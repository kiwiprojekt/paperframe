using paperframe_server.Services;
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
        var hasBattery = this.Request.Headers.TryGetValue("battery", out var batteryVal);
        var hasRes = this.Request.Headers.TryGetValue("screen_res", out var resVal);
        int? battery = hasBattery && int.TryParse(batteryVal, out var b) ? b : null;
        string res = hasRes ? resVal.ToString() : "unknown";

        if (!string.IsNullOrEmpty(deviceId))
        {
            if (_config.Devices != null && _config.Devices.TryGetValue(deviceId, out var deviceConfig))
            {
                if (deviceConfig.Disabled == true)
                {
                    _logService.LogCheckIn(
                        deviceId, 
                        battery, 
                        res, 
                        deviceConfig.ServiceName, 
                        deviceConfig.ConfigId, 
                        "Disabled", 
                        "Device is disabled on server.");

                    var disableScript = $@"#!/bin/sh
# Name: DisableDevice
# Author: Paperframe Server
# Device is disabled on the Paperframe Server

echo ""Device {deviceId} is disabled.""
lipc-set-prop com.lab126.powerd preventScreenSaver 0
exit 1
";
                    return Content(disableScript, "text/plain");
                }

                _ = _homeAssistantService.UpdateEntities(deviceId, battery)
                    .ContinueWith(t =>
                    {
                        if (t.Exception != null)
                            Console.WriteLine($"HA update failed for {deviceId}: {t.Exception.InnerException?.Message}");
                    }, TaskContinuationOptions.OnlyOnFaulted);

                _logService.LogCheckIn(
                    deviceId, 
                    battery, 
                    res, 
                    deviceConfig.ServiceName, 
                    deviceConfig.ConfigId, 
                    "Redirect", 
                    $"Redirected to /{deviceConfig.ServiceName}/{deviceConfig.ConfigId}");

                return Redirect($"/{deviceConfig.ServiceName.ToLower()}/{deviceConfig.ConfigId}");
            }
            else
            {
                _logService.LogCheckIn(
                    deviceId, 
                    battery, 
                    res, 
                    "Unknown", 
                    "None", 
                    "Error", 
                    $"Device not found in server configuration.");

                return NotFound("Device not configured.");
            }
        }
        
        // Serve Administration UI if no device_id header is present
        var indexPath = Path.Combine(_env.ContentRootPath, "StaticAssets", "index.html");
        if (System.IO.File.Exists(indexPath))
        {
            return PhysicalFile(indexPath, "text/html");
        }
        else
        {
            return Ok("Paperframe Server is active. Admin UI is missing from StaticAssets/index.html.");
        }
    }
}
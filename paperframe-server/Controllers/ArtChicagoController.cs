using System.Text.RegularExpressions;
using paperframe_server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace paperframe_server.Controllers;

[ApiController]
[Route("artchicago")]
public class ArtChicagoController : ControllerBase
{
    private static string ShellEscape(string input) =>
        Regex.Replace(input, @"[""\\$`!\r\n\t]", "");

    private readonly IArtChicagoService _artChicagoService;
    private readonly IPaperframeLogService _logService;
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;

    public ArtChicagoController(
        IArtChicagoService artChicagoService,
        IOptionsMonitor<AppSettings> options,
        IPaperframeLogService logService)
    {
        _artChicagoService = artChicagoService;
        _logService = logService;
        _optionsMonitor = options;
    }

    [HttpGet("{configId}")]
    public string Get(string configId)
    {
        var deviceId = Request.Headers["device_id"].FirstOrDefault() ?? "unknown";
        var batteryVal = Request.Headers["battery"].FirstOrDefault();
        int? battery = int.TryParse(batteryVal, out var b) ? b : null;
        var screenRes = Request.Headers["screen_res"].FirstOrDefault() ?? "758,1024";

        try
        {
            var configs = _optionsMonitor.CurrentValue.ArtChicago;
            if (configs == null || !configs.TryGetValue(configId, out var config))
            {
                throw new KeyNotFoundException($"Layout configuration '{configId}' is not defined in ArtChicago configs.");
            }

            var imageUrl = $"/artchicago/{configId}/image";
            var script = $@"#!/bin/sh

FBINK=""{config.FbinkPath ?? "/mnt/us/libkh/bin/fbink"}""
IMAGE_URL=$SERVICES_URL""{imageUrl}""

$FBINK -q -k

wget  --header=""device_id: $DEVICE_ID"" \
    --header=""screen_res: $SCREEN_RES"" \
    -O image.jpeg $IMAGE_URL; \
    image_result=$?

if [ $image_result -ne 0 ]; then
    return 1;
fi

$FBINK --image file=image.jpeg,dither
";
            _logService.LogCheckIn(deviceId, battery, screenRes, "ArtChicago", configId, "Success", "ArtChicago launcher script compiled successfully.");

            return script;
        }
        catch (Exception ex)
        {
            _logService.LogCheckIn(deviceId, battery, screenRes, "ArtChicago", configId, "Error", $"Layout compile failed: {ex.Message}");

            return $@"#!/bin/sh
# ARTCHICAGO COMPILE ERROR RUNTIME FALLBACK
FBINK=""/mnt/us/libkh/bin/fbink""
$FBINK -q -k
$FBINK -q ""ARTCHICAGO COMPILE ERROR"" -t size=20,top=200 -O -m -C GRAY9
$FBINK -q ""Config ID: {ShellEscape(configId)}"" -t size=12,top=260 -O -m -C GRAY6
$FBINK -q ""Error: {ShellEscape(ex.Message)}"" -t size=10,top=320 -O -m -C GRAY3
";
        }
    }

    [HttpGet("{configId}/image")]
    public async Task GetImage(string configId, [FromHeader(Name="screen_res")] string? screenRes = null, [FromHeader(Name="device_id")] string? deviceId = null)
    {
        var batteryVal = Request.Headers["battery"].FirstOrDefault();
        int? battery = int.TryParse(batteryVal, out var b) ? b : null;
        
        if (string.IsNullOrEmpty(screenRes)) screenRes = "758,1024";
        if (string.IsNullOrEmpty(deviceId)) deviceId = "unknown";

        try
        {
            if (deviceId == "unknown")
            {
                throw new ArgumentException("Missing 'device_id' header in photo request. Make sure the Paperframe client sends a valid device identifier.");
            }

            var configs = _optionsMonitor.CurrentValue.ArtChicago;
            if (configs == null || !configs.TryGetValue(configId, out var config))
            {
                throw new KeyNotFoundException($"Layout configuration '{configId}' is not defined in ArtChicago configs.");
            }

            var (x, y) = parseRes(screenRes);
            var image = await _artChicagoService.GetImage(config, deviceId, x, y);
            
            _logService.LogCheckIn(deviceId, battery, screenRes, "ArtChicagoImage", configId, "Success", "Art dithered and served successfully.");

            Response.ContentType = "image/jpeg";
            await this.HttpContext.Response.Body.WriteAsync(image, 0, image.Length);
        }
        catch (Exception ex)
        {
            _logService.LogCheckIn(deviceId, battery, screenRes, "ArtChicagoImage", configId, "Error", $"Serving photo failed: {ex.Message}");
            throw;
        }
    }

    private (uint x, uint y) parseRes(string res)
    {
        var parts = res.Split(",");
        if (parts.Length >= 2 && uint.TryParse(parts[0], out var x) && uint.TryParse(parts[1], out var y))
            return (x, y);
        return (758, 1024);
    }
}

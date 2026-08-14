using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using paperframe_server.Services;
using paperframe_server.Filters;
using paperframe_server.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace paperframe_server.Controllers;

[ApiController]
[Route("artchicago")]
public class ArtChicagoController : ControllerBase
{
    private readonly IArtChicagoService _artChicagoService;
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;
    private readonly IPaperframeLogService _logService;

    public ArtChicagoController(
        IArtChicagoService artChicagoService,
        IOptionsMonitor<AppSettings> options,
        IPaperframeLogService logService)
    {
        _artChicagoService = artChicagoService;
        _optionsMonitor = options;
        _logService = logService;
    }

    [HttpGet("{configId}")]
    [DeviceScript]
    public string Get(string configId)
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
    --header=""script_version: $SCRIPT_VERSION"" \
    -O image.jpeg $IMAGE_URL; \
    image_result=$?

if [ $image_result -ne 0 ]; then
    return 1;
fi

$FBINK --image file=image.jpeg,dither
";
        return script;
    }

    [HttpGet("{configId}/image")]
    public async Task GetImage(string configId)
    {
        var device = DeviceRequestReader.Read(Request.Headers);

        try
        {
            if (!device.IsIdentified)
            {
                throw new ArgumentException("Missing 'device_id' header in photo request. Make sure the Paperframe client sends a valid device identifier.");
            }

            var configs = _optionsMonitor.CurrentValue.ArtChicago;
            if (configs == null || !configs.TryGetValue(configId, out var config))
            {
                throw new KeyNotFoundException($"Layout configuration '{configId}' is not defined in ArtChicago configs.");
            }

            var (x, y) = parseRes(device.ScreenResolution);
            var image = await _artChicagoService.GetImage(config, device.DeviceId, x, y);
            
            _logService.LogCheckIn(CheckInRequest.From(
                device, "ArtChicagoImage", configId, "Success", "Art dithered and served successfully."));

            Response.ContentType = "image/jpeg";
            await this.HttpContext.Response.Body.WriteAsync(image, 0, image.Length);
        }
        catch (Exception ex)
        {
            _logService.LogCheckIn(CheckInRequest.From(
                device, "ArtChicagoImage", configId, "Error", $"Serving photo failed: {ex.Message}"));
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

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
[Route("immich")]
public class ImmichController : ControllerBase
{
    private readonly IImmichService _immichService;
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;
    private readonly IPaperframeLogService _logService;

    public ImmichController(
        IImmichService immichService,
        IOptionsMonitor<AppSettings> options,
        IPaperframeLogService logService)
    {
        _immichService = immichService;
        _optionsMonitor = options;
        _logService = logService;
    }

    [HttpGet("{configId}")]
    [DeviceScript]
    public string Get(string configId)
    {
        var configs = _optionsMonitor.CurrentValue.Immich;
        if (configs == null || !configs.TryGetValue(configId, out var config))
        {
            throw new KeyNotFoundException($"Layout configuration '{configId}' is not defined in Immich configs.");
        }

        var imageUrl = $"/immich/{configId}/image";
        var script = $@"#!/bin/sh

FBINK=""{config.FbinkPath}""
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

            var configs = _optionsMonitor.CurrentValue.Immich;
            if (configs == null || !configs.TryGetValue(configId, out var config))
            {
                throw new KeyNotFoundException($"Layout configuration '{configId}' is not defined in Immich configs.");
            }

            var (x, y) = parseRes(device.ScreenResolution);
            var image = await _immichService.GetImage(config, device.DeviceId, x, y);
            
            // Log successful photo download
            _logService.LogCheckIn(CheckInRequest.From(
                device, "ImmichImage", configId, "Success", "Photo dithered and served successfully."));

            await this.HttpContext.Response.Body.WriteAsync(image, 0, image.Length);
        }
        catch (Exception ex)
        {
            _logService.LogCheckIn(CheckInRequest.From(
                device, "ImmichImage", configId, "Error", $"Serving photo failed: {ex.Message}"));
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
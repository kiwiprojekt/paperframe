using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using paperframe_server.Filters;
using paperframe_server.Helpers;
using paperframe_server.Services;

namespace paperframe_server.Controllers;

/// <summary>
/// A service that fills the screen with a single dithered photo: the device fetches a
/// small renderer script, which then fetches the image itself.
///
/// Both endpoints are identical across every such service, so subclasses supply only
/// what actually differs — where the configs live, and how to produce the bytes.
/// </summary>
public abstract class PhotoFrameController<TConfig> : ControllerBase
{
    private const string DefaultFbinkPath = "/mnt/us/libkh/bin/fbink";

    private readonly IPaperframeLogService _logService;

    protected PhotoFrameController(IPaperframeLogService logService)
    {
        _logService = logService;
    }

    /// <summary>Name used in check-in logs and in the on-screen error banner.</summary>
    protected abstract string ServiceName { get; }

    protected abstract IDictionary<string, TConfig>? Configs { get; }

    protected abstract string? FbinkPathOf(TConfig config);

    protected abstract Task<byte[]> RenderAsync(TConfig config, DeviceRequest device, ScreenSize screen);

    [HttpGet("{configId}")]
    [DeviceScript]
    public string Get(string configId)
    {
        var config = Require(configId);

        return ShellScript.Render(ShellScript.PhotoFrame,
            ShellScript.Text("SERVICE", ServiceName),
            ShellScript.Text("FBINK_PATH", FbinkPathOf(config) ?? DefaultFbinkPath),
            // The image lives directly under this request's own route.
            ShellScript.Raw("IMAGE_PATH", $"{Request.Path.Value?.TrimEnd('/')}/image"));
    }

    [HttpGet("{configId}/image")]
    [IdentifiedDevice]
    public async Task GetImage(string configId)
    {
        var device = DeviceRequestReader.Read(Request.Headers);

        try
        {
            var image = await RenderAsync(Require(configId), device, device.Screen);

            _logService.LogCheckIn(CheckInRequest.From(
                device, $"{ServiceName}Image", configId, "Success", "Photo dithered and served successfully."));

            Response.ContentType = "image/jpeg";
            await Response.Body.WriteAsync(image);
        }
        catch (Exception ex)
        {
            _logService.LogCheckIn(CheckInRequest.From(
                device, $"{ServiceName}Image", configId, "Error", $"Serving photo failed: {ex.Message}"));
            throw;
        }
    }

    private TConfig Require(string configId)
    {
        if (Configs == null || !Configs.TryGetValue(configId, out var config))
        {
            throw new KeyNotFoundException($"Layout configuration '{configId}' is not defined in {ServiceName} configs.");
        }

        return config;
    }
}

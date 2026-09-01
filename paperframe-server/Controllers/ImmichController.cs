using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using paperframe_server.Helpers;
using paperframe_server.Services;

namespace paperframe_server.Controllers;

[ApiController]
[Route("immich")]
public class ImmichController : PhotoFrameController<AppSettings.ImmichConfig>
{
    private readonly IImmichService _immichService;
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;

    public ImmichController(
        IImmichService immichService,
        IOptionsMonitor<AppSettings> options,
        IPaperframeLogService logService)
        : base(logService)
    {
        _immichService = immichService;
        _optionsMonitor = options;
    }

    protected override string ServiceName => "Immich";

    protected override IDictionary<string, AppSettings.ImmichConfig>? Configs => _optionsMonitor.CurrentValue.Immich;

    protected override string? FbinkPathOf(AppSettings.ImmichConfig config) => config.FbinkPath;

    protected override Task<byte[]> RenderAsync(AppSettings.ImmichConfig config, DeviceRequest device, ScreenSize screen) =>
        _immichService.GetImage(config, device.DeviceId, screen.Width, screen.Height);
}

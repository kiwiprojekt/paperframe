using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using paperframe_server.Helpers;
using paperframe_server.Services;

namespace paperframe_server.Controllers;

[ApiController]
[Route("artchicago")]
public class ArtChicagoController : PhotoFrameController<AppSettings.ArtChicagoConfig>
{
    private readonly IArtChicagoService _artChicagoService;
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;

    public ArtChicagoController(
        IArtChicagoService artChicagoService,
        IOptionsMonitor<AppSettings> options,
        IPaperframeLogService logService)
        : base(logService)
    {
        _artChicagoService = artChicagoService;
        _optionsMonitor = options;
    }

    protected override string ServiceName => "ArtChicago";

    protected override IDictionary<string, AppSettings.ArtChicagoConfig>? Configs => _optionsMonitor.CurrentValue.ArtChicago;

    protected override string? FbinkPathOf(AppSettings.ArtChicagoConfig config) => config.FbinkPath;

    protected override Task<byte[]> RenderAsync(AppSettings.ArtChicagoConfig config, DeviceRequest device, ScreenSize screen) =>
        _artChicagoService.GetImage(config, device.DeviceId, screen.Width, screen.Height);
}

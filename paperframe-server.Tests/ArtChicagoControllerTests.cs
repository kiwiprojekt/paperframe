using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using paperframe_server.Controllers;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace paperframe_server.Tests;

public class ArtChicagoControllerTests
{
    [Fact]
    public void Get_generates_launcher_script()
    {
        var controller = NewController();

        var script = controller.Get("frame");

        script.Should().Contain("IMAGE_URL=$SERVICES_URL\"/artchicago/frame/image\"");
        script.Should().Contain("FBINK=\"/mnt/us/libkh/bin/fbink\"");
    }

    [Fact]
    public void Get_throws_when_config_is_missing()
    {
        var controller = NewController(options: new AppSettings { ArtChicago = new() });

        var act = () => controller.Get("missing");

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("Layout configuration 'missing' is not defined in ArtChicago configs.");
    }

    [Fact]
    public async Task GetImage_writes_service_bytes_and_passes_parsed_resolution()
    {
        var imageBytes = new byte[] { 1, 2, 3 };
        var artChicagoService = Substitute.For<IArtChicagoService>();
        artChicagoService.GetImage(Arg.Any<AppSettings.ArtChicagoConfig>(), "kindle-a", 600, 800)
            .Returns(imageBytes);
        var logService = Substitute.For<IPaperframeLogService>();
        var controller = NewController(artChicagoService, logService: logService);
        controller.Request.Headers["device_id"] = "kindle-a";
        controller.Request.Headers["screen_res"] = "600,800";
        controller.Request.Headers["battery"] = "12";
        controller.Response.Body = new MemoryStream();

        await controller.GetImage("frame");

        ((MemoryStream)controller.Response.Body).ToArray().Should().Equal(imageBytes);
        await artChicagoService.Received().GetImage(Arg.Any<AppSettings.ArtChicagoConfig>(), "kindle-a", 600, 800);
        logService.Received().LogCheckIn(Arg.Is<CheckInRequest>(r => 
            r.DeviceId == "kindle-a" && r.Battery == 12 && r.ScreenResolution == "600,800" && 
            r.Service == "ArtChicagoImage" && r.ConfigId == "frame" && r.Status == "Success"));
    }

    [Fact]
    public async Task GetImage_uses_default_resolution_when_header_is_invalid()
    {
        var artChicagoService = Substitute.For<IArtChicagoService>();
        artChicagoService.GetImage(Arg.Any<AppSettings.ArtChicagoConfig>(), "kindle-a", 758, 1024)
            .Returns(new byte[] { 1 });
        var controller = NewController(artChicagoService);
        controller.Request.Headers["device_id"] = "kindle-a";
        controller.Request.Headers["screen_res"] = "invalid";
        controller.Response.Body = new MemoryStream();

        await controller.GetImage("frame");

        await artChicagoService.Received().GetImage(Arg.Any<AppSettings.ArtChicagoConfig>(), "kindle-a", 758, 1024);
    }

    [Fact]
    public async Task GetImage_logs_and_rethrows_when_device_id_is_missing()
    {
        var logService = Substitute.For<IPaperframeLogService>();
        var controller = NewController(logService: logService);
        controller.Request.Headers["screen_res"] = "600,800";
        controller.Response.Body = new MemoryStream();

        var act = () => controller.GetImage("frame");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Missing 'device_id' header in photo request*");
        logService.Received().LogCheckIn(Arg.Is<CheckInRequest>(r => 
            r.DeviceId == "unknown" && r.Battery == null && r.ScreenResolution == "600,800" && 
            r.Service == "ArtChicagoImage" && r.ConfigId == "frame" && r.Status == "Error"));
    }

    private static ArtChicagoController NewController(
        IArtChicagoService? artChicagoService = null,
        IPaperframeLogService? logService = null,
        AppSettings? options = null)
    {
        var controller = new ArtChicagoController(
            artChicagoService ?? Substitute.For<IArtChicagoService>(),
            new TestOptionsMonitor<AppSettings>(options ?? new AppSettings
            {
                ArtChicago = new Dictionary<string, AppSettings.ArtChicagoConfig>
                {
                    ["frame"] = new()
                    {
                        Query = "painting",
                        FbinkPath = "/mnt/us/libkh/bin/fbink"
                    }
                }
            }),
            logService ?? Substitute.For<IPaperframeLogService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }
}

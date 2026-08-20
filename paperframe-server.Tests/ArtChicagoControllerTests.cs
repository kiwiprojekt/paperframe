using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using paperframe_server.Controllers;
using paperframe_server.Filters;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;
using System;
using System.IO;
using System.Threading;
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
    public async Task GetImage_logs_aborted_when_client_disconnects_mid_render()
    {
        var cts = new CancellationTokenSource();
        var artChicagoService = Substitute.For<IArtChicagoService>();
        artChicagoService.GetImage(Arg.Any<AppSettings.ArtChicagoConfig>(), "kindle-a", 600, 800)
            .Returns((Func<NSubstitute.Core.CallInfo, byte[]>)(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            }));
        var logService = Substitute.For<IPaperframeLogService>();
        var controller = NewController(artChicagoService, logService: logService);
        controller.Request.Headers["device_id"] = "kindle-a";
        controller.Request.Headers["screen_res"] = "600,800";
        controller.HttpContext.RequestAborted = cts.Token;

        var act = () => controller.GetImage("frame");

        await act.Should().ThrowAsync<OperationCanceledException>();
        logService.Received().LogCheckIn(Arg.Is<CheckInRequest>(r =>
            r.DeviceId == "kindle-a" && r.Service == "ArtChicagoImage" && r.ConfigId == "frame" && r.Status == "Aborted"));
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
    public void GetImage_carries_the_identified_device_filter()
    {
        // Rejecting an unidentified caller is the filter's job, so every device
        // route answers 400 instead of each one inventing its own failure mode.
        typeof(ArtChicagoController).GetMethod(nameof(ArtChicagoController.GetImage))!
            .GetCustomAttributes(typeof(IdentifiedDeviceAttribute), inherit: true)
            .Should().NotBeEmpty();
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
            HttpContext = new DefaultHttpContext { Request = { Path = "/artchicago/frame" } }
        };
        return controller;
    }
}

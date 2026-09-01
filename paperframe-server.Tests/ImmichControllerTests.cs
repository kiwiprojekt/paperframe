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
using System.Threading.Tasks;
using System.Collections.Generic;

namespace paperframe_server.Tests;

public class ImmichControllerTests
{
    [Fact]
    public void Get_generates_launcher_script()
    {
        var controller = NewController();

        var script = controller.Get("frame");

        script.Should().Contain("IMAGE_URL=$SERVICES_URL\"/immich/frame/image\"");
        script.Should().Contain("FBINK=\"/mnt/us/libkh/bin/fbink\"");
    }

    [Fact]
    public void Get_falls_back_to_the_default_fbink_path_when_unset()
    {
        var controller = NewController(options: new AppSettings
        {
            Immich = new Dictionary<string, AppSettings.ImmichConfig> { ["frame"] = new() { FbinkPath = null } }
        });

        controller.Get("frame").Should().Contain("FBINK=\"/mnt/us/libkh/bin/fbink\"");
    }

    [Fact]
    public void Get_script_exits_rather_than_returning_when_the_image_fetch_fails()
    {
        // "return" outside a function is an error in ash/dash: the script would
        // carry on and render a stale image instead of reporting the failure.
        var script = NewController().Get("frame");

        script.Should().Contain("exit 1");
        script.Should().NotContain("return 1");
    }

    [Fact]
    public async Task GetImage_declares_the_jpeg_content_type()
    {
        var immichService = Substitute.For<IImmichService>();
        immichService.GetImage(Arg.Any<AppSettings.ImmichConfig>(), Arg.Any<string>(), Arg.Any<uint>(), Arg.Any<uint>())
            .Returns(new byte[] { 1 });
        var controller = NewController(immichService);
        controller.Request.Headers["device_id"] = "kindle-a";
        controller.Response.Body = new MemoryStream();

        await controller.GetImage("frame");

        controller.Response.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public void Get_throws_when_config_is_missing()
    {
        var controller = NewController(options: new AppSettings { Immich = new() });

        var act = () => controller.Get("missing");

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("Layout configuration 'missing' is not defined in Immich configs.");
    }

    [Fact]
    public async Task GetImage_writes_service_bytes_and_passes_parsed_resolution()
    {
        var imageBytes = new byte[] { 1, 2, 3 };
        var immichService = Substitute.For<IImmichService>();
        immichService.GetImage(Arg.Any<AppSettings.ImmichConfig>(), "kindle-a", 600, 800)
            .Returns(imageBytes);
        var logService = Substitute.For<IPaperframeLogService>();
        var controller = NewController(immichService, logService: logService);
        controller.Request.Headers["device_id"] = "kindle-a";
        controller.Request.Headers["screen_res"] = "600,800";
        controller.Request.Headers["battery"] = "12";
        controller.Response.Body = new MemoryStream();

        await controller.GetImage("frame");

        ((MemoryStream)controller.Response.Body).ToArray().Should().Equal(imageBytes);
        await immichService.Received().GetImage(Arg.Any<AppSettings.ImmichConfig>(), "kindle-a", 600, 800);
        logService.Received().LogCheckIn(Arg.Is<CheckInRequest>(r => 
            r.DeviceId == "kindle-a" && r.Battery == 12 && r.ScreenResolution == "600,800" && 
            r.Service == "ImmichImage" && r.ConfigId == "frame" && r.Status == "Success"));
    }

    [Fact]
    public async Task GetImage_uses_default_resolution_when_header_is_invalid()
    {
        var immichService = Substitute.For<IImmichService>();
        immichService.GetImage(Arg.Any<AppSettings.ImmichConfig>(), "kindle-a", 758, 1024)
            .Returns(new byte[] { 1 });
        var controller = NewController(immichService);
        controller.Request.Headers["device_id"] = "kindle-a";
        controller.Request.Headers["screen_res"] = "invalid";
        controller.Response.Body = new MemoryStream();

        await controller.GetImage("frame");

        await immichService.Received().GetImage(Arg.Any<AppSettings.ImmichConfig>(), "kindle-a", 758, 1024);
    }

    [Fact]
    public void GetImage_carries_the_identified_device_filter()
    {
        // Rejecting an unidentified caller is the filter's job, so every device
        // route answers 400 instead of each one inventing its own failure mode.
        typeof(ImmichController).GetMethod(nameof(ImmichController.GetImage))!
            .GetCustomAttributes(typeof(IdentifiedDeviceAttribute), inherit: true)
            .Should().NotBeEmpty();
    }

    private static ImmichController NewController(
        IImmichService? immichService = null,
        IPaperframeLogService? logService = null,
        AppSettings? options = null)
    {
        var controller = new ImmichController(
            immichService ?? Substitute.For<IImmichService>(),
            new TestOptionsMonitor<AppSettings>(options ?? new AppSettings
            {
                Immich = new Dictionary<string, AppSettings.ImmichConfig>
                {
                    ["frame"] = new()
                    {
                        ApiUrl = "http://immich.local/api/",
                        ApiKey = "key",
                        AlbumName = "Frame",
                        FbinkPath = "/mnt/us/libkh/bin/fbink"
                    }
                }
            }),
            logService ?? Substitute.For<IPaperframeLogService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { Request = { Path = "/immich/frame" } }
        };
        return controller;
    }
}

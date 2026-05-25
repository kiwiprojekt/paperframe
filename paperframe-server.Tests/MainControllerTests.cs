using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using paperframe_server.Controllers;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;

namespace paperframe_server.Tests;

public class MainControllerTests
{
    [Fact]
    public void Get_without_device_id_serves_admin_ui_when_present()
    {
        var root = Directory.CreateTempSubdirectory("paperframe-static-test-");
        Directory.CreateDirectory(Path.Combine(root.FullName, "StaticAssets"));
        File.WriteAllText(Path.Combine(root.FullName, "StaticAssets", "index.html"), "<html>manager</html>");
        var controller = NewController(new AppSettings(), contentRoot: root.FullName);

        var result = controller.Get();

        var file = result.Should().BeOfType<PhysicalFileResult>().Subject;
        file.FileName.Should().EndWith(Path.Combine("StaticAssets", "index.html"));
        file.ContentType.Should().Be("text/html");
    }

    [Fact]
    public void Get_unknown_device_returns_404_and_logs_error()
    {
        var logService = Substitute.For<IPaperframeLogService>();
        var controller = NewController(new AppSettings { Devices = new() }, logService: logService);
        controller.Request.Headers["device_id"] = "kindle-missing";
        controller.Request.Headers["battery"] = "80";
        controller.Request.Headers["screen_res"] = "600,800";

        var result = controller.Get("kindle-missing");

        result.Should().BeOfType<NotFoundObjectResult>();
        logService.Received().LogCheckIn("kindle-missing", 80, "600,800", "Unknown", "None", "Error", Arg.Any<string>());
    }

    [Fact]
    public void Get_disabled_device_returns_disable_script_and_skips_home_assistant_update()
    {
        var ha = Substitute.For<IHomeAssistantService>();
        ha.UpdateEntities(Arg.Any<string>(), Arg.Any<int?>()).Returns(Task.CompletedTask);
        var controller = NewController(new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig>
            {
                ["kindle-a"] = new() { ServiceName = "Calendar", ConfigId = "family", Disabled = true }
            }
        }, homeAssistantService: ha);
        controller.Request.Headers["device_id"] = "kindle-a";

        var result = controller.Get("kindle-a");

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.Content.Should().Contain("Device kindle-a is disabled.");
        ha.DidNotReceive().UpdateEntities(Arg.Any<string>(), Arg.Any<int?>());
    }

    [Fact]
    public void Get_configured_device_redirects_to_service_route_and_logs()
    {
        var ha = Substitute.For<IHomeAssistantService>();
        ha.UpdateEntities(Arg.Any<string>(), Arg.Any<int?>()).Returns(Task.CompletedTask);
        var logService = Substitute.For<IPaperframeLogService>();
        var controller = NewController(new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig>
            {
                ["kindle-a"] = new() { ServiceName = "Immich", ConfigId = "frame" }
            }
        }, ha, logService);
        controller.Request.Headers["device_id"] = "kindle-a";
        controller.Request.Headers["battery"] = "95";

        var result = controller.Get("kindle-a");

        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be("/immich/frame");
        ha.Received().UpdateEntities("kindle-a", 95);
        logService.Received().LogCheckIn("kindle-a", 95, "unknown", "Immich", "frame", "Redirect", Arg.Any<string>());
    }

    private static MainController NewController(
        AppSettings settings,
        IHomeAssistantService? homeAssistantService = null,
        IPaperframeLogService? logService = null,
        string? contentRoot = null)
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(contentRoot ?? Directory.CreateTempSubdirectory("paperframe-root-test-").FullName);

        var controller = new MainController(
            new TestOptionsSnapshot<AppSettings>(settings),
            homeAssistantService ?? Substitute.For<IHomeAssistantService>(),
            logService ?? Substitute.For<IPaperframeLogService>(),
            env);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }
}

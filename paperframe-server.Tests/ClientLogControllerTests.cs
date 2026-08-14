using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using paperframe_server.Controllers;
using paperframe_server.Services;

namespace paperframe_server.Tests;

public class ClientLogControllerTests
{
    private readonly IPaperframeLogService _logService = Substitute.For<IPaperframeLogService>();

    [Fact]
    public void ReportError_records_the_failure_with_the_identity_the_device_reported()
    {
        var controller = NewController();
        controller.Request.Headers["device_id"] = "kindle-a";
        controller.Request.Headers["battery"] = "17";
        controller.Request.Headers["screen_res"] = "600,800";
        controller.Request.Headers["script_version"] = "1.1";

        var result = controller.ReportError("script_failed_1_fbink not found");

        result.Should().BeOfType<OkResult>();
        _logService.Received(1).LogCheckIn(Arg.Is<CheckInRequest>(r =>
            r.DeviceId == "kindle-a" &&
            r.Battery == 17 &&
            r.ScreenResolution == "600,800" &&
            r.ScriptVersion == "1.1" &&
            r.Service == "Client" &&
            r.Status == "Error" &&
            r.Message == "script_failed_1_fbink not found"));
    }

    [Fact]
    public void ReportError_rejects_a_request_without_a_device_id()
    {
        var controller = NewController();

        controller.ReportError("anything").Should().BeOfType<BadRequestObjectResult>();
        _logService.DidNotReceive().LogCheckIn(Arg.Any<CheckInRequest>());
    }

    [Fact]
    public void ReportError_truncates_an_oversized_message()
    {
        var controller = NewController();
        controller.Request.Headers["device_id"] = "kindle-a";

        controller.ReportError(new string('x', 5000));

        _logService.Received(1).LogCheckIn(Arg.Is<CheckInRequest>(r => r.Message.Length == 200));
    }

    [Fact]
    public void ReportError_substitutes_a_message_when_the_device_sent_none()
    {
        var controller = NewController();
        controller.Request.Headers["device_id"] = "kindle-a";

        controller.ReportError(null);

        _logService.Received(1).LogCheckIn(Arg.Is<CheckInRequest>(r => r.Message.Length > 0));
    }

    private ClientLogController NewController() => new(_logService)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };
}

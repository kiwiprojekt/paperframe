using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using paperframe_server.Filters;
using paperframe_server.Services;

namespace paperframe_server.Tests;

public class DeviceCheckInAttributeTests
{
    private readonly IPaperframeLogService _logService = Substitute.For<IPaperframeLogService>();
    private readonly DeviceCheckInAttribute _attribute = new();
    private readonly DefaultHttpContext _httpContext = new();

    public DeviceCheckInAttributeTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_logService);
        _httpContext.RequestServices = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public async Task Returns_bad_request_when_device_id_is_missing()
    {
        var context = CreateContext();

        await _attribute.OnActionExecutionAsync(context, () => Task.FromResult(Executed(context)));

        context.Result.Should().BeOfType<BadRequestObjectResult>();
        _logService.DidNotReceive().LogCheckIn(Arg.Any<CheckInRequest>());
    }

    [Fact]
    public async Task Logs_a_successful_check_in_with_what_the_device_reported()
    {
        _httpContext.Request.Headers["device_id"] = "test-device";
        _httpContext.Request.Headers["battery"] = "90";
        _httpContext.Request.Headers["screen_res"] = "758,1024";
        _httpContext.Request.Headers["script_version"] = "1.2";

        var context = CreateContext(new RouteData(new RouteValueDictionary
        {
            { "controller", "TestController" }, { "configId", "my-config" }
        }));

        await _attribute.OnActionExecutionAsync(context, () => Task.FromResult(Executed(context)));

        _logService.Received(1).LogCheckIn(Arg.Is<CheckInRequest>(r =>
            r.DeviceId == "test-device" && r.Battery == 90 && r.ScreenResolution == "758,1024" &&
            r.ScriptVersion == "1.2" && r.Service == "TestController" && r.ConfigId == "my-config" &&
            r.Status == "Success"));
    }

    [Fact]
    public async Task Logs_the_failure_and_lets_it_propagate_rather_than_answering_with_a_script()
    {
        // The device cannot display a diagnostic usefully, and any body it receives is
        // executed as shell. Failing the request is what makes the launcher report and stop.
        _httpContext.Request.Headers["device_id"] = "test-device";

        var context = CreateContext(new RouteData(new RouteValueDictionary
        {
            { "controller", "Immich" }, { "configId", "family" }
        }));

        ActionExecutedContext? resultContext = null;
        Task<ActionExecutedContext> Next()
        {
            resultContext = Executed(context);
            resultContext.Exception = new Exception("Database failed!");
            return Task.FromResult(resultContext);
        }

        await _attribute.OnActionExecutionAsync(context, Next);

        _logService.Received(1).LogCheckIn(Arg.Is<CheckInRequest>(r =>
            r.DeviceId == "test-device" && r.Service == "Immich" && r.ConfigId == "family" &&
            r.Status == "Error" && r.Message == "Layout compile failed: Database failed!"));

        resultContext!.ExceptionHandled.Should().BeFalse();
        resultContext.Result.Should().BeNull();
    }

    private static ActionExecutedContext Executed(ActionExecutingContext context) =>
        new(context, new List<IFilterMetadata>(), controller: null!);

    private ActionExecutingContext CreateContext(RouteData? routeData = null)
    {
        var actionContext = new ActionContext(_httpContext, routeData ?? new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: null!);
    }
}

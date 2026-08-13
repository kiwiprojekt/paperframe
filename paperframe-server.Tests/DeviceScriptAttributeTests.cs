using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using paperframe_server.Filters;
using paperframe_server.Services;

namespace paperframe_server.Tests;

public class DeviceScriptAttributeTests
{
    private readonly IPaperframeLogService _logService = Substitute.For<IPaperframeLogService>();
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor = Substitute.For<IOptionsMonitor<AppSettings>>();
    private readonly DeviceScriptAttribute _attribute = new DeviceScriptAttribute();
    private readonly DefaultHttpContext _httpContext = new DefaultHttpContext();

    public DeviceScriptAttributeTests()
    {
        _optionsMonitor.CurrentValue.Returns(new AppSettings());

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_logService);
        serviceCollection.AddSingleton(_optionsMonitor);
        _httpContext.RequestServices = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ReturnsBadRequest_WhenDeviceIdMissing()
    {
        var context = CreateContext();
        ActionExecutionDelegate next = () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));

        await _attribute.OnActionExecutionAsync(context, next);

        context.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_AddsSleepTimeHeader_AndLogsSuccess()
    {
        _optionsMonitor.CurrentValue.Returns(new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig>
            {
                ["test-device"] = new() { WakeupCron = "*/5 * * * *" }
            }
        });

        _httpContext.Request.Headers["device_id"] = "test-device";
        _httpContext.Request.Headers["battery"] = "90";
        _httpContext.Request.Headers["screen_res"] = "758,1024";

        var context = CreateContext(new RouteData(new RouteValueDictionary { { "controller", "TestController" }, { "configId", "my-config" } }));
        ActionExecutionDelegate next = () => Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));

        await _attribute.OnActionExecutionAsync(context, next);

        _httpContext.Response.Headers.Should().ContainKey("X-Sleep-Time");
        _logService.Received(1).LogCheckIn(Arg.Is<CheckInRequest>(r => 
            r.DeviceId == "test-device" && r.Battery == 90 && r.ScreenResolution == "758,1024" && 
            r.Service == "TestController" && r.ConfigId == "my-config" && r.Status == "Success"));
    }

    [Fact]
    public async Task OnActionExecutionAsync_ReturnsFallbackScript_WhenExceptionOccurs()
    {
        _httpContext.Request.Headers["device_id"] = "test-device";

        var context = CreateContext(new RouteData(new RouteValueDictionary { { "controller", "Immich" }, { "configId", "family" } }));
        ActionExecutedContext? resultContext = null;
        ActionExecutionDelegate next = () =>
        {
            resultContext = new ActionExecutedContext(context, new List<IFilterMetadata>(), null)
            {
                Exception = new Exception("Database failed!")
            };
            return Task.FromResult(resultContext);
        };

        await _attribute.OnActionExecutionAsync(context, next);

        _logService.Received(1).LogCheckIn(Arg.Is<CheckInRequest>(r => 
            r.DeviceId == "test-device" && r.Battery == null && r.ScreenResolution == "758,1024" && 
            r.Service == "Immich" && r.ConfigId == "family" && r.Status == "Error" && 
            r.Message == "Layout compile failed: Database failed!"));

        var contentResult = resultContext!.Result.Should().BeOfType<ContentResult>().Subject;
        contentResult.StatusCode.Should().Be(200);
        contentResult.Content.Should().Contain("IMMICH COMPILE ERROR");
        contentResult.Content.Should().Contain("Error: Database failed");
    }

    private ActionExecutingContext CreateContext(RouteData? routeData = null)
    {
        var actionContext = new ActionContext(_httpContext, routeData ?? new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), null);
    }
}

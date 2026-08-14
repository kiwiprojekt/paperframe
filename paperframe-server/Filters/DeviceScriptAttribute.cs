using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using paperframe_server.Services;
using paperframe_server.Helpers;

namespace paperframe_server.Filters;

/// <summary>
/// A device-facing endpoint that returns a shell script. On top of the identity check
/// it tells the device when to wake next, records the check-in, and turns a layout
/// compile failure into an on-screen diagnostic rather than an HTTP error the device
/// has no way to display.
/// </summary>
public sealed class DeviceScriptAttribute : IdentifiedDeviceAttribute
{
    protected override async Task OnIdentifiedAsync(
        DeviceRequest device, ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var options = httpContext.RequestServices.GetRequiredService<IOptionsMonitor<AppSettings>>();
        var logService = httpContext.RequestServices.GetRequiredService<IPaperframeLogService>();

        httpContext.Response.Headers[ClientProtocol.SleepHeader] =
            WakeupSchedule.SecondsUntilNextWake(device.DeviceId, options.CurrentValue).ToString();

        var serviceName = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
        var configId = context.RouteData.Values["configId"] as string ?? "None";

        var resultContext = await next();
        var failure = resultContext.ExceptionHandled ? null : resultContext.Exception;

        logService.LogCheckIn(CheckInRequest.From(
            device,
            serviceName,
            configId,
            failure == null ? "Success" : "Error",
            failure == null
                ? $"{serviceName} rendering script generated successfully."
                : $"Layout compile failed: {failure.Message}"));

        if (failure == null)
        {
            return;
        }

        resultContext.Result = new ContentResult
        {
            Content = ShellScript.Render(ShellScript.CompileError,
                ShellScript.Text("SERVICE", serviceName.ToUpperInvariant()),
                ShellScript.Text("CONFIG_ID", configId),
                ShellScript.Text("ERROR", failure.Message)),
            ContentType = "text/plain",
            StatusCode = StatusCodes.Status200OK
        };
        resultContext.ExceptionHandled = true;
    }
}

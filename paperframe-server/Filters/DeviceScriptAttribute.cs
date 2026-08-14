using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using paperframe_server.Services;
using paperframe_server.Helpers;

namespace paperframe_server.Filters;

/// <summary>
/// Wraps a device-facing rendering endpoint: identifies the caller, tells it when to
/// wake next, records the check-in, and turns a layout compile failure into an
/// on-screen diagnostic rather than an HTTP error the device cannot display.
/// </summary>
public class DeviceScriptAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var device = DeviceRequestReader.Read(httpContext.Request.Headers);

        if (!device.IsIdentified)
        {
            context.Result = new BadRequestObjectResult(DeviceRequestReader.MissingDeviceIdMessage);
            return;
        }

        var options = httpContext.RequestServices.GetRequiredService<IOptionsMonitor<AppSettings>>();
        var logService = httpContext.RequestServices.GetRequiredService<IPaperframeLogService>();

        httpContext.Response.Headers["X-Sleep-Time"] =
            DeviceHelper.GetSleepTimeSeconds(device.DeviceId, options.CurrentValue).ToString();

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
                ("SERVICE", serviceName.ToUpperInvariant()),
                ("CONFIG_ID", configId),
                ("ERROR", failure.Message)),
            ContentType = "text/plain",
            StatusCode = StatusCodes.Status200OK
        };
        resultContext.ExceptionHandled = true;
    }
}

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using paperframe_server.Helpers;
using paperframe_server.Services;

namespace paperframe_server.Filters;

/// <summary>
/// A device-facing endpoint whose every visit is a check-in worth recording.
///
/// A failure is logged and then left to propagate: the device has no way to display a
/// diagnostic usefully, and an error page would only be executed as a shell script. The
/// launcher reports the failed download to <c>/client/error</c> and stops, which keeps
/// the record on the server where it can actually be read.
/// </summary>
public sealed class DeviceCheckInAttribute : IdentifiedDeviceAttribute
{
    protected override async Task OnIdentifiedAsync(
        DeviceRequest device, ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var logService = context.HttpContext.RequestServices.GetRequiredService<IPaperframeLogService>();

        var service = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
        var configId = context.RouteData.Values["configId"] as string ?? "None";

        var executed = await next();
        var failure = executed.ExceptionHandled ? null : executed.Exception;

        logService.LogCheckIn(CheckInRequest.From(
            device,
            service,
            configId,
            failure == null ? "Success" : "Error",
            failure == null
                ? $"{service} rendering script generated successfully."
                : $"Layout compile failed: {failure.Message}"));
    }
}

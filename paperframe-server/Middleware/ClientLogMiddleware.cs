using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using paperframe_server.Helpers;
using paperframe_server.Services;

namespace paperframe_server.Middleware;

/// <summary>
/// Records whatever a device managed to say about itself on the way in.
///
/// A device attaches its undelivered log to whichever request it happens to make next,
/// which may be a check-in, an image fetch, or a failure report. Draining it here rather
/// than in each of those routes means none of them has to know the log exists, and the
/// lines land before the action writes its own entry, so a failure reads in the order it
/// happened.
/// </summary>
public sealed class ClientLogMiddleware
{
    private readonly RequestDelegate _next;

    public ClientLogMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context, IPaperframeLogService logService)
    {
        var device = context.Device();

        if (device.IsIdentified && device.ClientLog.Count > 0)
        {
            logService.LogDeviceDiagnostics(device);
        }

        return _next(context);
    }
}

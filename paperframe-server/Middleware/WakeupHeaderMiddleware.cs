using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using paperframe_server.Helpers;

namespace paperframe_server.Middleware;

/// <summary>
/// Stamps every response to an identified device with the seconds until its next wake.
///
/// The launcher refuses to sleep without this header, so carrying it cannot be the
/// responsibility of any one route: a redirect, a disable, and a rendering script all
/// have to answer with it. Deciding it here means no endpoint can forget.
/// </summary>
public sealed class WakeupHeaderMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<AppSettings> _options;

    public WakeupHeaderMiddleware(RequestDelegate next, IOptionsMonitor<AppSettings> options)
    {
        _next = next;
        _options = options;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var device = context.Device();

        if (device.IsIdentified)
        {
            context.Response.Headers[ClientProtocol.SleepHeader] =
                WakeupSchedule.SecondsUntilNextWake(device.DeviceId, _options.CurrentValue).ToString();
        }

        return _next(context);
    }
}

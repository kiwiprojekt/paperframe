using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using paperframe_server.Services;
using paperframe_server.Helpers;

namespace paperframe_server.Filters;

public class DeviceScriptAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var device = DeviceRequestReader.Read(httpContext.Request.Headers);

        if (device.DeviceId == DeviceRequestReader.UnknownDeviceId)
        {
            context.Result = new BadRequestObjectResult("Missing 'device_id' header. Make sure the Paperframe client sends a valid device identifier.");
            return;
        }

        var options = httpContext.RequestServices.GetRequiredService<IOptionsMonitor<AppSettings>>();
        httpContext.Response.Headers["X-Sleep-Time"] = DeviceHelper.GetSleepTimeSeconds(device.DeviceId, options.CurrentValue).ToString();

        var serviceName = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
        var configId = context.RouteData.Values["configId"] as string ?? "None";

        var logService = httpContext.RequestServices.GetRequiredService<IPaperframeLogService>();

        var resultContext = await next();

        if (resultContext.Exception != null && !resultContext.ExceptionHandled)
        {
            logService.LogCheckIn(new CheckInRequest(
                DeviceId: device.DeviceId,
                Battery: device.Battery,
                ScreenResolution: device.ScreenResolution,
                Service: serviceName,
                ConfigId: configId,
                Status: "Error",
                Message: $"Layout compile failed: {resultContext.Exception.Message}",
                ScriptVersion: device.ScriptVersion));

            resultContext.Result = new ContentResult
            {
                Content = BuildFallbackScript(serviceName, configId, resultContext.Exception.Message),
                ContentType = "text/plain",
                StatusCode = 200
            };
            resultContext.ExceptionHandled = true;
        }
        else
        {
            logService.LogCheckIn(new CheckInRequest(
                DeviceId: device.DeviceId,
                Battery: device.Battery,
                ScreenResolution: device.ScreenResolution,
                Service: serviceName,
                ConfigId: configId,
                Status: "Success",
                Message: $"{serviceName} rendering script generated successfully.",
                ScriptVersion: device.ScriptVersion));
        }
    }

    private static string BuildFallbackScript(string serviceName, string configId, string errorMessage)
    {
        var safeServiceName = serviceName.ToUpperInvariant();
        var safeConfigId = ShellEscape(configId);
        var safeEx = ShellEscape(errorMessage);

        return $@"#!/bin/sh
# {safeServiceName} COMPILE ERROR RUNTIME FALLBACK
FBINK=""/mnt/us/libkh/bin/fbink""
$FBINK -q -k
$FBINK -q ""{safeServiceName} COMPILE ERROR"" -t size=20,top=200 -O -m -C GRAY9
$FBINK -q ""Config ID: {safeConfigId}"" -t size=12,top=260 -O -m -C GRAY6
$FBINK -q ""Error: {safeEx}"" -t size=10,top=320 -O -m -C GRAY3
";
    }

    private static string ShellEscape(string input) =>
        Regex.Replace(input ?? "", @"[""\\$`!\r\n\t]", "");
}

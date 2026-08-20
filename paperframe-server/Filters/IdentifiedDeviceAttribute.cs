using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Threading.Tasks;
using paperframe_server.Helpers;

namespace paperframe_server.Filters;

/// <summary>
/// Requires the caller to identify itself with a <c>device_id</c> header, answering 400
/// when it does not. Every device-facing route carries this (directly or via a subclass)
/// so the unidentified-caller contract is stated once rather than reinvented per endpoint.
/// </summary>
public class IdentifiedDeviceAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var device = context.HttpContext.Device();

        if (!device.IsIdentified)
        {
            context.Result = new BadRequestObjectResult(DeviceRequestReader.MissingDeviceIdMessage);
            return;
        }

        await OnIdentifiedAsync(device, context, next);
    }

    /// <summary>Runs once the caller is known. Subclasses layer their own behaviour here.</summary>
    protected virtual Task OnIdentifiedAsync(
        DeviceRequest device, ActionExecutingContext context, ActionExecutionDelegate next) => next();
}

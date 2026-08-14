using Microsoft.AspNetCore.Mvc;
using paperframe_server.Filters;
using paperframe_server.Helpers;
using paperframe_server.Services;

namespace paperframe_server.Controllers;

/// <summary>
/// Device-facing failure reporting. Deliberately routed outside <c>/api</c> alongside
/// the other client routes (<c>/calendar</c>, <c>/immich</c>, <c>/artchicago</c>) —
/// anything under <c>/api</c> requires an admin session cookie that a Kindle does not have.
/// </summary>
[ApiController]
[Route("client")]
public class ClientLogController : ControllerBase
{
    private const int MaxMessageLength = 200;

    private readonly IPaperframeLogService _logService;

    public ClientLogController(IPaperframeLogService logService)
    {
        _logService = logService;
    }

    [HttpGet("error")]
    [IdentifiedDevice]
    public IActionResult ReportError([FromHeader(Name = "x-client-error")] string? message = null)
    {
        var device = DeviceRequestReader.Read(Request.Headers);

        _logService.LogCheckIn(CheckInRequest.From(
            device, "Client", "None", "Error", Summarize(message)));

        return Ok();
    }

    private static string Summarize(string? message) => string.IsNullOrWhiteSpace(message)
        ? "Client reported an unspecified failure."
        : message.Length <= MaxMessageLength ? message : message[..MaxMessageLength];
}

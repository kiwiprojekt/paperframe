using Microsoft.AspNetCore.Mvc;
using paperframe_server.Services;

namespace paperframe_server.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly IPaperframeLogService _logService;

    public LogsController(IPaperframeLogService logService)
    {
        _logService = logService;
    }

    [HttpGet]
    public IActionResult GetLogs() => Ok(_logService.GetLogs());

    [HttpGet("client_error")]
    public IActionResult LogClientError([FromQuery] string device_id, [FromQuery] string msg)
    {
        _logService.LogCheckIn(device_id, null, "unknown", "Client", "None", "Error", msg);
        return Ok();
    }
}

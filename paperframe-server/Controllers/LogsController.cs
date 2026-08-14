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
}

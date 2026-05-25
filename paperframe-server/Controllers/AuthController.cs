using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace paperframe_server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IOptionsMonitor<AppSettings> _options;
    private readonly AuthService _authService;

    public AuthController(IOptionsMonitor<AppSettings> options, AuthService authService)
    {
        _options = options;
        _authService = authService;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var config = _options.CurrentValue;
        var expectedPassword = config.Settings?.ManagerPassword;

        // If no password configured, auth is disabled — always allow
        if (string.IsNullOrEmpty(expectedPassword))
        {
            var freeToken = _authService.CreateSession();
            AppendSessionCookie(freeToken);
            return Ok(new { success = true, message = "No password configured." });
        }

        if (request?.Password != expectedPassword)
            return Unauthorized(new { message = "Incorrect password." });

        var token = _authService.CreateSession();
        AppendSessionCookie(token);
        return Ok(new { success = true });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var token = Request.Cookies["paperframe_session"];
        _authService.RevokeSession(token);
        Response.Cookies.Delete("paperframe_session");
        return Ok(new { success = true });
    }

    [HttpGet("check")]
    public IActionResult Check()
    {
        var config = _options.CurrentValue;
        var password = config.Settings?.ManagerPassword;
        var authEnabled = !string.IsNullOrEmpty(password);

        if (!authEnabled)
            return Ok(new { authenticated = true, authEnabled = false });

        var token = Request.Cookies["paperframe_session"];
        if (_authService.ValidateSession(token))
            return Ok(new { authenticated = true, authEnabled = true });

        return Unauthorized(new { authenticated = false, authEnabled = true });
    }

    private void AppendSessionCookie(string token)
    {
        Response.Cookies.Append("paperframe_session", token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Expires  = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}

public class LoginRequest
{
    public string? Password { get; set; }
}

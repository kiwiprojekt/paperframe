using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using paperframe_server.Controllers;
using paperframe_server.Tests.TestSupport;

namespace paperframe_server.Tests;

public class AuthControllerTests
{
    [Fact]
    public void Login_allows_any_password_when_manager_password_is_empty()
    {
        using var authService = new AuthService();
        var controller = NewController(authService, managerPassword: "");

        var result = controller.Login(new LoginRequest { Password = "anything" });

        result.Should().BeOfType<OkObjectResult>();
        controller.Response.Headers.SetCookie.ToString().Should().Contain("paperframe_session=");
    }

    [Fact]
    public void Login_rejects_wrong_password()
    {
        using var authService = new AuthService();
        var controller = NewController(authService, managerPassword: "correct");

        var result = controller.Login(new LoginRequest { Password = "wrong" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
        controller.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public void Check_requires_valid_cookie_when_auth_is_enabled()
    {
        using var authService = new AuthService();
        var token = authService.CreateSession();
        var controller = NewController(authService, managerPassword: "correct");
        controller.Request.Headers.Cookie = $"paperframe_session={token}";

        var result = controller.Check();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void Logout_revokes_cookie_session()
    {
        using var authService = new AuthService();
        var token = authService.CreateSession();
        var controller = NewController(authService, managerPassword: "correct");
        controller.Request.Headers.Cookie = $"paperframe_session={token}";

        var result = controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
        authService.ValidateSession(token).Should().BeFalse();
        controller.Response.Headers.SetCookie.ToString().Should().Contain("paperframe_session=");
    }

    private static AuthController NewController(AuthService authService, string? managerPassword)
    {
        var controller = new AuthController(
            new TestOptionsMonitor<AppSettings>(new AppSettings
            {
                Settings = new AppSettings.SettingsConfig { ManagerPassword = managerPassword }
            }),
            authService);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }
}

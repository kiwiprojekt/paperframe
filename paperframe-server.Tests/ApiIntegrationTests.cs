using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using paperframe_server.Tests.TestSupport;

namespace paperframe_server.Tests;

public class ApiIntegrationTests
{
    [Fact]
    public async Task Protected_api_returns_401_when_manager_password_is_configured_and_cookie_is_missing()
    {
        using var factory = new PaperframeWebApplicationFactory(AppSettingsJson(managerPassword: "secret"));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/config");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task Login_cookie_allows_protected_api_access()
    {
        using var factory = new PaperframeWebApplicationFactory(AppSettingsJson(managerPassword: "secret"));
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { password = "secret" });
        var cookie = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        var config = await client.GetAsync("/api/config");

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        config.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Root_without_device_id_serves_manager_html()
    {
        using var factory = new PaperframeWebApplicationFactory(AppSettingsJson(managerPassword: ""));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        html.Should().Contain("Paperframe Manager");
    }

    [Fact]
    public async Task Client_error_reporting_stays_reachable_when_a_manager_password_is_configured()
    {
        // Devices have no session cookie. Routing this under /api would 401 every
        // report and silently disable client error reporting on secured servers.
        using var factory = new PaperframeWebApplicationFactory(AppSettingsJson(managerPassword: "secret"));
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/client/error");
        request.Headers.Add("device_id", "kindle-a");
        request.Headers.Add("x-client-error", "wget_failed_4");
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Client_error_reporting_rejects_an_unidentified_caller()
    {
        using var factory = new PaperframeWebApplicationFactory(AppSettingsJson(managerPassword: "secret"));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/client/error");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string AppSettingsJson(string managerPassword) => $$"""
        {
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "AllowedHosts": "*",
          "Configuration": {
            "Devices": {},
            "Calendar": {},
            "Immich": {},
            "HomeAssistant": {
              "ApiUrl": "",
              "OAuthBearerToken": ""
            },
            "Settings": {
              "ServerAddress": "",
              "EnableCalendar": true,
              "EnableImmich": true,
              "EnableHomeAssistant": true,
              "ManagerPassword": "{{managerPassword}}"
            }
          }
        }
        """;
}

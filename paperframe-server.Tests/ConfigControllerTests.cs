using System.Text;
using System.Text.Json;
using FluentAssertions;
using Flurl.Http.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using paperframe_server.Controllers;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;

namespace paperframe_server.Tests;

public class ConfigControllerTests
{
    [Fact]
    public void SaveConfig_updates_only_configuration_section()
    {
        var tempDir = Directory.CreateTempSubdirectory("paperframe-config-test-");
        var configPath = Path.Combine(tempDir.FullName, "appsettings.json");
        File.WriteAllText(configPath, """
            {
              "Logging": { "LogLevel": { "Default": "Information" } },
              "Configuration": { "Settings": { "ServerAddress": "old" } }
            }
            """);
        var controller = NewController(configPath, new AppSettings());

        var result = controller.SaveConfig(new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig>
            {
                ["kindle-a"] = new() { ServiceName = "Calendar", ConfigId = "family" }
            },
            Settings = new AppSettings.SettingsConfig { ServerAddress = "http://paperframe.local" }
        });

        result.Should().BeOfType<OkObjectResult>();
        var json = JsonDocument.Parse(File.ReadAllText(configPath)).RootElement;
        json.GetProperty("Logging").GetProperty("LogLevel").GetProperty("Default").GetString().Should().Be("Information");
        json.GetProperty("Configuration").GetProperty("Settings").GetProperty("ServerAddress").GetString().Should().Be("http://paperframe.local");
        json.GetProperty("Configuration").GetProperty("Devices").GetProperty("kindle-a").GetProperty("ServiceName").GetString().Should().Be("Calendar");
    }

    [Fact]
    public async Task ValidateCalendar_rejects_invalid_culture_before_fetching_url()
    {
        using var httpTest = new HttpTest();
        var controller = NewController();

        var result = await controller.ValidateCalendar(new AppSettings.CalendarConfig
        {
            CultureInfoName = null,
            TimeZoneId = "UTC",
            IcalUrls = new[] { "https://calendar.example/feed.ics" }
        });

        var payload = Payload(result);
        payload.GetProperty("success").GetBoolean().Should().BeFalse();
        payload.GetProperty("message").GetString().Should().Contain("Invalid culture");
        httpTest.CallLog.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateCalendar_accepts_valid_http_ical_feed()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWith("BEGIN:VCALENDAR\nVERSION:2.0\nEND:VCALENDAR", 200);
        var controller = NewController();

        var result = await controller.ValidateCalendar(new AppSettings.CalendarConfig
        {
            CultureInfoName = "en-US",
            TimeZoneId = "UTC",
            IcalUrls = new[] { "https://calendar.example/feed.ics" }
        });

        var payload = Payload(result);
        payload.GetProperty("success").GetBoolean().Should().BeTrue();
        httpTest.ShouldHaveCalled("https://calendar.example/feed.ics").Times(1);
    }

    [Fact]
    public async Task ValidateImmich_reports_missing_album_with_available_names()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWithJson(new[]
        {
            new { albumName = "Trips", id = "album-1", assetCount = 12 },
            new { albumName = "Family", id = "album-2", assetCount = 4 }
        });
        var controller = NewController();

        var result = await controller.ValidateImmich(new AppSettings.ImmichConfig
        {
            ApiUrl = "http://immich.local/api",
            ApiKey = "key",
            AlbumName = "Frame"
        });

        var payload = Payload(result);
        payload.GetProperty("success").GetBoolean().Should().BeFalse();
        payload.GetProperty("message").GetString().Should().Contain("Available albums: 'Trips', 'Family'");
    }

    [Fact]
    public async Task GetImmichAlbums_returns_album_names_and_counts()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWithJson(new[]
        {
            new { albumName = "Frame", id = "album-1", assetCount = 12 }
        });
        var controller = NewController();

        var result = await controller.GetImmichAlbums(new ImmichAlbumsRequest
        {
            ApiUrl = "http://immich.local/api/",
            ApiKey = "key"
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = JsonAssertions.ToJsonElement(ok.Value!);
        payload.EnumerateArray().Single().GetProperty("name").GetString().Should().Be("Frame");
        httpTest.ShouldHaveCalled("http://immich.local/api/albums")
            .WithHeader("x-api-key", "key")
            .Times(1);
    }

    [Fact]
    public void DownloadClientScript_requires_configured_device()
    {
        var controller = NewController(options: new AppSettings { Devices = new() });

        var result = controller.DownloadClientScript("missing-device");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void DownloadClientScript_uses_request_origin_device_id_and_sleep_seconds()
    {
        var controller = NewController(options: new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig>
            {
                ["kindle-a"] = new() { ServiceName = "Calendar", ConfigId = "family" }
            }
        });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
                {
                    Scheme = "https",
                    Host = new HostString("paperframe.local", 8443)
                }
            }
        };

        var result = controller.DownloadClientScript("kindle-a", sleepSeconds: 123);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        var script = Encoding.UTF8.GetString(file.FileContents);
        script.Should().Contain("DEVICE_ID=\"kindle-a\"");
        script.Should().Contain("SLEEP_TIME_S=123");
        script.Should().Contain("SERVICES_URL=\"https://paperframe.local:8443\"");
    }

    private static ConfigController NewController(string? configPath = null, AppSettings? options = null)
    {
        var path = configPath ?? Path.Combine(Directory.CreateTempSubdirectory("paperframe-config-test-").FullName, "appsettings.json");
        return new ConfigController(
            new ConfigFilePointer(path),
            new TestOptionsMonitor<AppSettings>(options ?? new AppSettings()),
            Substitute.For<IPaperframeLogService>());
    }

    private static JsonElement Payload(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return JsonAssertions.ToJsonElement(ok.Value!);
    }
}

using FluentAssertions;
using Flurl.Http.Testing;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;

namespace paperframe_server.Tests;

public class HomeAssistantServiceTests
{
    [Fact]
    public async Task UpdateEntities_posts_update_and_battery_for_known_device()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWithJson(new { ok = true });
        httpTest.RespondWithJson(new { ok = true });

        var service = new HomeAssistantService(new TestOptionsMonitor<AppSettings>(new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig> { ["kindle-a"] = new() },
            HomeAssistant = new AppSettings.HomeAssistantConfig
            {
                ApiUrl = "http://ha.local/api",
                OAuthBearerToken = "secret-token"
            }
        }));

        await service.UpdateEntities("kindle-a", 87);

        httpTest.ShouldHaveCalled("http://ha.local/api/states/input_datetime.paperframe_kindle-a_update")
            .WithVerb(HttpMethod.Post)
            .WithHeader("Authorization", "Bearer secret-token")
            .Times(1);

        httpTest.ShouldHaveCalled("http://ha.local/api/states/input_number.paperframe_kindle-a_battery")
            .WithVerb(HttpMethod.Post)
            .WithRequestJson(new { state = 87 })
            .Times(1);
    }

    [Fact]
    public async Task UpdateEntities_honors_custom_templates_and_disabled_entities()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWithJson(new { ok = true });

        var service = new HomeAssistantService(new TestOptionsMonitor<AppSettings>(new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig> { ["kindle-a"] = new() },
            HomeAssistant = new AppSettings.HomeAssistantConfig
            {
                ApiUrl = "http://ha.local/api/",
                OAuthBearerToken = "secret-token",
                BatteryEntityTemplate = "sensor.{deviceId}.battery",
                DisableUpdateEntity = true
            }
        }));

        await service.UpdateEntities("kindle-a", 55);

        httpTest.ShouldHaveCalled("http://ha.local/api/states/sensor.kindle-a.battery")
            .WithRequestJson(new { state = 55 })
            .Times(1);
        httpTest.CallLog.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateEntities_skips_unknown_or_empty_devices()
    {
        using var httpTest = new HttpTest();
        var service = new HomeAssistantService(new TestOptionsMonitor<AppSettings>(new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig> { ["kindle-a"] = new() },
            HomeAssistant = new AppSettings.HomeAssistantConfig { ApiUrl = "http://ha.local/api" }
        }));

        await service.UpdateEntities("kindle-b", 90);
        await service.UpdateEntities("", 90);

        httpTest.CallLog.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateEntities_swallows_home_assistant_http_failures()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWith("error", 500);

        var service = new HomeAssistantService(new TestOptionsMonitor<AppSettings>(new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig> { ["kindle-a"] = new() },
            HomeAssistant = new AppSettings.HomeAssistantConfig { ApiUrl = "http://ha.local/api" }
        }));

        var act = () => service.UpdateEntities("kindle-a", null);

        await act.Should().NotThrowAsync();
    }
}

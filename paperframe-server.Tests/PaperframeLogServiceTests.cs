using FluentAssertions;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;

namespace paperframe_server.Tests;

public class PaperframeLogServiceTests
{
    [Fact]
    public void LogCheckIn_returns_newest_logs_first_and_keeps_latest_device_status()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 5, 25, 10, 0, 0, TimeSpan.Zero));
        var service = NewService(clock, "kindle-a");

        service.LogCheckIn(new CheckInRequest("kindle-a", "Calendar", "main", "Redirect", "first", "758,1024", "1.1", Battery: 42));
        clock.Advance(TimeSpan.FromMinutes(5));
        service.LogCheckIn(new CheckInRequest("kindle-a", "Calendar", "main", "Success", "second", "758,1024", "1.2", Battery: null));

        var logs = service.GetLogs();
        logs.Select(l => l.Message).Should().Equal("second", "first");

        var status = service.GetDeviceStatuses()["kindle-a"];
        status.Status.Should().Be("Success");
        status.Battery.Should().Be(42);
        status.ScriptVersion.Should().Be("1.2");
        status.LastUpdate.Should().Be(new DateTime(2026, 5, 25, 10, 5, 0));
    }

    [Fact]
    public void LogCheckIn_trims_oldest_entries_after_maximum()
    {
        var service = NewService();

        for (var i = 0; i < 105; i++)
        {
            service.LogCheckIn(new CheckInRequest($"kindle-{i}", "Calendar", "main", "Success", $"entry-{i}", "758,1024", "1.0", Battery: i));
        }

        var logs = service.GetLogs();

        logs.Should().HaveCount(100);
        logs.Should().NotContain(l => l.Message == "entry-0");
        logs.Should().Contain(l => l.Message == "entry-104");
    }

    [Fact]
    public void LogCheckIn_logs_unconfigured_devices_without_tracking_their_status()
    {
        var service = NewService(configuredDeviceIds: "kindle-a");

        service.LogCheckIn(new CheckInRequest("kindle-a", "Calendar", "main", "Success", "known", "758,1024", "1.1"));
        service.LogCheckIn(new CheckInRequest("intruder", "Client", "None", "Error", "unknown", "758,1024", "1.1"));

        service.GetLogs().Select(l => l.DeviceId).Should().BeEquivalentTo(["kindle-a", "intruder"]);
        service.GetDeviceStatuses().Keys.Should().Equal("kindle-a");
    }

    private static PaperframeLogService NewService(params string[] configuredDeviceIds) =>
        NewService(null, configuredDeviceIds);

    private static PaperframeLogService NewService(TimeProvider? clock, params string[] configuredDeviceIds)
    {
        var settings = new AppSettings
        {
            Devices = configuredDeviceIds.ToDictionary(id => id, _ => new AppSettings.DeviceConfig())
        };

        return new PaperframeLogService(new TestOptionsMonitor<AppSettings>(settings), clock);
    }
}

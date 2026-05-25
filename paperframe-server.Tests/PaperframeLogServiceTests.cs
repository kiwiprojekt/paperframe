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
        var service = new PaperframeLogService(clock);

        service.LogCheckIn("kindle-a", 42, "758,1024", "Calendar", "main", "Redirect", "first");
        clock.Advance(TimeSpan.FromMinutes(5));
        service.LogCheckIn("kindle-a", null, "758,1024", "Calendar", "main", "Success", "second");

        var logs = service.GetLogs();
        logs.Select(l => l.Message).Should().Equal("second", "first");

        var status = service.GetDeviceStatuses()["kindle-a"];
        status.Status.Should().Be("Success");
        status.Battery.Should().Be(42);
        status.LastUpdate.Should().Be(new DateTime(2026, 5, 25, 10, 5, 0));
    }

    [Fact]
    public void LogCheckIn_trims_oldest_entries_after_maximum()
    {
        var service = new PaperframeLogService();

        for (var i = 0; i < 105; i++)
        {
            service.LogCheckIn($"kindle-{i}", i, "758,1024", "Calendar", "main", "Success", $"entry-{i}");
        }

        var logs = service.GetLogs();

        logs.Should().HaveCount(100);
        logs.Should().NotContain(l => l.Message == "entry-0");
        logs.Should().Contain(l => l.Message == "entry-104");
    }
}

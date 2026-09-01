using FluentAssertions;
using Microsoft.AspNetCore.Http;
using paperframe_server;
using paperframe_server.Helpers;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;

namespace paperframe_server.Tests;

/// <summary>How a device's own log reaches the server and what happens to it there.</summary>
public class ClientLogDeliveryTests
{
    [Fact]
    public void Log_lines_arrive_flattened_and_are_split_back_apart()
    {
        var device = Read("2026-08-23T10:00:00 NET wifi connected after 4s"
            + "~2026-08-23T10:00:05 EXIT wget_failed_4 (code 1)");

        device.ClientLog.Should().Equal(
            "2026-08-23T10:00:00 NET wifi connected after 4s",
            "2026-08-23T10:00:05 EXIT wget_failed_4 (code 1)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("~~~")]
    public void Nothing_to_say_is_not_a_log_line(string header)
    {
        Read(header).ClientLog.Should().BeEmpty();
    }

    [Fact]
    public void A_client_ignoring_the_size_limit_cannot_spend_the_servers_memory()
    {
        var flood = string.Join('~', Enumerable.Range(0, 5000).Select(i => $"line {i}"));

        Read(flood).ClientLog.Should().HaveCount(DeviceRequestReader.MaxClientLogLines);
    }

    [Fact]
    public void Delivered_lines_are_recorded_without_overwriting_what_the_device_status_says()
    {
        var settings = new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig> { ["kindle-a"] = new() }
        };
        var service = new PaperframeLogService(new TestOptionsMonitor<AppSettings>(settings), TestSupport.TestLog.Sink());
        var device = Read("NET wifi connected after 4s", deviceId: "kindle-a");

        service.LogCheckIn(CheckInRequest.From(device, "Calendar", "family", "Redirect", "served"));
        service.LogDeviceDiagnostics(device);

        // The device's own history is history: the status stays whatever the check-in said.
        service.GetDeviceStatuses()["kindle-a"].Status.Should().Be("Redirect");
        service.GetLogs().Should().ContainSingle(l => l.Message == "NET wifi connected after 4s")
            .Which.Status.Should().Be(PaperframeLogService.DeviceStatusName);
    }

    [Fact]
    public void The_durable_log_keeps_the_newest_half_when_it_outgrows_its_ceiling()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("paperframe-logfile-").FullName, "paperframe.log");
        var file = new PaperframeLogFile(new LogFilePointer(path));

        File.WriteAllText(path, string.Concat(Enumerable.Repeat($"old{new string('x', 200)}\n", 30_000)));
        file.Append(new PaperframeLogEntry { DeviceId = "kindle-a", Message = "newest" });

        var lines = File.ReadAllLines(path);
        lines.Length.Should().BeLessThan(30_000);
        lines[^1].Should().Contain("newest");
    }

    [Fact]
    public void A_message_with_newlines_cannot_forge_extra_rows_in_the_durable_log()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("paperframe-logfile-").FullName, "paperframe.log");

        new PaperframeLogFile(new LogFilePointer(path))
            .Append(new PaperframeLogEntry { DeviceId = "kindle-a", Message = "first\nsecond\rthird" });

        File.ReadAllLines(path).Should().ContainSingle().Which.Should().Contain("first second third");
    }

    [Fact]
    public void The_same_delivery_repeated_on_a_redirect_is_recorded_once()
    {
        // wget repeats its headers when it follows the server's redirect, so the same lines
        // arrive on both hops while the device clears its outbox only once.
        var settings = new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig> { ["kindle-a"] = new() }
        };
        var service = new PaperframeLogService(new TestOptionsMonitor<AppSettings>(settings), TestSupport.TestLog.Sink());
        var device = Read("NET wifi connected after 4s", deviceId: "kindle-a");

        service.LogDeviceDiagnostics(device);
        service.LogDeviceDiagnostics(device);

        service.GetLogs().Should().ContainSingle(l => l.Message == "NET wifi connected after 4s");

        // A genuinely new delivery still lands.
        service.LogDeviceDiagnostics(Read("NET wifi connected after 9s", deviceId: "kindle-a"));
        service.GetLogs().Should().HaveCount(2);
    }

    [Fact]
    public void An_over_budget_delivery_keeps_the_lines_nearest_the_failure()
    {
        // The device trims its outbox from the front, so the tail is the part that matters.
        var lines = string.Join('~', Enumerable.Range(0, DeviceRequestReader.MaxClientLogLines + 10)
            .Select(i => $"line {i}"));

        Read(lines).ClientLog.Should().EndWith($"line {DeviceRequestReader.MaxClientLogLines + 9}");
    }

    private static DeviceRequest Read(string clientLog, string deviceId = "kindle-a")
    {
        var headers = new HeaderDictionary
        {
            ["device_id"] = deviceId,
            [DeviceRequestReader.ClientLogHeader] = clientLog
        };

        return DeviceRequestReader.Read(headers);
    }
}

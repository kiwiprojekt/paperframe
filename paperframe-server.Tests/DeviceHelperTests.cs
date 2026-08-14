using FluentAssertions;
using paperframe_server.Helpers;

namespace paperframe_server.Tests;

public class DeviceHelperTests
{
    [Theory]
    [InlineData("Europe/Warsaw")]
    [InlineData("America/New_York")]
    [InlineData("UTC")]
    public void GetSleepTimeSeconds_resolves_cron_in_the_configured_timezone(string timeZoneId)
    {
        // "0 8 * * *" has to mean 8am where the frame hangs. A UTC-naive
        // implementation wakes the device at 8am UTC instead.
        var seconds = DeviceHelper.GetSleepTimeSeconds("kindle-a", ConfigFor("0 8 * * *", timeZoneId));

        var wakeUpAt = TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow.AddSeconds(seconds),
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

        wakeUpAt.Hour.Should().Be(8);
        seconds.Should().BeLessThanOrEqualTo((int)TimeSpan.FromHours(24).TotalSeconds);
    }

    [Fact]
    public void GetSleepTimeSeconds_falls_back_to_default_without_a_schedule()
    {
        var config = ConfigFor(wakeupCron: null, timeZoneId: "Europe/Warsaw");

        DeviceHelper.GetSleepTimeSeconds("kindle-a", config).Should().Be(DeviceHelper.DefaultSleepSeconds);
    }

    [Fact]
    public void GetSleepTimeSeconds_falls_back_to_default_for_an_unknown_device()
    {
        var config = ConfigFor(wakeupCron: "0 * * * *", timeZoneId: null);

        DeviceHelper.GetSleepTimeSeconds("not-registered", config).Should().Be(DeviceHelper.DefaultSleepSeconds);
    }

    [Fact]
    public void GetSleepTimeSeconds_never_returns_less_than_the_minimum()
    {
        // Fires every minute, so the next occurrence is always under the floor.
        var config = ConfigFor(wakeupCron: "* * * * *", timeZoneId: "UTC");

        DeviceHelper.GetSleepTimeSeconds("kindle-a", config).Should().Be(DeviceHelper.MinimumSleepSeconds);
    }

    [Fact]
    public void GetSleepTimeSeconds_falls_back_to_default_for_an_invalid_cron()
    {
        var config = ConfigFor(wakeupCron: "not a cron", timeZoneId: "UTC");

        DeviceHelper.GetSleepTimeSeconds("kindle-a", config).Should().Be(DeviceHelper.DefaultSleepSeconds);
    }

    [Fact]
    public void ResolveTimeZone_falls_back_to_utc_when_unset_or_unknown()
    {
        DeviceHelper.ResolveTimeZone(ConfigFor(null, timeZoneId: null)).Should().Be(TimeZoneInfo.Utc);
        DeviceHelper.ResolveTimeZone(ConfigFor(null, timeZoneId: "  ")).Should().Be(TimeZoneInfo.Utc);
        DeviceHelper.ResolveTimeZone(ConfigFor(null, timeZoneId: "Mars/Olympus_Mons")).Should().Be(TimeZoneInfo.Utc);
    }

    [Theory]
    [InlineData("0 8 * * *", true)]
    [InlineData("*/30 * * * *", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("0 8 * *", false)]
    [InlineData("nonsense", false)]
    public void TryParseCron_reports_whether_the_expression_is_usable(string? expression, bool expected)
    {
        DeviceHelper.TryParseCron(expression, out var cron, out var error).Should().Be(expected);

        if (expected)
        {
            cron.Should().NotBeNull();
            error.Should().BeEmpty();
        }
        else
        {
            cron.Should().BeNull();
            error.Should().NotBeEmpty();
        }
    }

    private static AppSettings ConfigFor(string? wakeupCron, string? timeZoneId) => new()
    {
        Settings = new AppSettings.SettingsConfig { TimeZoneId = timeZoneId },
        Devices = new Dictionary<string, AppSettings.DeviceConfig>
        {
            ["kindle-a"] = new() { WakeupCron = wakeupCron }
        }
    };
}

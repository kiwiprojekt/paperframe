using FluentAssertions;
using paperframe_server;
using paperframe_server.Helpers;

namespace paperframe_server.Tests;

/// <summary>Which answer a checking-in device gets, and why.</summary>
public class CheckInOutcomeTests
{
    [Fact]
    public void An_unknown_device_is_not_configured()
    {
        var outcome = CheckInOutcome.Resolve(Device("stranger"), Config());

        outcome.Verdict.Should().Be(CheckInVerdict.NotConfigured);
        outcome.ToCheckIn(Device("stranger")).Status.Should().Be("Error");
    }

    [Fact]
    public void A_switched_off_device_is_disabled_whatever_it_is_running()
    {
        var config = Config(disabled: true);

        CheckInOutcome.Resolve(Device(version: "0.1"), config).Verdict.Should().Be(CheckInVerdict.Disabled);
    }

    [Fact]
    public void A_current_device_is_served_its_rendering_script()
    {
        var outcome = CheckInOutcome.Resolve(Device(version: ClientProtocol.Version), Config());

        outcome.Verdict.Should().Be(CheckInVerdict.Serve);
        outcome.RedirectPath.Should().Be("/calendar/family");
    }

    [Fact]
    public void An_outdated_device_with_auto_update_on_is_provisioned()
    {
        var outcome = CheckInOutcome.Resolve(Device(version: "1.2"), Config(autoUpdate: true));

        outcome.Verdict.Should().Be(CheckInVerdict.Provision);
        outcome.RedirectPath.Should().Be("/provision");
    }

    [Fact]
    public void An_outdated_device_is_left_alone_until_auto_update_is_switched_on()
    {
        // Every device pulls the same launcher, so an update goes out one device at a time.
        var outcome = CheckInOutcome.Resolve(Device(version: "1.2"), Config(autoUpdate: null));

        outcome.Verdict.Should().Be(CheckInVerdict.Serve);
        outcome.Message.Should().Contain("autoUpdate is off");
    }

    [Theory]
    [InlineData("1.1")]
    [InlineData("1.0")]
    [InlineData(DeviceRequestReader.DefaultScriptVersion)]
    [InlineData("not-a-version")]
    public void A_device_too_old_to_be_driven_remotely_is_never_provisioned(string version)
    {
        // Its running shell would keep executing the old code, report the old version on
        // the next wake, and be sent straight back — a frame that never renders again.
        var outcome = CheckInOutcome.Resolve(Device(version: version), Config(autoUpdate: true));

        outcome.Verdict.Should().Be(CheckInVerdict.Serve);
        outcome.Message.Should().Contain("by hand");
    }

    [Fact]
    public void A_device_that_gave_up_updating_is_served_rather_than_sent_back()
    {
        var device = Device(version: "1.2") with { SkipProvisioning = true };

        var outcome = CheckInOutcome.Resolve(device, Config(autoUpdate: true));

        outcome.Verdict.Should().Be(CheckInVerdict.Serve);
        outcome.RedirectPath.Should().Be("/calendar/family");
    }

    private static DeviceRequest Device(string id = "kindle-a", string version = "1.2") =>
        new(id, Battery: 80, ScreenResolution: "758,1024", ScriptVersion: version,
            ClientLog: [], SkipProvisioning: false);

    private static AppSettings Config(bool? disabled = null, bool? autoUpdate = null) => new()
    {
        Devices = new Dictionary<string, AppSettings.DeviceConfig>
        {
            ["kindle-a"] = new()
            {
                ServiceName = "Calendar",
                ConfigId = "family",
                Disabled = disabled,
                AutoUpdate = autoUpdate
            }
        }
    };
}

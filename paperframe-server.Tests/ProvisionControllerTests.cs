using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using paperframe_server.Controllers;
using paperframe_server.Helpers;

namespace paperframe_server.Tests;

/// <summary>The script that replaces a launcher, and the launcher it hands over.</summary>
public class ProvisionControllerTests
{
    [Fact]
    public void The_provisioning_script_verifies_before_it_commits()
    {
        var script = Controller().Get();

        // Nothing irreversible happens before the download is proven complete and parseable.
        var install = script.IndexOf("mv -f \"$STAGED\"", StringComparison.Ordinal);
        install.Should().BeGreaterThan(script.IndexOf("grep -q \"^$SENTINEL$\"", StringComparison.Ordinal));
        install.Should().BeGreaterThan(script.IndexOf("sh -n \"$STAGED\"", StringComparison.Ordinal));

        // /mnt/us is a fuse overlay over vfat, so the file is re-read after the flush.
        script.Should().Contain("sync");
        script.LastIndexOf("sh -n \"$LAUNCHER\"", StringComparison.Ordinal).Should().BeGreaterThan(install);
    }

    [Fact]
    public void Every_failure_leaves_the_device_rendering_rather_than_blank()
    {
        var script = Controller().Get();

        // Exiting 0 would leave the screen on the blank the launcher cleared before running
        // this, with the next wake landing right back here.
        script.Should().Contain($"exit {ClientProtocol.ProvisionDeclinedExitCode}");
        Regex.Matches(script, @"^\s*exit 0$", RegexOptions.Multiline).Should().BeEmpty();
    }

    [Fact]
    public void Every_step_past_the_point_of_no_return_rolls_back()
    {
        var script = Controller().Get();

        // Once the launcher on disk has been replaced, no failure may leave it that way.
        // From the line after the install: the install's own failure has nothing to undo.
        var installed = script.IndexOf('\n', script.IndexOf("mv -f \"$STAGED\"", StringComparison.Ordinal));
        foreach (var line in script[installed..].Split('\n').Where(l => l.Contains("|| ") && l.Contains("give_up")))
        {
            line.Should().Contain("roll_back", "every failure after the install has to undo it");
        }

        script.Should().Contain("roll_back \"could not make the new launcher executable\"");
        script.Should().Contain("roll_back \"launcher unreadable after write\"");
    }

    [Fact]
    public void A_half_written_attempt_counter_cannot_kill_the_script()
    {
        // A file cut short by a full disk leaves the version where the count should be, and
        // feeding that to $(( )) is fatal in dash — which the launcher reads as a failure.
        Controller().Get().Should().Contain("*[!0-9]*");
    }

    [Fact]
    public void The_attempt_budget_is_never_cleared_by_the_script_that_spends_it()
    {
        // Retiring the budget is the new launcher's job, because running at all is the only
        // evidence the update worked. A handover that fails every time must not be able to
        // reset its own budget on the way out.
        Controller().Get().Should().NotContain("rm -f \"$ATTEMPTS_FILE\"");
        LauncherScript.Render("kindle-a", "https://paperframe.local").Should().Contain("provision-attempts");
    }

    [Fact]
    public void The_budget_is_keyed_to_the_version_being_installed()
    {
        // Otherwise a device that exhausted its attempts on one bad launcher would refuse
        // the fixed one that follows, and the only reset would be a USB cable.
        Controller().Get().Should().Contain($"= \"{ClientProtocol.Version}\"");
    }

    [Fact]
    public void There_is_no_taking_a_device_over_from_the_outside()
    {
        // Launchers that cannot restart themselves are never sent here — they read the
        // decline code as a script failure and stop, so every recoverable hiccup during an
        // update became a dark frame. They are flagged for a manual re-install instead.
        var script = Controller().Get();

        script.Should().NotContain("$PPID");
        script.Should().NotContain("nohup");
        script.Should().NotContain("kill -0");
    }

    [Fact]
    public void The_launcher_it_serves_is_the_one_the_manager_hands_out()
    {
        var controller = Controller();

        var served = controller.GetLauncher().Should().BeOfType<ContentResult>().Subject.Content!;

        served.Should().Be(LauncherScript.Render("kindle-a", "https://paperframe.local:8443"));
        served.TrimEnd().Should().EndWith(ClientProtocol.Sentinel);
    }

    private static ProvisionController Controller(string clientVersion = "1.3")
    {
        var context = new DefaultHttpContext
        {
            Request = { Scheme = "https", Host = new HostString("paperframe.local", 8443), Path = "/provision" }
        };
        context.Request.Headers["device_id"] = "kindle-a";
        context.Request.Headers["script_version"] = clientVersion;

        var settings = new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig> { ["kindle-a"] = new() }
        };

        return new ProvisionController(new TestSupport.TestOptionsMonitor<AppSettings>(settings))
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }
}

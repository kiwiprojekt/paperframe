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
        Regex.Matches(script, @"^\s*exit 0$", RegexOptions.Multiline).Should().HaveCount(1);
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

    [Theory]
    [InlineData("1.2", "1")]
    [InlineData("1.3", "")]
    public void Who_restarts_the_loop_is_decided_by_the_server(string clientVersion, string expectLegacy)
    {
        // Comparing versions in shell would be a second, looser copy of a rule that already
        // lives in ClientProtocol — and the shell copy would be an exact match, not a floor.
        var script = Controller(clientVersion).Get();

        script.Should().Contain($"if [ -n \"{expectLegacy}\" ]; then");
    }

    [Fact]
    public void The_launcher_it_serves_is_the_one_the_manager_hands_out()
    {
        var controller = Controller();

        var served = controller.GetLauncher().Should().BeOfType<ContentResult>().Subject.Content!;

        served.Should().Be(LauncherScript.Render("kindle-a", "https://paperframe.local:8443"));
        served.TrimEnd().Should().EndWith(ClientProtocol.Sentinel);
    }

    private static ProvisionController Controller(string clientVersion = "1.2")
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

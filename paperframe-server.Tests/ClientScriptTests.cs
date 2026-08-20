using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using paperframe_server.Controllers;
using paperframe_server.Helpers;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;

namespace paperframe_server.Tests;

/// <summary>What the launcher shell script shipped to a device must contain.</summary>
public class ClientScriptTests
{
    [Fact]
    public void Launcher_uses_request_origin_and_device_id()
    {
        var controller = NewController(new AppSettings
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

        var result = controller.DownloadClientScript("kindle-a");

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        var script = Encoding.UTF8.GetString(file.FileContents);
        script.Should().Contain("DEVICE_ID=\"kindle-a\"");
        script.Should().Contain("SERVICES_URL=\"https://paperframe.local:8443\"");
        script.Should().Contain("wget_headers.log");
        script.Should().Contain($"grep -i '{ClientProtocol.SleepHeader}:'");
        script.Should().Contain($"SCRIPT_VERSION=\"{ClientProtocol.Version}\"");
        script.Should().NotContain("@");
    }

    [Fact]
    public void Launcher_holds_the_screensaver_guard_for_the_whole_run()
    {
        var script = LauncherScript();

        // Acquired once before the loop, released only by cleanup, so a transient
        // failure cannot silently leave the screensaver free to paint over a frame.
        Regex.Matches(script, @"^lipc-set-prop com\.lab126\.powerd preventScreenSaver 1$", RegexOptions.Multiline)
            .Should().HaveCount(1);
        Regex.Matches(script, @"preventScreenSaver 0").Should().HaveCount(1);
        script.Should().Contain("trap 'cleanup 0' INT TERM");

        var cleanupBody = script[script.IndexOf("cleanup() {", StringComparison.Ordinal)..];
        cleanupBody[..cleanupBody.IndexOf('}')].Should().Contain("preventScreenSaver 0");
    }

    [Fact]
    public void Launcher_dies_rather_than_falling_back_to_a_local_sleep_interval()
    {
        var script = LauncherScript();

        script.Should().Contain("die \"wget_failed_$wget_result\"");
        script.Should().Contain("die \"missing_sleep_header\"");
        script.Should().Contain("/client/error");
        script.Should().NotContain($"SLEEP_TIME_S={WakeupSchedule.DefaultSleepSeconds}");
        script.Should().NotContain("sleepSeconds");
    }

    [Fact]
    public void Launcher_stops_cleanly_on_the_disable_header_before_running_anything()
    {
        var script = LauncherScript();

        script.Should().Contain($"grep -qi '{ClientProtocol.DisabledHeader}:'");

        // The check has to precede execution: a disabled device must not run a body it
        // was never meant to, nor die on the wake interval the server had no reason to send.
        var disableCheck = script.IndexOf(ClientProtocol.DisabledHeader, StringComparison.Ordinal);
        disableCheck.Should().BeLessThan(script.IndexOf("missing_sleep_header", StringComparison.Ordinal));
        disableCheck.Should().BeLessThan(script.IndexOf("./script.sh", StringComparison.Ordinal));
    }

    [Fact]
    public void Launcher_neutralises_shell_metacharacters_in_the_device_id()
    {
        var hostileId = "kindle\"; rm -rf /; echo \"";
        var controller = NewController(new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig> { [hostileId] = new() }
        });
        controller.ControllerContext = NewContext();

        var file = controller.DownloadClientScript(hostileId).Should().BeOfType<FileContentResult>().Subject;
        var script = Encoding.UTF8.GetString(file.FileContents);

        // The id stays inert inside its quotes: nothing that could close the string,
        // start a substitution, or add a line of its own survives substitution.
        var assignment = script.Split('\n').Single(l => l.StartsWith("DEVICE_ID="));
        assignment.Should().Be("DEVICE_ID=\"kindle; rm -rf /; echo \"");
        assignment[11..^1].Should().NotContainAny("\"", "$", "`", "\\");
    }

    private static string LauncherScript()
    {
        var controller = NewController(new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig> { ["kindle-a"] = new() }
        });
        controller.ControllerContext = NewContext();

        var file = controller.DownloadClientScript("kindle-a").Should().BeOfType<FileContentResult>().Subject;
        return Encoding.UTF8.GetString(file.FileContents);
    }

    private static ControllerContext NewContext() => new()
    {
        HttpContext = new DefaultHttpContext
        {
            Request = { Scheme = "https", Host = new HostString("paperframe.local", 8443) }
        }
    };

    private static ConfigController NewController(AppSettings options)
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("paperframe-script-test-").FullName, "appsettings.json");
        return new ConfigController(
            new ConfigFilePointer(path),
            new TestOptionsMonitor<AppSettings>(options),
            Substitute.For<IPaperframeLogService>());
    }
}

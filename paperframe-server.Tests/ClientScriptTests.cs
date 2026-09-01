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
        // No unresolved @TOKEN@ placeholder should survive rendering. A plain "not contain @"
        // check is too broad now that the script legitimately uses "$@" to forward wget args.
        script.Should().NotMatchRegex(@"@[A-Z0-9_]+@");
    }

    [Fact]
    public void Launcher_holds_the_screensaver_guard_for_the_whole_run()
    {
        var script = LauncherScript();

        // Reclaimed at the top of every iteration, because powerd takes it back on its own
        // across some suspend cycles, and released in exactly one place on the way out.
        Regex.Matches(script, @"preventScreenSaver 1").Should().HaveCount(1);
        Regex.Matches(script, @"preventScreenSaver 0").Should().HaveCount(1);
        script.IndexOf("while true; do", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("preventScreenSaver 1", StringComparison.Ordinal));

        var handler = script[script.IndexOf("on_exit() {", StringComparison.Ordinal)..];
        handler[..handler.IndexOf('}')].Should().Contain("preventScreenSaver 0");
    }

    [Fact]
    public void Launcher_routes_every_exit_through_one_handler()
    {
        var script = LauncherScript();

        // Signals set a reason and exit rather than releasing the guard themselves,
        // so the EXIT trap stays the only place the display is handed back.
        script.Should().Contain("trap on_exit EXIT");
        script.Should().MatchRegex(@"trap '.*EXIT_REASON=.*exit \d+' INT");
        script.Should().MatchRegex(@"trap '.*EXIT_REASON=.*exit \d+' TERM");
        script.Should().NotContain("cleanup");
    }

    [Fact]
    public void Launcher_records_the_exit_reason_before_anything_that_can_fail()
    {
        var script = LauncherScript();

        // The failures worth diagnosing are the ones that cannot reach the network,
        // so the local write has to come first in both paths.
        var handler = script[script.IndexOf("on_exit() {", StringComparison.Ordinal)..];

        // The guard release comes first: it is the one step that must never be skipped,
        // and the log write is the one that can block on a device mounted over USB.
        handler.IndexOf("preventScreenSaver 0", StringComparison.Ordinal)
            .Should().BeLessThan(handler.IndexOf("log_line EXIT", StringComparison.Ordinal));
        handler.IndexOf("log_line EXIT", StringComparison.Ordinal)
            .Should().BeLessThan(handler.IndexOf("report_failure", StringComparison.Ordinal));
    }

    [Fact]
    public void Launcher_waits_for_wifi_before_reaching_the_network()
    {
        var script = LauncherScript();

        script.Should().Contain("WIFI_TIMEOUT_S=30");
        script.Should().Contain("lipc-set-prop com.lab126.wifid enable 1");
        script.Should().Contain("lipc-get-prop com.lab126.wifid cmState");

        // A timed-out radio is fatal like any other failure: the alternative is a
        // device that wakes, fails, and sleeps forever on a wifi password change.
        script.Should().Contain(@"wait_for_wifi || die ""wifi_timeout_${WIFI_TIMEOUT_S}s""");
        script.IndexOf("wait_for_wifi ||", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("retry_wget --header", StringComparison.Ordinal));
    }

    [Fact]
    public void Launcher_carries_a_local_log_that_cannot_take_the_loop_down()
    {
        var script = LauncherScript();

        script.Should().Contain("log_line() {");
        script.Should().Contain(ClientProtocol.DeviceStateDir);

        // Every statement that touches the filesystem swallows its own failure: a full or
        // read-only device must cost a log line, never the frame.
        var body = Between(script, "log_line() {", "\n}");
        foreach (var line in new[] { "mkdir -p", ">> \"$LOG_FILE\"", ">> \"$LOG_OUTBOX\"" })
        {
            body.Split('\n').Single(l => l.Contains(line)).Should().Contain("2>/dev/null");
        }
    }

    [Theory]
    [InlineData("/bin/sh")]
    [InlineData("/bin/dash")]
    public void Launcher_is_valid_posix_shell(string shell)
    {
        // Worth more than any amount of string matching: the device runs this, and a
        // syntax error reaches it as a frame that never updates again.
        if (!File.Exists(shell))
        {
            return;
        }

        var path = Path.Combine(Directory.CreateTempSubdirectory("paperframe-syntax-").FullName, "paperframe.sh");
        File.WriteAllText(path, LauncherScript());

        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            shell, ["-n", path]) { RedirectStandardError = true })!;
        process.WaitForExit();

        process.ExitCode.Should().Be(0, process.StandardError.ReadToEnd());
    }

    [Fact]
    public void Launcher_ends_with_the_loop_and_its_sentinel()
    {
        var script = LauncherScript();

        // Two invariants in one place. The sentinel is how a device tells a complete
        // download from a truncated one. The loop being the last construct is what makes
        // replacing this file underneath a running shell safe: once the loop is entered
        // there is nothing left to read, so the update cannot corrupt what is executing.
        script.TrimEnd().Should().EndWith(ClientProtocol.Sentinel);

        var code = script.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();

        code[^1].Should().Be("done");
    }

    private static string Between(string script, string start, string end)
    {
        var from = script.IndexOf(start, StringComparison.Ordinal);
        var to = script.IndexOf(end, from, StringComparison.Ordinal);
        return script[from..to];
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

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using paperframe_server;
using paperframe_server.Helpers;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;

namespace paperframe_server.Tests;

public class ShellScriptTests
{
    [Fact]
    public void Render_strips_shell_metacharacters_from_untrusted_text()
    {
        var script = ShellScript.Render(ShellScript.PhotoFrame,
            ShellScript.Text("SERVICE", "Immich"),
            ShellScript.Text("FBINK_PATH", "/mnt/us/libkh/bin/fbink"),
            ShellScript.Text("IMAGE_PATH", "/immich/fam\"; rm -rf /; echo \"/image"),
            ShellScript.Raw("STATE_DIR", ClientProtocol.DeviceStateDir));

        var assignment = script.Split('\n').Single(l => l.StartsWith("IMAGE_URL="));
        assignment.Should().Be("IMAGE_URL=$SERVICES_URL\"/immich/fam; rm -rf /; echo /image\"");
    }

    [Fact]
    public void Render_passes_structural_values_through_untouched()
    {
        // A header name is part of the contract, not user text: it must not travel
        // through an escaper that could silently alter it.
        var script = ShellScript.Render(ShellScript.Launcher,
            ShellScript.Text("DEVICE_ID", "kindle-a"),
            ShellScript.Text("SERVER_URL", "https://paperframe.local"),
            ShellScript.Raw("SCRIPT_VERSION", ClientProtocol.Version),
            ShellScript.Raw("SLEEP_HEADER", "X-Sleep-Time"),
            ShellScript.Raw("DISABLED_HEADER", "X-Paperframe-Disabled"),
            ShellScript.Raw("SENTINEL", ClientProtocol.Sentinel),
            ShellScript.Raw("STATE_DIR", ClientProtocol.DeviceStateDir),
            ShellScript.Raw("PROVISIONED_EXIT_CODE", "42"),
            ShellScript.Raw("DECLINED_EXIT_CODE", "43"));

        script.Should().Contain("X-Sleep-Time:");
        script.Should().Contain("X-Paperframe-Disabled:");
    }

    [Theory]
    [InlineData(ShellScript.Launcher)]
    [InlineData(ShellScript.PhotoFrame)]
    [InlineData(ShellScript.Provision)]
    public void Every_template_pulling_in_retry_wget_also_pulls_in_the_log_it_calls(string template)
    {
        // retry_wget records its attempts through log_line. A template that took one without
        // the other would render cleanly here and fail as "command not found" on a device,
        // which is the failure this whole placeholder mechanism exists to prevent.
        var source = ShellScript.Load(template);

        if (source.Contains("@RETRY_WGET_FN@"))
        {
            source.Should().Contain("@LOG_FN@");
            source.IndexOf("@LOG_FN@", StringComparison.Ordinal)
                .Should().BeLessThan(source.IndexOf("@RETRY_WGET_FN@", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void A_fragment_resolves_against_the_defaults_and_not_the_caller()
    {
        // The point of the closed set: including @LOG_FN@ must never make that fragment's
        // own placeholders into arguments the including template has to know about.
        var script = ShellScript.Render(ShellScript.PhotoFrame,
            ShellScript.Text("SERVICE", "Immich"),
            ShellScript.Text("FBINK_PATH", "/mnt/us/libkh/bin/fbink"),
            ShellScript.Text("IMAGE_PATH", "/immich/frame/image"),
            ShellScript.Raw("STATE_DIR", "/tmp/ignored"));

        script.Should().Contain($"LOG_DIR=\"{ClientProtocol.DeviceStateDir}\"");
    }

    [Fact]
    public void An_unsupplied_placeholder_names_the_template_the_caller_asked_for()
    {
        // Including a fragment must not produce an error naming a file the caller has never
        // heard of. photo-frame.sh includes log.sh, whose own tokens come from the defaults.
        var render = () => ShellScript.Render(ShellScript.PhotoFrame, ShellScript.Text("SERVICE", "Immich"));

        render.Should().Throw<InvalidOperationException>()
            .WithMessage($"*'{ShellScript.PhotoFrame}'*");
    }

    [Fact]
    public void The_two_retention_streams_do_not_evict_each_other()
    {
        // A device delivering a full outbox must not push every check-in verdict out of the
        // manager's log view.
        var settings = new AppSettings
        {
            Devices = new Dictionary<string, AppSettings.DeviceConfig> { ["kindle-a"] = new() }
        };
        var service = new PaperframeLogService(
            new TestOptionsMonitor<AppSettings>(settings), TestSupport.TestLog.Sink());

        service.LogCheckIn(new CheckInRequest("kindle-a", "Calendar", "main", "Redirect", "the verdict", "758,1024", "1.3"));

        for (var i = 0; i < PaperframeLogService.MaxDeviceLines + 50; i++)
        {
            service.LogDeviceDiagnostics(DeviceRequestReader.Read(new HeaderDictionary
            {
                ["device_id"] = "kindle-a",
                [DeviceRequestReader.ClientLogHeader] = $"NET line {i}"
            }));
        }

        service.GetLogs().Should().Contain(l => l.Message == "the verdict");
    }

    [Fact]
    public void Render_rejects_a_template_placeholder_nobody_supplied()
    {
        var render = () => ShellScript.Render(ShellScript.PhotoFrame, ShellScript.Text("SERVICE", "Immich"));

        render.Should().Throw<InvalidOperationException>().WithMessage("*FBINK_PATH*");
    }

    [Theory]
    [InlineData(ShellScript.Launcher)]
    [InlineData(ShellScript.PhotoFrame)]
    public void Templates_are_embedded_and_start_with_a_shebang(string template)
    {
        ShellScript.Load(template).Should().StartWith("#!/bin/sh");
    }
}

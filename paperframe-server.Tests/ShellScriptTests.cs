using FluentAssertions;
using paperframe_server.Helpers;

namespace paperframe_server.Tests;

public class ShellScriptTests
{
    [Fact]
    public void Render_strips_shell_metacharacters_from_untrusted_text()
    {
        var script = ShellScript.Render(ShellScript.PhotoFrame,
            ShellScript.Text("SERVICE", "Immich"),
            ShellScript.Text("FBINK_PATH", "/mnt/us/libkh/bin/fbink"),
            ShellScript.Text("IMAGE_PATH", "/immich/fam\"; rm -rf /; echo \"/image"));

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
            ShellScript.Raw("SCRIPT_VERSION", "1.2"),
            ShellScript.Raw("SLEEP_HEADER", "X-Sleep-Time"),
            ShellScript.Raw("DISABLED_HEADER", "X-Paperframe-Disabled"));

        script.Should().Contain("X-Sleep-Time:");
        script.Should().Contain("X-Paperframe-Disabled:");
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

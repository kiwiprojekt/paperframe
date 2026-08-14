using FluentAssertions;
using paperframe_server.Helpers;

namespace paperframe_server.Tests;

public class ShellScriptTests
{
    [Fact]
    public void Render_strips_shell_metacharacters_from_untrusted_text()
    {
        var script = ShellScript.Render(ShellScript.CompileError,
            ShellScript.Text("SERVICE", "IMMICH"),
            ShellScript.Text("CONFIG_ID", "family"),
            ShellScript.Text("ERROR", "boom \"$(rm -rf /)\" `whoami`\nsecond line"));

        var errorLine = script.Split('\n').Single(l => l.Contains("Error:"));
        errorLine.Should().Be("$FBINK -q \"Error: boom (rm -rf /) whoamisecond line\" -t size=10,top=320 -O -m -C GRAY3");
    }

    [Fact]
    public void Render_passes_structural_values_through_untouched()
    {
        // An exit code is part of the contract, not user text: it must not travel
        // through an escaper that could silently alter it.
        var script = ShellScript.Render(ShellScript.Disabled,
            ShellScript.Text("DEVICE_ID", "kindle-a"),
            ShellScript.Raw("DISABLED_EXIT_CODE", "42"));

        script.Should().Contain("exit 42");
    }

    [Fact]
    public void Render_rejects_a_template_placeholder_nobody_supplied()
    {
        var render = () => ShellScript.Render(ShellScript.Disabled, ShellScript.Text("DEVICE_ID", "kindle-a"));

        render.Should().Throw<InvalidOperationException>().WithMessage("*DISABLED_EXIT_CODE*");
    }

    [Theory]
    [InlineData(ShellScript.Launcher)]
    [InlineData(ShellScript.Disabled)]
    [InlineData(ShellScript.CompileError)]
    [InlineData(ShellScript.PhotoFrame)]
    public void Templates_are_embedded_and_start_with_a_shebang(string template)
    {
        ShellScript.Load(template).Should().StartWith("#!/bin/sh");
    }
}

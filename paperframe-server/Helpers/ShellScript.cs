using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace paperframe_server.Helpers;

/// <summary>
/// Renders the shell scripts shipped to devices from templates embedded in this
/// assembly (see the ClientScripts folder).
///
/// Keeping the scripts in real .sh files rather than C# string literals means they
/// stay readable, lintable, and free of interpolation escaping — an interpolated
/// verbatim string silently mangles any shell construct containing braces.
/// </summary>
public static class ShellScript
{
    public const string Launcher = "paperframe.sh";
    public const string Disabled = "disabled.sh";
    public const string CompileError = "compile-error.sh";

    private static readonly ConcurrentDictionary<string, string> Templates = new();
    private static readonly Regex Placeholder = new(@"@([A-Z0-9_]+)@", RegexOptions.Compiled);
    private static readonly Regex ShellMetacharacters = new(@"[""\\$`!\r\n\t]", RegexOptions.Compiled);

    /// <summary>
    /// Substitutes <c>@TOKEN@</c> placeholders in the named template. Values are
    /// stripped of shell metacharacters, so the result is always valid /bin/sh no
    /// matter what a device id or exception message contains.
    /// </summary>
    public static string Render(string template, params (string Token, string Value)[] values)
    {
        var lookup = values.ToDictionary(v => v.Token, v => Escape(v.Value));

        return Placeholder.Replace(Templates.GetOrAdd(template, Load), match =>
        {
            var token = match.Groups[1].Value;
            return lookup.TryGetValue(token, out var value)
                ? value
                : throw new InvalidOperationException($"No value supplied for placeholder @{token}@ in '{template}'.");
        });
    }

    public static string Escape(string? value) => ShellMetacharacters.Replace(value ?? "", "");

    private static string Load(string template)
    {
        var resource = $"paperframe_server.ClientScripts.{template}";
        using var stream = typeof(ShellScript).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded client script '{resource}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

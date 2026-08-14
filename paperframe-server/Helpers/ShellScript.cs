using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace paperframe_server.Helpers;

/// <summary>
/// A value bound to a <c>@TOKEN@</c> placeholder. There is no implicit conversion on
/// purpose: every call site has to say whether it is substituting untrusted text or a
/// structural value the server controls, so escaping can never be forgotten by default.
/// </summary>
public readonly record struct ScriptValue(string Token, string Value);

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
    public const string PhotoFrame = "photo-frame.sh";

    private static readonly ConcurrentDictionary<string, string> Templates = new();
    private static readonly Regex Placeholder = new(@"@([A-Z0-9_]+)@", RegexOptions.Compiled);
    private static readonly Regex ShellMetacharacters = new(@"[""\\$`!\r\n\t]", RegexOptions.Compiled);

    /// <summary>Untrusted text — device ids, config ids, exception messages. Escaped.</summary>
    public static ScriptValue Text(string token, string? value) => new(token, Escape(value));

    /// <summary>A structural value this server controls — exit codes, header names, paths it composed.</summary>
    public static ScriptValue Raw(string token, string value) => new(token, value);

    /// <summary>
    /// Substitutes every <c>@TOKEN@</c> placeholder in the named template. Throws if the
    /// template contains a placeholder no caller supplied, so a template can never ship
    /// to a device half-rendered.
    /// </summary>
    public static string Render(string template, params ScriptValue[] values)
    {
        var lookup = values.ToDictionary(v => v.Token, v => v.Value);

        return Placeholder.Replace(Load(template), match =>
        {
            var token = match.Groups[1].Value;
            return lookup.TryGetValue(token, out var value)
                ? value
                : throw new InvalidOperationException($"No value supplied for placeholder @{token}@ in '{template}'.");
        });
    }

    public static string Escape(string? value) => ShellMetacharacters.Replace(value ?? "", "");

    public static string Load(string template) => Templates.GetOrAdd(template, static name =>
    {
        var resource = $"paperframe_server.ClientScripts.{name}";
        using var stream = typeof(ShellScript).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded client script '{resource}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}

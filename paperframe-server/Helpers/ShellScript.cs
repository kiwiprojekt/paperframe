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
    public const string PhotoFrame = "photo-frame.sh";
    public const string RetryWgetHelper = "retry-wget.sh";
    public const string LogHelper = "log.sh";
    public const string Provision = "provision.sh";

    /// <summary>
    /// Placeholders that pull in a shared shell fragment. Resolving them here rather than at
    /// each render site means including a fragment is a one-line change to the template that
    /// wants it, instead of an argument every caller has to remember to pass.
    /// </summary>
    private static readonly Dictionary<string, string> Fragments = new()
    {
        ["RETRY_WGET_FN"] = RetryWgetHelper,
        ["LOG_FN"] = LogHelper
    };

    /// <summary>
    /// Values the server owns outright, available to every template without being passed in.
    /// Nothing here varies per request. A caller may override one for its own template's
    /// placeholders, but not for a fragment's — see <see cref="Render"/>.
    ///
    /// This is what keeps a photo renderer from having to name the launcher's state
    /// directory just because the logging fragment writes there.
    /// </summary>
    private static readonly ScriptValue[] Defaults =
    [
        new("SCRIPT_VERSION", ClientProtocol.Version),
        new("TARGET_VERSION", ClientProtocol.Version),
        new("SLEEP_HEADER", ClientProtocol.SleepHeader),
        new("DISABLED_HEADER", ClientProtocol.DisabledHeader),
        new("LAUNCHER_PATH", ClientProtocol.LauncherDevicePath),
        new("STATE_DIR", ClientProtocol.DeviceStateDir),
        new("SENTINEL", ClientProtocol.Sentinel),
        new("PROVISIONED_EXIT_CODE", ClientProtocol.ProvisionedExitCode.ToString()),
        new("DECLINED_EXIT_CODE", ClientProtocol.ProvisionDeclinedExitCode.ToString())
    ];

    private static readonly ConcurrentDictionary<string, string> Templates = new();
    private static readonly Regex Placeholder = new(@"@([A-Z0-9_]+)@", RegexOptions.Compiled);
    private static readonly Regex ShellMetacharacters = new(@"[""\\$`!\r\n\t]", RegexOptions.Compiled);

    /// <summary>
    /// Anything that reached this process from outside it — device ids, config ids,
    /// request URLs and paths, exception messages. Escaped.
    /// </summary>
    public static ScriptValue Text(string token, string? value) => new(token, Escape(value));

    /// <summary>
    /// A compile-time constant this server owns: header names, protocol versions, exit
    /// codes. Nothing derived from a request qualifies, however well-formed it looks —
    /// a URL built from the Host header is the client's text, not the server's.
    /// </summary>
    public static ScriptValue Raw(string token, string value) => new(token, value);

    /// <summary>
    /// Substitutes every <c>@TOKEN@</c> placeholder in the named template. Throws if the
    /// template contains a placeholder no caller supplied, so a template can never ship
    /// to a device half-rendered.
    /// </summary>
    public static string Render(string template, params ScriptValue[] values)
    {
        // Last wins, so a caller can override a default rather than colliding with it.
        var lookup = new Dictionary<string, string>();
        foreach (var supplied in Defaults.Concat(values))
        {
            lookup[supplied.Token] = supplied.Value;
        }

        return Substitute(template, lookup, template, fragmentsAllowed: true);
    }

    /// <summary>
    /// Fills in one file's placeholders. <paramref name="requestedTemplate"/> is what the
    /// caller actually asked to render, so an error names a file they recognise rather than
    /// a fragment they never mentioned.
    /// </summary>
    private static string Substitute(
        string template, Dictionary<string, string> lookup, string requestedTemplate, bool fragmentsAllowed) =>
        Placeholder.Replace(Load(template), match =>
        {
            var token = match.Groups[1].Value;

            if (lookup.TryGetValue(token, out var value))
            {
                return value;
            }

            if (!Fragments.TryGetValue(token, out var fragment))
            {
                throw new InvalidOperationException(
                    $"No value supplied for placeholder @{token}@ in '{requestedTemplate}'"
                    + (template == requestedTemplate ? "." : $" (via fragment '{template}')."));
            }

            // Fragments are leaves. One fragment pulling in another would make the include
            // order load-bearing and open the door to a cycle; a fragment that needs a
            // second one is a dependency the including template states for itself.
            if (!fragmentsAllowed)
            {
                throw new InvalidOperationException(
                    $"Fragment '{template}' may not include another fragment (@{token}@). "
                    + $"Have '{requestedTemplate}' include both instead.");
            }

            // Rendered against the defaults alone, never the caller's values: a fragment's
            // own placeholders are its business, and letting them reach up into the caller
            // would make them arguments of every template that includes it.
            return Substitute(fragment, DefaultLookup, requestedTemplate, fragmentsAllowed: false);
        });

    private static readonly Dictionary<string, string> DefaultLookup =
        Defaults.ToDictionary(v => v.Token, v => v.Value);

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

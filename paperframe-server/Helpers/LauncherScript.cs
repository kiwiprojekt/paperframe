namespace paperframe_server.Helpers;

/// <summary>
/// Renders the launcher a device runs. Two callers need the identical bytes — the manager,
/// where a human downloads it for a first install, and the provisioning route, where a
/// device fetches it to replace itself — so neither owns the recipe.
///
/// Everything the launcher needs beyond these two values is a server-owned constant that
/// <see cref="ShellScript"/> supplies to every template.
/// </summary>
public static class LauncherScript
{
    public const string FileName = "paperframe.sh";

    public static string Render(string deviceId, string origin) =>
        ShellScript.Render(ShellScript.Launcher,
            ShellScript.Text("DEVICE_ID", deviceId),
            ShellScript.Text("SERVER_URL", origin));
}

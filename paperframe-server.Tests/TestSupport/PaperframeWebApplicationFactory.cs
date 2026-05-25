using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace paperframe_server.Tests.TestSupport;

internal sealed class PaperframeWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _configDir;

    public PaperframeWebApplicationFactory(string appsettingsJson)
    {
        _configDir = Path.Combine(Path.GetTempPath(), "paperframe-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDir);
        File.WriteAllText(Path.Combine(_configDir, "appsettings.json"), appsettingsJson);
        Environment.SetEnvironmentVariable(Program.ConfigDirEnvironmentVariable, _configDir);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable(Program.ConfigDirEnvironmentVariable, null);
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_configDir))
        {
            Directory.Delete(_configDir, recursive: true);
        }
    }
}

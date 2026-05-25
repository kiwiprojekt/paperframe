using Flurl.Http;
using paperframe_server.Services;
using Microsoft.Extensions.FileProviders;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace paperframe_server;

public record ConfigFilePointer(string FilePath);

public class Program
{
    internal const string ConfigDirEnvironmentVariable = "PAPERFRAME_CONFIG_DIR";
    private const string ConfigFilename = "appsettings.json";

    internal static string ResolveConfigFilePath(IHostEnvironment environment)
    {
        var configuredDir = Environment.GetEnvironmentVariable(ConfigDirEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredDir))
        {
            return Path.Combine(configuredDir, ConfigFilename);
        }

        var configDir = "/app/config";
        if (!Directory.Exists(configDir))
        {
            configDir = Path.Combine(environment.ContentRootPath, "config");
        }

        return Path.Combine(configDir, ConfigFilename);
    }

    internal static void InitializeConfigFile(string fullConfigPath, string defaultConfigPath)
    {
        try
        {
            var configDir = Path.GetDirectoryName(fullConfigPath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            if (!File.Exists(fullConfigPath) && File.Exists(defaultConfigPath))
            {
                File.Copy(defaultConfigPath, fullConfigPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing configuration directories: {ex.Message}");
        }
    }

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var fullConfigPath = ResolveConfigFilePath(builder.Environment);
        InitializeConfigFile(fullConfigPath, Path.Combine(builder.Environment.ContentRootPath, ConfigFilename));

        builder.Configuration.AddJsonFile(fullConfigPath, optional: true, reloadOnChange: true);

        // Register ConfigFilePointer so Controllers can write back to it
        builder.Services.AddSingleton(new ConfigFilePointer(fullConfigPath));

        builder.Services.AddControllers();
        
        builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("Configuration"));

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AuthService>();
        
        builder.Services.AddSingleton<ICalendarService, CalendarService>();
        builder.Services.AddSingleton<ICalendarLayoutService, CalendarLayoutService>();
        builder.Services.AddSingleton<IHomeAssistantService, HomeAssistantService>();
        builder.Services.AddSingleton<IImmichService, ImmichService>();
        builder.Services.AddSingleton<IImageProcessingService, ImageProcessingService>();
        builder.Services.AddSingleton<IPaperframeLogService, PaperframeLogService>();

        var app = builder.Build();

        app.UseStaticFiles(new StaticFileOptions()
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(builder.Environment.ContentRootPath, "StaticAssets")),
            RequestPath = "/assets"
        });

        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";

            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase))
            {
                var cfg = context.RequestServices
                    .GetRequiredService<IOptionsMonitor<AppSettings>>()
                    .CurrentValue;
                var password = cfg.Settings?.ManagerPassword;

                if (!string.IsNullOrEmpty(password))
                {
                    var authService = context.RequestServices.GetRequiredService<AuthService>();
                    var token = context.Request.Cookies["paperframe_session"];

                    if (!authService.ValidateSession(token))
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsJsonAsync(new { message = "Unauthorized" });
                        return;
                    }
                }
            }

            await next();
        });

        app.MapControllers();

        app.Run();
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using Flurl.Http;
using paperframe_server.Services;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace paperframe_server.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly ConfigFilePointer _filePointer;
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;
    private readonly IPaperframeLogService _logService;

    public ConfigController(
        ConfigFilePointer filePointer,
        IOptionsMonitor<AppSettings> optionsMonitor,
        IPaperframeLogService logService)
    {
        _filePointer = filePointer;
        _optionsMonitor = optionsMonitor;
        _logService = logService;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        try
        {
            var config = _optionsMonitor.CurrentValue;
            return Ok(config);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error reading config: {ex.Message}" });
        }
    }

    [HttpPost]
    public IActionResult SaveConfig([FromBody] AppSettings newSettings)
    {
        try
        {
            if (newSettings == null)
            {
                return BadRequest(new { message = "Invalid configuration data." });
            }

            JsonNode node;
            if (System.IO.File.Exists(_filePointer.FilePath))
            {
                var jsonText = System.IO.File.ReadAllText(_filePointer.FilePath);
                node = JsonNode.Parse(jsonText) ?? new JsonObject();
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePointer.FilePath)!);
                node = new JsonObject();
            }

            // Update only the Configuration section
            var serializedSettings = JsonSerializer.SerializeToNode(newSettings);
            node["Configuration"] = serializedSettings;

            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = node.ToJsonString(options);

            System.IO.File.WriteAllText(_filePointer.FilePath, updatedJson);

            return Ok(new { message = "Configuration saved successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error saving config: {ex.Message}" });
        }
    }

    [HttpPost("validate/calendar")]
    public async Task<IActionResult> ValidateCalendar([FromBody] AppSettings.CalendarConfig config)
    {
        if (config == null) return BadRequest(new { message = "Invalid calendar config" });

        try
        {
            if (string.IsNullOrEmpty(config.CultureInfoName))
            {
                return Ok(new { success = false, message = "Invalid culture identifier: '' (e.g. use 'pl-PL' or 'en-US')" });
            }

            if (string.IsNullOrEmpty(config.TimeZoneId))
            {
                return Ok(new { success = false, message = "Invalid Timezone ID: '' (e.g. use 'Poland' or 'Europe/Warsaw')" });
            }

            // Validate Culture
            try
            {
                CultureInfo.GetCultureInfo(config.CultureInfoName);
            }
            catch
            {
                return Ok(new { success = false, message = $"Invalid culture identifier: '{config.CultureInfoName}' (e.g. use 'pl-PL' or 'en-US')" });
            }

            // Validate TimeZone
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId);
            }
            catch
            {
                return Ok(new { success = false, message = $"Invalid Timezone ID: '{config.TimeZoneId}' (e.g. use 'Poland' or 'Europe/Warsaw')" });
            }

            // Validate iCal URLs
            if (config.IcalUrls == null || config.IcalUrls.Length == 0)
            {
                return Ok(new { success = false, message = "Calendar has no iCal URLs configured." });
            }

            foreach (var url in config.IcalUrls)
            {
                if (string.IsNullOrEmpty(url)) continue;
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    return Ok(new { success = false, message = $"URL must start with http/https: '{url}'" });
                }

                try
                {
                    var responseText = await url.WithTimeout(5).GetStringAsync();
                    if (!responseText.Contains("BEGIN:VCALENDAR"))
                    {
                        return Ok(new { success = false, message = $"URL responded but did not contain valid iCalendar header: '{url}'" });
                    }
                }
                catch (Exception ex)
                {
                    return Ok(new { success = false, message = $"Failed to fetch calendar from '{url}': {ex.Message}" });
                }
            }

            return Ok(new { success = true, message = "Calendar configuration is completely valid!" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"Validation error: {ex.Message}" });
        }
    }

    [HttpPost("validate/immich")]
    public async Task<IActionResult> ValidateImmich([FromBody] AppSettings.ImmichConfig config)
    {
        if (config == null) return BadRequest(new { message = "Invalid Immich config" });

        try
        {
            if (string.IsNullOrEmpty(config.ApiUrl) || string.IsNullOrEmpty(config.ApiKey))
            {
                return Ok(new { success = false, message = "API URL and API Key are required." });
            }

            var albumsUrl = config.ApiUrl.TrimEnd('/') + "/albums";
            ImmichAlbum[] albums;
            try
            {
                albums = await albumsUrl
                    .WithHeader("x-api-key", config.ApiKey)
                    .WithTimeout(6)
                    .GetJsonAsync<ImmichAlbum[]>();
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Failed to connect to Immich at '{albumsUrl}': {ex.Message}" });
            }

            if (albums == null || albums.Length == 0)
            {
                return Ok(new { success = false, message = "Connected to Immich, but found no albums in your account." });
            }

            var matchingAlbum = albums.FirstOrDefault(a => a.AlbumName.Equals(config.AlbumName, StringComparison.OrdinalIgnoreCase));
            if (matchingAlbum == null)
            {
                var albumNames = string.Join(", ", albums.Select(a => $"'{a.AlbumName}'"));
                return Ok(new { success = false, message = $"Album '{config.AlbumName}' not found. Available albums: {albumNames}" });
            }

            return Ok(new { success = true, message = $"Valid configuration! Connected successfully and located album '{matchingAlbum.AlbumName}' with {matchingAlbum.AssetCount} assets." });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"Validation failed: {ex.Message}" });
        }
    }

    [HttpPost("validate/homeassistant")]
    public async Task<IActionResult> ValidateHomeAssistant([FromBody] AppSettings.HomeAssistantConfig config)
    {
        if (config == null) return BadRequest(new { message = "Invalid HA config" });

        try
        {
            if (string.IsNullOrEmpty(config.ApiUrl) || string.IsNullOrEmpty(config.OAuthBearerToken))
            {
                return Ok(new { success = false, message = "HA ApiUrl and Bearer Token are required." });
            }

            var testUrl = config.ApiUrl.TrimEnd('/') + "/";
            try
            {
                var result = await testUrl
                    .WithOAuthBearerToken(config.OAuthBearerToken)
                    .WithTimeout(5)
                    .GetJsonAsync<JsonElement>();

                var message = result.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Connected";
                return Ok(new { success = true, message = $"Successfully connected to Home Assistant: '{message}'" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Failed to query Home Assistant at '{testUrl}': {ex.Message}" });
            }
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"Validation failed: {ex.Message}" });
        }
    }

    [HttpPost("immich/albums")]
    public async Task<IActionResult> GetImmichAlbums([FromBody] ImmichAlbumsRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.ApiUrl) || string.IsNullOrEmpty(request.ApiKey))
        {
            return BadRequest(new { message = "apiUrl and apiKey are required." });
        }

        try
        {
            var albumsUrl = request.ApiUrl.TrimEnd('/') + "/albums";
            var albums = await albumsUrl
                .WithHeader("x-api-key", request.ApiKey)
                .WithTimeout(8)
                .GetJsonAsync<ImmichAlbum[]>();

            var list = albums.Select(a => new {
                id = a.Id,
                name = a.AlbumName,
                count = a.AssetCount
            }).ToList();

            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Failed to query Immich albums: {ex.Message}" });
        }
    }

    [HttpGet("devices/status")]
    public IActionResult GetDeviceStatuses()
    {
        return Ok(_logService.GetDeviceStatuses());
    }

    [HttpGet("download-client/{deviceId}")]
    public IActionResult DownloadClientScript(string deviceId, [FromQuery] int sleepSeconds = 7200)
    {
        var config = _optionsMonitor.CurrentValue;
        if (config.Devices == null || !config.Devices.TryGetValue(deviceId, out var deviceConfig))
        {
            return NotFound($"Device '{deviceId}' is not configured.");
        }

        var scheme = Request.Scheme;
        var host = Request.Host;
        var serverUrl = $"{scheme}://{host}";

        var paperframeTemplate = $@"#!/bin/sh
# Name: Paperframe Client
# Author: Michal Sadurski
# DontUseFBInk
# Auto-provisioned for Device: {deviceId}

# -------- Configuration --------
DEVICE_ID=""{deviceId}"" 
SLEEP_TIME_S={sleepSeconds}
SERVICES_URL=""{serverUrl}""
# -------------------------------
SCREEN_RES=""$(eips -i | grep 'xres:' | tr -d ' xres:' | tr 'y' ',')""
# -------------------------------

# mount filesystem as writeable
mntroot rw

# disable screensaver
lipc-set-prop com.lab126.powerd preventScreenSaver 1

# move to documents directory
cd /mnt/us/documents

while true; do
    # get battery status
    BATT_PERCENT=""$(gasgauge-info -s)""

    # download script to execute
    wget --header=""device_id: $DEVICE_ID"" \
        --header=""battery: $BATT_PERCENT"" \
        --header=""screen_res: $SCREEN_RES"" \
        -O script.sh $SERVICES_URL; \
        wget_result=$?

    #if download failed
    if [ $wget_result -ne 0 ]; then
        #reenable screensaver 
        lipc-set-prop com.lab126.powerd preventScreenSaver 0
        # exit
        return 1;
    fi

    # clear display
    eips -f
    sleep 1
    eips -f

    #make variables available to downloaded script
    export DEVICE_ID SCREEN_RES SERVICES_URL BATT_PERCENT

    # run downloaded script
    ./script.sh; script_result=$?
    
    if [ $script_result -ne 0 ]; then
        #reenable screensaver 
        lipc-set-prop com.lab126.powerd preventScreenSaver 0
        # exit
        return 1;
    fi

    sleep 3

    # set powersave mode
    echo powersave > /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor

    # schedule next wakeup
    rtcwake -d /dev/rtc1 -m no -s $SLEEP_TIME_S

    # set sleep mode
    echo ""mem"" > /sys/power/state

    sleep 5;
done
";
        var bytes = System.Text.Encoding.UTF8.GetBytes(paperframeTemplate);
        return File(bytes, "application/x-sh", "paperframe.sh");
    }

    private class ImmichAlbum
    {
        [System.Text.Json.Serialization.JsonPropertyName("albumName")]
        public string AlbumName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("assetCount")]
        public int AssetCount { get; set; }
    }
}

public class ImmichAlbumsRequest
{
    public string ApiUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

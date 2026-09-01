using System.Text.Json.Serialization;

namespace paperframe_server;

public class AppSettings
{
    public Dictionary<string, DeviceConfig>? Devices { get; set; }

    public Dictionary<string, CalendarConfig>? Calendar { get; set; }

    public Dictionary<string, ImmichConfig>? Immich { get; set; }

    public Dictionary<string, ArtChicagoConfig>? ArtChicago { get; set; }

    public HomeAssistantConfig? HomeAssistant { get; set; }

    public SettingsConfig? Settings { get; set; }

    public class SettingsConfig
    {
        public string? ServerAddress { get; set; }
        public bool? EnableCalendar { get; set; }
        public bool? EnableImmich { get; set; }
        public bool? EnableArtChicago { get; set; }
        public bool? EnableHomeAssistant { get; set; }
        public string? ManagerPassword { get; set; }

        /// <summary>IANA id used to resolve device wakeup cron expressions. Defaults to UTC.</summary>
        public string? TimeZoneId { get; set; }
    }

    public class DeviceConfig
    {
        public string? ServiceName { get; set; }
        public string? ConfigId { get; set; }
        public bool? Disabled { get; set; }
        public string? WakeupCron { get; set; }

        /// <summary>
        /// Whether the server may replace this device's launcher when it checks in with an
        /// outdated version. Off by default: every device pulls the same launcher, so a bad
        /// one would take the whole fleet out at once, and recovery means a USB cable per
        /// Kindle. Promote one device, watch it survive a cycle, then enable the rest.
        /// </summary>
        public bool? AutoUpdate { get; set; }
    }

    public class CalendarConfig
    {
        public string[]? IcalUrls { get; set; }
        public string? CultureInfoName { get; set; }
        public string? TimeZoneId { get; set; }
        public string? HeaderToday { get; set; }
        public string? HeaderTomorrow { get; set; }
        public string? FbinkPath { get; set; }
        public string? FontPath { get; set; }
    }

    public class HomeAssistantConfig
    {
        public string? ApiUrl { get; set; }

        [JsonPropertyName("oauthBearerToken")]
        public string? OAuthBearerToken { get; set; }

        public string? UpdateEntityTemplate { get; set; } = "input_datetime.paperframe_{deviceId}_update";
        public string? BatteryEntityTemplate { get; set; } = "input_number.paperframe_{deviceId}_battery";
        public bool DisableUpdateEntity { get; set; }
        public bool DisableBatteryEntity { get; set; }
    }

    public class ImmichConfig
    {
        public string? ApiUrl { get; set; }
        public string? ApiKey { get; set; }
        public string? AlbumName { get; set; }
        public string? FbinkPath { get; set; }
        public int Brightness { get; set; }
        public int Contrast { get; set; }
    }

    public class ArtChicagoConfig
    {
        public string? Query { get; set; }
        public string? FbinkPath { get; set; }
        public int Brightness { get; set; }
        public int Contrast { get; set; }
        public string? Orientation { get; set; } = "all";
        public int Rotation { get; set; } = 0;
    }
}
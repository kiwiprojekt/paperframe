using System.Text.Json.Serialization;

namespace paperframe_server;

public class AppSettings
{
    public Dictionary<string, DeviceConfig>? Devices { get; set; }

    public Dictionary<string, CalendarConfig>? Calendar { get; set; }

    public Dictionary<string, ImmichConfig>? Immich { get; set; }

    public HomeAssistantConfig? HomeAssistant { get; set; }

    public SettingsConfig? Settings { get; set; }

    public class SettingsConfig
    {
        public string? ServerAddress { get; set; }
        public bool? EnableCalendar { get; set; }
        public bool? EnableImmich { get; set; }
        public bool? EnableHomeAssistant { get; set; }
        public string? ManagerPassword { get; set; }
    }

    public class DeviceConfig
    {
        public string? ServiceName { get; set; }
        public string? ConfigId { get; set; }
        public bool? Disabled { get; set; }
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
}
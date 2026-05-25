using Flurl.Http;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace paperframe_server.Services;

public class HomeAssistantService : IHomeAssistantService
{
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;

    public HomeAssistantService(IOptionsMonitor<AppSettings> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public async Task UpdateEntities(string deviceId, int? battery)
    {
        var config = _optionsMonitor.CurrentValue.HomeAssistant;
        var deviceIds = _optionsMonitor.CurrentValue.Devices?.Keys.ToList() ?? new List<string>();
        
        if (string.IsNullOrEmpty(deviceId)) return;
        
        if (deviceIds.Contains(deviceId) && config != null && !string.IsNullOrEmpty(config.ApiUrl))
        {
            try
            {
                var updateTemplate = string.IsNullOrEmpty(config.UpdateEntityTemplate) 
                    ? "input_datetime.paperframe_{deviceId}_update" 
                    : config.UpdateEntityTemplate;

                var batteryTemplate = string.IsNullOrEmpty(config.BatteryEntityTemplate) 
                    ? "input_number.paperframe_{deviceId}_battery" 
                    : config.BatteryEntityTemplate;

                var updateEntityId = updateTemplate.Replace("{deviceId}", deviceId);
                var batteryEntityId = batteryTemplate.Replace("{deviceId}", deviceId);

                if (!config.DisableUpdateEntity)
                {
                    _ = await (config.ApiUrl.TrimEnd('/') + $"/states/{updateEntityId}")
                        .WithOAuthBearerToken(config.OAuthBearerToken)
                        .PostJsonAsync(new { state = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") });
                }

                if (battery.HasValue && !config.DisableBatteryEntity)
                {
                    var result = await (config.ApiUrl.TrimEnd('/') + $"/states/{batteryEntityId}")
                        .WithOAuthBearerToken(config.OAuthBearerToken)
                        .PostJsonAsync(new { state = battery.Value });
                    
                    Console.WriteLine($"HA Update Result: {result.ResponseMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Home Assistant: {ex.Message}");
            }
        }
    }
}

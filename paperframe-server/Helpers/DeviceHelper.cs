using System;
using Cronos;

namespace paperframe_server.Helpers;

public static class DeviceHelper
{
    public static int GetSleepTimeSeconds(string deviceId, AppSettings config, int defaultSleep = 7200)
    {
        if (config.Devices != null && config.Devices.TryGetValue(deviceId, out var deviceConfig) && !string.IsNullOrEmpty(deviceConfig.WakeupCron))
        {
            try
            {
                var cron = CronExpression.Parse(deviceConfig.WakeupCron);
                var next = cron.GetNextOccurrence(DateTime.UtcNow);
                if (next.HasValue)
                {
                    var delay = (int)(next.Value - DateTime.UtcNow).TotalSeconds;
                    if (delay < 60) delay = 60; // Minimum 1 minute sleep
                    return delay;
                }
            }
            catch
            {
                // Fallback to default if parsing fails at runtime
            }
        }
        return defaultSleep;
    }

    public static bool TryParseCron(string cronExpression, out string errorMessage)
    {
        if (string.IsNullOrEmpty(cronExpression))
        {
            errorMessage = string.Empty;
            return true;
        }

        try
        {
            CronExpression.Parse(cronExpression);
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}

using System;
using Cronos;

namespace paperframe_server.Helpers;

public static class DeviceHelper
{
    public const int DefaultSleepSeconds = 7200;
    public const int MinimumSleepSeconds = 60;
    public const int ErrorFallbackSleepSeconds = 3600;
    public const string ClientScriptVersion = "1.0";

    public static int GetSleepTimeSeconds(string deviceId, AppSettings config, int defaultSleep = DefaultSleepSeconds)
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
                    if (delay < MinimumSleepSeconds) delay = MinimumSleepSeconds;
                    return delay;
                }
            }
            catch (CronFormatException)
            {
                // Fall back to the default if the configured cron expression is invalid.
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

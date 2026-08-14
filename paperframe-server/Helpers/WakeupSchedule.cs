using System;
using System.Diagnostics.CodeAnalysis;
using Cronos;

namespace paperframe_server.Helpers;

/// <summary>
/// Decides when a device should wake next, from its cron expression resolved in the
/// server's configured timezone.
/// </summary>
public static class WakeupSchedule
{
    public const int DefaultSleepSeconds = 7200;
    public const int MinimumSleepSeconds = 60;

    /// <summary>
    /// Seconds until the device's next scheduled wake, or <see cref="DefaultSleepSeconds"/>
    /// when it has no usable schedule. Cron expressions are resolved in the server's
    /// configured timezone, so "0 8 * * *" means 8am where the frame actually hangs.
    /// </summary>
    public static int SecondsUntilNextWake(string deviceId, AppSettings config)
    {
        if (config.Devices == null
            || !config.Devices.TryGetValue(deviceId, out var device)
            || !TryParseCron(device.WakeupCron, out var cron, out _))
        {
            return DefaultSleepSeconds;
        }

        var now = DateTimeOffset.UtcNow;
        var next = cron.GetNextOccurrence(now, ResolveTimeZone(config));

        // Round up: truncating would wake the device a fraction of a second *before*
        // the scheduled minute, which reads as the previous hour.
        return next.HasValue
            ? Math.Max(MinimumSleepSeconds, (int)Math.Ceiling((next.Value - now).TotalSeconds))
            : DefaultSleepSeconds;
    }

    /// <summary>Falls back to UTC when unset or unknown to this host's timezone database.</summary>
    public static TimeZoneInfo ResolveTimeZone(AppSettings config)
    {
        var timeZoneId = config.Settings?.TimeZoneId;
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        return TryResolveTimeZone(timeZoneId, out var timeZone, out _) ? timeZone : TimeZoneInfo.Utc;
    }

    public static bool TryResolveTimeZone(string? timeZoneId, [NotNullWhen(true)] out TimeZoneInfo? timeZone, out string error)
    {
        timeZone = null;

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId!);
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentNullException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryParseCron(string? expression, [NotNullWhen(true)] out CronExpression? cron, out string error)
    {
        cron = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "Cron expression is empty.";
            return false;
        }

        try
        {
            cron = CronExpression.Parse(expression);
            error = string.Empty;
            return true;
        }
        catch (CronFormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

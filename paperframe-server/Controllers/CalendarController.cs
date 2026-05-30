using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using paperframe_server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace paperframe_server.Controllers;

[ApiController]
[Route("calendar")]
public class CalendarController : ControllerBase
{
    private static string ShellEscape(string input) =>
        Regex.Replace(input, @"[""\\$`!\r\n\t]", "");

    private readonly ICalendarService _calendarService;
    private readonly ICalendarLayoutService _calendarLayoutService;
    private readonly IHomeAssistantService _homeAssistantService;
    private readonly IPaperframeLogService _logService;
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;

    public CalendarController(
        ICalendarService calendarService,
        ICalendarLayoutService calendarLayoutService,
        IHomeAssistantService homeAssistantService,
        IOptionsMonitor<AppSettings> options,
        IPaperframeLogService logService)
    {
        _calendarService = calendarService;
        _calendarLayoutService = calendarLayoutService;
        _homeAssistantService = homeAssistantService;
        _logService = logService;
        _optionsMonitor = options;
    }

    [HttpGet("{configId}")]
    public async Task<string> Get(string configId, [FromHeader(Name="screen_res")]string? screenRes = null)
    {
        var deviceId = Request.Headers["device_id"].FirstOrDefault() ?? "unknown";
        var batteryVal = Request.Headers["battery"].FirstOrDefault();
        int? battery = int.TryParse(batteryVal, out var b) ? b : null;
        
        if (string.IsNullOrEmpty(screenRes)) screenRes = "758,1024";

        try
        {
            if (deviceId == "unknown")
            {
                throw new ArgumentException("Missing 'device_id' header. Make sure the Paperframe client sends a valid device identifier.");
            }

            var calendarConfigs = _optionsMonitor.CurrentValue.Calendar;
            if (calendarConfigs == null || !calendarConfigs.TryGetValue(configId, out var config))
            {
                throw new KeyNotFoundException($"Layout configuration '{configId}' is not defined in Calendar configs.");
            }

            var m = decimal.TryParse(screenRes.Split(',')[0], out var screenWidth) && screenWidth > 0
                ? screenWidth / 758m
                : 1m;
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId ?? "UTC");
            var date = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            var culture = System.Globalization.CultureInfo.GetCultureInfo(config.CultureInfoName ?? "en-US");
            
            var events = await this._calendarService.GetCalendarEventsForDateRange(config.IcalUrls ?? Array.Empty<string>(), date, date.AddDays(14));

            var context = new CalendarLayoutContext
            {
                Config = config,
                ScaleMultiplier = m,
                ReferenceDate = date,
                Culture = culture,
                TimeZone = timeZone,
                Events = events
            };

            var script = _calendarLayoutService.CompileScript(context);

            // Log success check-in
            _logService.LogCheckIn(deviceId, battery, screenRes, "Calendar", configId, "Success", "Calendar rendering script generated successfully.");

            return script;
        }
        catch (Exception ex)
        {
            // Log compile failure
            _logService.LogCheckIn(deviceId, battery, screenRes, "Calendar", configId, "Error", $"Layout compile failed: {ex.Message}");

            // Safe E-Ink diagnostic script
            return $@"#!/bin/sh
# CALENDAR COMPILE ERROR RUNTIME FALLBACK
FBINK=""/mnt/us/libkh/bin/fbink""
$FBINK -q -k
$FBINK -q ""CALENDAR COMPILE ERROR"" -t size=20,top=200 -O -m -C GRAY9
$FBINK -q ""Config ID: {ShellEscape(configId)}"" -t size=12,top=260 -O -m -C GRAY6
$FBINK -q ""Error: {ShellEscape(ex.Message)}"" -t size=10,top=320 -O -m -C GRAY3
";
        }
    }
}
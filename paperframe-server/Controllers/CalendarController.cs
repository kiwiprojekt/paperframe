using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using paperframe_server.Services;
using paperframe_server.Helpers;
using paperframe_server.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace paperframe_server.Controllers;

[ApiController]
[Route("calendar")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendarService;
    private readonly ICalendarLayoutService _calendarLayoutService;
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;

    public CalendarController(
        ICalendarService calendarService,
        ICalendarLayoutService calendarLayoutService,
        IOptionsMonitor<AppSettings> options)
    {
        _calendarService = calendarService;
        _calendarLayoutService = calendarLayoutService;
        _optionsMonitor = options;
    }

    [HttpGet("{configId}")]
    [DeviceScript]
    public async Task<string> Get(string configId, [FromHeader(Name="screen_res")]string? screenRes = null)
    {
        if (string.IsNullOrEmpty(screenRes)) screenRes = DeviceRequestReader.DefaultScreenResolution;

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

        return _calendarLayoutService.CompileScript(context);
    }
}
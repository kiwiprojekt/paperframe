using FluentAssertions;
using paperframe_server.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace paperframe_server.Tests;

public class CalendarLayoutServiceTests
{
    [Fact]
    public void CompileScript_generates_valid_fbink_shell_script()
    {
        var service = new CalendarLayoutService();
        var config = new AppSettings.CalendarConfig
        {
            IcalUrls = new[] { "https://calendar.example/feed.ics" },
            CultureInfoName = "en-US",
            TimeZoneId = "UTC",
            HeaderToday = "Today Label",
            HeaderTomorrow = "Tomorrow Label",
            FbinkPath = "/mnt/us/libkh/bin/fbink",
            FontPath = "/mnt/us/documents/Cal_Sans/CalSans-Regular.ttf"
        };

        var referenceDate = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
        var events = new List<ICalendarService.CalendarEntry>
        {
            new()
            {
                UtcDate = referenceDate,
                Summary = "Diner with $Family",
                IsAllDayEvent = false
            }
        };

        var context = new CalendarLayoutContext
        {
            Config = config,
            ScaleMultiplier = 1.0m,
            ReferenceDate = referenceDate,
            Culture = CultureInfo.GetCultureInfo("en-US"),
            TimeZone = TimeZoneInfo.Utc,
            Events = events
        };

        var script = service.CompileScript(context);

        script.Should().StartWith("#!/bin/sh");
        script.Should().Contain("FBINK=\"/mnt/us/libkh/bin/fbink\"");
        script.Should().Contain("Diner with Family");
        script.Should().NotContain("$Family");
        script.Should().Contain("TODAY LABEL");
    }
}

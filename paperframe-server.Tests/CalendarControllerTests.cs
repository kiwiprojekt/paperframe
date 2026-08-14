using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using paperframe_server.Controllers;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace paperframe_server.Tests;

public class CalendarControllerTests
{
    [Fact]
    public async Task Get_generates_scaled_shell_script_and_escapes_event_text()
    {
        var calendarService = Substitute.For<ICalendarService>();
        calendarService.GetCalendarEventsForDateRange(Arg.Any<string[]>(), Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new List<ICalendarService.CalendarEntry>
            {
                new()
                {
                    UtcDate = DateTime.UtcNow,
                    Summary = "Team \"Sync\" $HOME\nNext",
                    IsAllDayEvent = false
                }
            });
        var controller = NewController(calendarService);
        controller.Request.Headers["screen_res"] = "1516,2048";

        var script = await controller.Get("family");

        script.Should().StartWith("#!/bin/sh");
        script.Should().Contain("FBINK=\"/mnt/us/libkh/bin/fbink\"");
        script.Should().Contain("top=20");
        script.Should().Contain("Team Sync HOMENext");
        script.Should().NotContain("Team \"Sync\" $HOME");
    }

    [Fact]
    public async Task Get_throws_when_config_is_missing()
    {
        var controller = NewController(options: new AppSettings { Calendar = new() });

        var act = () => controller.Get("missing");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Layout configuration 'missing' is not defined in Calendar configs.");
    }

    private static CalendarController NewController(
        ICalendarService? calendarService = null,
        ICalendarLayoutService? calendarLayoutService = null,
        AppSettings? options = null)
    {
        var controller = new CalendarController(
            calendarService ?? Substitute.For<ICalendarService>(),
            calendarLayoutService ?? new CalendarLayoutService(),
            new TestOptionsMonitor<AppSettings>(options ?? new AppSettings
            {
                Calendar = new Dictionary<string, AppSettings.CalendarConfig>
                {
                    ["family"] = new()
                    {
                        IcalUrls = new[] { "https://calendar.example/feed.ics" },
                        CultureInfoName = "en-US",
                        TimeZoneId = "UTC",
                        HeaderToday = "Today",
                        HeaderTomorrow = "Tomorrow",
                        FbinkPath = "/mnt/us/libkh/bin/fbink",
                        FontPath = "/mnt/us/documents/Cal_Sans/CalSans-Regular.ttf"
                    }
                }
            }));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }
}

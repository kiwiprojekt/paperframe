using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using paperframe_server.Controllers;
using paperframe_server.Services;
using paperframe_server.Tests.TestSupport;

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
        var logService = Substitute.For<IPaperframeLogService>();
        var controller = NewController(calendarService, logService: logService);
        controller.Request.Headers["device_id"] = "kindle-a";
        controller.Request.Headers["battery"] = "44";

        var script = await controller.Get("family", screenRes: "1516,2048");

        script.Should().StartWith("#!/bin/sh");
        script.Should().Contain("FBINK=\"/mnt/us/libkh/bin/fbink\"");
        script.Should().Contain("top=20");
        script.Should().Contain("Team Sync HOMENext");
        script.Should().NotContain("Team \"Sync\" $HOME");
        logService.Received().LogCheckIn("kindle-a", 44, "1516,2048", "Calendar", "family", "Success", Arg.Any<string>());
    }

    [Fact]
    public async Task Get_returns_diagnostic_script_and_logs_when_device_header_is_missing()
    {
        var logService = Substitute.For<IPaperframeLogService>();
        var controller = NewController(logService: logService);

        var script = await controller.Get("family");

        script.Should().Contain("CALENDAR COMPILE ERROR");
        script.Should().Contain("Missing 'device_id' header");
        logService.Received().LogCheckIn("unknown", null, "758,1024", "Calendar", "family", "Error", Arg.Any<string>());
    }

    [Fact]
    public async Task Get_returns_diagnostic_script_when_config_is_unknown()
    {
        var logService = Substitute.For<IPaperframeLogService>();
        var controller = NewController(options: new AppSettings { Calendar = new() }, logService: logService);
        controller.Request.Headers["device_id"] = "kindle-a";

        var script = await controller.Get("missing");

        script.Should().Contain("CALENDAR COMPILE ERROR");
        script.Should().Contain("Config ID: missing");
        logService.Received().LogCheckIn("kindle-a", null, "758,1024", "Calendar", "missing", "Error", Arg.Any<string>());
    }

    private static CalendarController NewController(
        ICalendarService? calendarService = null,
        ICalendarLayoutService? calendarLayoutService = null,
        IHomeAssistantService? homeAssistantService = null,
        IPaperframeLogService? logService = null,
        AppSettings? options = null)
    {
        var controller = new CalendarController(
            calendarService ?? Substitute.For<ICalendarService>(),
            calendarLayoutService ?? new CalendarLayoutService(),
            homeAssistantService ?? Substitute.For<IHomeAssistantService>(),
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
            }),
            logService ?? Substitute.For<IPaperframeLogService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }
}

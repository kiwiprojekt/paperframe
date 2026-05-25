using FluentAssertions;
using Flurl.Http.Testing;
using paperframe_server.Services;

namespace paperframe_server.Tests;

public class CalendarServiceTests
{
    [Fact]
    public async Task GetCalendarEventsForDateRange_reads_timed_all_day_and_recurring_events()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWith("""
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Paperframe Tests//EN
            BEGIN:VEVENT
            UID:timed-1
            DTSTART:20260525T090000Z
            DTEND:20260525T100000Z
            SUMMARY:Morning Sync
            END:VEVENT
            BEGIN:VEVENT
            UID:all-day-1
            DTSTART;VALUE=DATE:20260526
            DTEND;VALUE=DATE:20260527
            SUMMARY:Holiday
            END:VEVENT
            BEGIN:VEVENT
            UID:daily-1
            DTSTART:20260525T120000Z
            DTEND:20260525T123000Z
            RRULE:FREQ=DAILY;COUNT=3
            SUMMARY:Daily Standup
            END:VEVENT
            END:VCALENDAR
            """, 200);

        var service = new CalendarService();

        var events = await service.GetCalendarEventsForDateRange(
            "https://calendar.example/feed.ics",
            new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc));

        events.Select(e => e.Summary).Should().Contain(new[]
        {
            "Morning Sync",
            "Holiday",
            "Daily Standup"
        });
        events.Where(e => e.Summary == "Daily Standup").Should().HaveCount(3);
        events.Single(e => e.Summary == "Holiday").IsAllDayEvent.Should().BeTrue();
    }

    [Fact]
    public async Task GetCalendarEventsForDateRange_excludes_end_boundary_and_deduplicates_identical_entries()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWith("""
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Paperframe Tests//EN
            BEGIN:VEVENT
            UID:duplicate-1
            DTSTART:20260525T090000Z
            DTEND:20260525T100000Z
            SUMMARY:Same Event
            END:VEVENT
            BEGIN:VEVENT
            UID:duplicate-2
            DTSTART:20260525T090000Z
            DTEND:20260525T100000Z
            SUMMARY:Same Event
            END:VEVENT
            BEGIN:VEVENT
            UID:boundary
            DTSTART:20260526T000000Z
            DTEND:20260526T010000Z
            SUMMARY:Boundary Event
            END:VEVENT
            END:VCALENDAR
            """, 200);

        var service = new CalendarService();

        var events = await service.GetCalendarEventsForDateRange(
            "https://calendar.example/feed.ics",
            new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc));

        events.Should().ContainSingle(e => e.Summary == "Same Event");
        events.Should().NotContain(e => e.Summary == "Boundary Event");
    }

    [Fact]
    public async Task GetCalendarEventsForDateRange_aggregates_multiple_feeds()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWith("""
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            UID:first
            DTSTART:20260525T090000Z
            DTEND:20260525T100000Z
            SUMMARY:First Feed
            END:VEVENT
            END:VCALENDAR
            """, 200);
        httpTest.RespondWith("""
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            UID:second
            DTSTART:20260525T100000Z
            DTEND:20260525T110000Z
            SUMMARY:Second Feed
            END:VEVENT
            END:VCALENDAR
            """, 200);

        var service = new CalendarService();

        var events = await service.GetCalendarEventsForDateRange(
            new[] { "https://calendar.example/one.ics", "https://calendar.example/two.ics" },
            new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc));

        events.Select(e => e.Summary).Should().Equal("First Feed", "Second Feed");
    }
}

using Flurl.Http;
using Ical.Net;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Options;

namespace paperframe_server.Services;

public class CalendarService : ICalendarService
{
    public async Task<List<ICalendarService.CalendarEntry>> GetCalendarEventsForDateRange(string[] icalUrls, DateTime from, DateTime to)
    {
        var events = new List<ICalendarService.CalendarEntry>();
        
        foreach (var icalUrl in icalUrls)
        {
            events.AddRange(await this.GetCalendarEventsForDateRange(icalUrl, from, to));
        }

        return events;
    }
    
    public async Task<List<ICalendarService.CalendarEntry>> GetCalendarEventsForDateRange(string icalUrl, DateTime from, DateTime to)
    {
        var icalText = await icalUrl.GetStringAsync();
        var calendar = Calendar.Load(icalText);
        
        var fromDate = new CalDateTime(from.ToUniversalTime());
        var toDate = new CalDateTime(to.ToUniversalTime());

        var seen = new HashSet<(DateTime, string)>();
        var results = new List<ICalendarService.CalendarEntry>();

        foreach (var e in calendar.Events)
        {
            if (e.RecurrenceRules.Any())
            {
                foreach (var occ in e.GetOccurrences(fromDate).TakeWhileBefore(toDate))
                {
                    var entry = new ICalendarService.CalendarEntry
                    {
                        UtcDate = occ.Period.StartTime.AsUtc,
                        Summary = e.Summary,
                        IsAllDayEvent = e.IsAllDay
                    };
                    if (seen.Add((entry.UtcDate, entry.Summary)))
                        results.Add(entry);
                }
            }
            else if (e.Start >= fromDate && e.Start < toDate)
            {
                var entry = new ICalendarService.CalendarEntry
                {
                    UtcDate = e.Start.AsUtc,
                    Summary = e.Summary,
                    IsAllDayEvent = e.IsAllDay
                };
                if (seen.Add((entry.UtcDate, entry.Summary)))
                    results.Add(entry);
            }
        }

        return results;
    }
}
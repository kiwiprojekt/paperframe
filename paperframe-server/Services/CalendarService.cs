using Flurl.Http;
using Ical.Net;
using Ical.Net.CalendarComponents;
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
        var results = new List<ICalendarService.CalendarEntry>();
        var icalText = await icalUrl.GetStringAsync();
        var calendar = Calendar.Load(icalText);
        if (calendar == null) return results;
        
        var seen = new HashSet<(DateTime, string)>();

        var occurrences = calendar.GetOccurrences(new CalDateTime(from)).TakeWhileBefore(new CalDateTime(to));
        foreach (var occ in occurrences)
        {
            if (occ.Source is not CalendarEvent e) continue;

            var entry = new ICalendarService.CalendarEntry
            {
                UtcDate = occ.Period.StartTime.AsUtc,
                Summary = e.Summary ?? string.Empty,
                IsAllDayEvent = e.IsAllDay
            };

            if (seen.Add((entry.UtcDate, entry.Summary)))
                results.Add(entry);
        }

        return results;
    }
}
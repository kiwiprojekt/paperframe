namespace paperframe_server.Services;

public interface ICalendarService
{
    Task<List<CalendarEntry>> GetCalendarEventsForDateRange(string[] icalUrls, DateTime from, DateTime to);
    
    Task<List<CalendarEntry>> GetCalendarEventsForDateRange(string icalUrl, DateTime from, DateTime to);
    
    public class CalendarEntry
    {
        public DateTime UtcDate { get; init; }
        public bool IsAllDayEvent { get; init; }
        public string Summary { get; init; } = string.Empty;
    }
}
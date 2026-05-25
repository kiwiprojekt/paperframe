using System;
using System.Collections.Generic;

namespace paperframe_server.Services;

public class CalendarLayoutContext
{
    public AppSettings.CalendarConfig Config { get; set; } = null!;
    public decimal ScaleMultiplier { get; set; }
    public DateTime ReferenceDate { get; set; }
    public System.Globalization.CultureInfo Culture { get; set; } = null!;
    public TimeZoneInfo TimeZone { get; set; } = null!;
    public List<ICalendarService.CalendarEntry> Events { get; set; } = null!;
}

public interface ICalendarLayoutService
{
    string CompileScript(CalendarLayoutContext context);
}

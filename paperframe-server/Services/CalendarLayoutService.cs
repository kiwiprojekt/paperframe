using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace paperframe_server.Services;

public class CalendarLayoutService : ICalendarLayoutService
{
    private static string ShellEscape(string input) =>
        Regex.Replace(input, @"[""\\$`!\r\n\t]", "");

    public string CompileScript(CalendarLayoutContext context)
    {
        var config = context.Config;
        var date = context.ReferenceDate;
        var culture = context.Culture;
        var timeZone = context.TimeZone;
        var m = context.ScaleMultiplier;
        var events = context.Events;

        var month = ShellEscape(date.ToString("MMMM", culture).ToUpperInvariant());
        var dayOfWeek = ShellEscape(date.ToString("dddd", culture).ToUpperInvariant());

        var dayOfWeekFontSize = dayOfWeek.Length > 7 ? 48 : 64;
        var dayOfWeekTop = (int)((dayOfWeekFontSize == 48 ? 400 : 395) * m);
        
        var fontPath = string.IsNullOrEmpty(config.FontPath) 
            ? "/mnt/us/documents/Cal_Sans/CalSans-Regular.ttf" 
            : config.FontPath;

        var documentsIndex = fontPath.IndexOf("/documents/");
        var relativePath = documentsIndex >= 0 
            ? fontPath.Substring(documentsIndex + "/documents/".Length) 
            : System.IO.Path.GetFileName(fontPath);

        var fontDir = System.IO.Path.GetDirectoryName(fontPath)?.Replace('\\', '/') ?? "/mnt/us/documents";

        var sb = new StringBuilder();
        var initAndDate = @$"#!/bin/sh
 
FBINK=""{config.FbinkPath}""
FONT={fontPath}
 
$FBINK -q -k
 
if ! test -f $FONT; then
    mkdir -p {fontDir}/
    wget $SERVICES_URL/assets/{relativePath} -O $FONT
fi
 
$FBINK -q ""{month}"" -t regular=$FONT,size=48,top={(int)(10*m)} -O -m -C GRAY9
$FBINK -q ""{date.Day}"" -t regular=$FONT,size=128,top={(int)(90*m)},right={(int)(20*m)} -O -m
$FBINK -q ""{dayOfWeek}"" -t regular=$FONT,size={dayOfWeekFontSize},top={dayOfWeekTop} -O -m -C GRAY3

";

        sb.Append(initAndDate);

        var positionTop = (int)(598*m);

        var groupedEvents = events
            .GroupBy(e => TimeZoneInfo.ConvertTimeFromUtc(e.UtcDate, timeZone).Date.DayOfYear - date.DayOfYear)
            .OrderBy(g => g.Key);
        
        foreach (var group in groupedEvents)
        {
            if (positionTop > (int)(905*m)) break;
            var header = group.Key switch
            {
                0 => config.HeaderToday,
                1 => config.HeaderTomorrow,
                _ => TimeZoneInfo.ConvertTime(group.First().UtcDate, timeZone).ToString("dddd, d MMMM", culture)
            };

            sb.AppendLine(GetEventsHeader(header, positionTop, m));
            positionTop += (int)(42*m);

            foreach (var ev in group.OrderBy(e => !e.IsAllDayEvent).ThenBy(e => e.UtcDate))
            {
                if (positionTop > (int)(986*m)) break;
                sb.AppendLine(GetEventLine(ev, positionTop, timeZone, m));
                positionTop += (int)(40*m);
            }

            positionTop += (int)(20*m);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string GetEventsHeader(string header, int top, decimal m)
        => @$"$FBINK -q ""{ShellEscape(header.ToUpperInvariant())}"" -t regular=$FONT,size=14,top={top},left={(int)(30*m)} -O";

    private string GetEventLine(ICalendarService.CalendarEntry ev, int top, TimeZoneInfo timeZone, decimal m)
        => @$"$FBINK -q ""{GetDisplayTime(ev, timeZone)}"" -t regular=$FONT,size=12,top={top},left={(int)(50*m)} -O  -C GRAY6
$FBINK -q ""{ShellEscape(ev.Summary)}"" -t regular=$FONT,size=12,top={top},left={(int)(150*m)} -O  -C GRAY3";
    
    private string GetDisplayTime(ICalendarService.CalendarEntry ev, TimeZoneInfo timeZone) 
        => ev.IsAllDayEvent ? "~~~~" : TimeZoneInfo.ConvertTime(ev.UtcDate, timeZone).ToString("HH:mm");
}

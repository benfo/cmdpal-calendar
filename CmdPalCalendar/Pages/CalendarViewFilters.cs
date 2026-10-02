using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal enum CalendarView
{
    Day,
    Schedule,
}

internal sealed partial class CalendarViewFilters : Filters
{
    private const string DayId = "day";
    private const string ScheduleId = "schedule";

    public CalendarViewFilters() => CurrentFilterId = DayId;

    public CalendarView Current => CurrentFilterId == ScheduleId ? CalendarView.Schedule : CalendarView.Day;

    public override IFilterItem[] GetFilters() =>
    [
        new Filter { Id = DayId, Name = "Day", Icon = new IconInfo("") },
        new Filter { Id = ScheduleId, Name = "Schedule", Icon = new IconInfo("") },
    ];
}

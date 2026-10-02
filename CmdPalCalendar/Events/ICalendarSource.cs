using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CmdPalCalendar.Events;

internal interface ICalendarSource
{
    bool IsStale { get; }

    IReadOnlyList<string> Errors { get; }

    Task LoadAsync(CancellationToken cancellationToken);

    IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive);
}

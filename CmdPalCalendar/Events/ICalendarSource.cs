using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CmdPalCalendar.Events;

internal interface ICalendarSource
{
    event EventHandler? Updated;

    bool IsStale { get; }

    bool HasData { get; }

    IReadOnlyList<CalendarProblem> Problems { get; }

    Task LoadAsync(CancellationToken cancellationToken);

    IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive);
}

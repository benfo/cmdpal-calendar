using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CmdPalCalendar.Events;

internal sealed class CompositeCalendarSource : ICalendarSource
{
    private readonly IReadOnlyList<ICalendarSource> _sources;

    public CompositeCalendarSource(params IReadOnlyList<ICalendarSource> sources)
    {
        _sources = sources;
        foreach (var source in _sources)
        {
            source.Updated += (_, _) => Updated?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Updated;

    public bool IsStale => _sources.Any(s => s.IsStale);

    public bool HasData => _sources.Any(s => s.HasData);

    public bool IsFetching => _sources.Any(s => s.IsFetching);

    public IReadOnlyList<CalendarProblem> Problems => _sources.SelectMany(s => s.Problems).ToList();

    public Task LoadAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_sources.Select(s => s.LoadAsync(cancellationToken)));

    public IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive) =>
        _sources
            .SelectMany(s => s.GetEntries(from, toExclusive))
            .OrderBy(e => e.Start)
            .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}

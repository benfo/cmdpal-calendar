using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class CompositeCalendarSourceTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    [Fact]
    public void Merges_entries_in_start_order()
    {
        var work = new FakeSource { Entries = [Entry("Review", 10), Entry("Standup", 9)] };
        var family = new FakeSource { Entries = [Entry("Dentist", 9, minute: 30)] };

        var entries = new CompositeCalendarSource(work, family).GetEntries(Monday, Monday.AddDays(1));

        Assert.Equal(["Standup", "Dentist", "Review"], entries.Select(e => e.Title));
    }

    [Fact]
    public void Collects_problems_from_every_source()
    {
        var work = new FakeSource { Problems = [new CalendarProblem("Work", "Offline", null)] };
        var family = new FakeSource { Problems = [new CalendarProblem("Family", "Not found", null)] };

        var problems = new CompositeCalendarSource(work, family).Problems;

        Assert.Equal(["Work", "Family"], problems.Select(p => p.Calendar));
    }

    [Fact]
    public void Is_stale_or_has_data_when_any_source_is()
    {
        var composite = new CompositeCalendarSource(new FakeSource(), new FakeSource { IsStale = true, HasData = true });

        Assert.True(composite.IsStale);
        Assert.True(composite.HasData);
        Assert.False(new CompositeCalendarSource(new FakeSource(), new FakeSource()).IsStale);
    }

    [Fact]
    public async Task Loads_every_source()
    {
        var work = new FakeSource();
        var family = new FakeSource();

        await new CompositeCalendarSource(work, family).LoadAsync(CancellationToken.None);

        Assert.Equal(1, work.Loads);
        Assert.Equal(1, family.Loads);
    }

    [Fact]
    public void Forwards_updates()
    {
        var family = new FakeSource();
        var composite = new CompositeCalendarSource(new FakeSource(), family);
        var updates = 0;
        composite.Updated += (_, _) => updates++;

        family.RaiseUpdated();

        Assert.Equal(1, updates);
    }

    private static CalendarEntry Entry(string title, int hour, int minute = 0)
    {
        var start = new DateTimeOffset(2026, 10, 5, hour, minute, 0, TimeSpan.Zero);
        return new CalendarEntry(title, title, start, start.AddMinutes(30), false, null, null, null, [], null, "Test");
    }

    private sealed class FakeSource : ICalendarSource
    {
        public event EventHandler? Updated;

        public bool IsStale { get; init; }

        public bool HasData { get; init; }

        public IReadOnlyList<CalendarProblem> Problems { get; init; } = [];

        public IReadOnlyList<CalendarEntry> Entries { get; init; } = [];

        public int Loads { get; private set; }

        public void RaiseUpdated() => Updated?.Invoke(this, EventArgs.Empty);

        public Task LoadAsync(CancellationToken cancellationToken)
        {
            Loads++;
            return Task.CompletedTask;
        }

        public IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive) => Entries;
    }
}

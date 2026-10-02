using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class CalendarRefresherTests
{
    [Fact]
    public async Task Loads_once_while_a_load_is_in_progress()
    {
        var source = new FakeSource { IsStale = true };
        var refresher = new CalendarRefresher(source);

        refresher.Refresh();
        refresher.Refresh(force: true);
        source.Release();
        await WaitUntilIdle(refresher);

        Assert.Equal(1, source.Loads);
    }

    [Fact]
    public async Task Skips_fresh_data_unless_forced()
    {
        var source = new FakeSource { IsStale = false };
        var refresher = new CalendarRefresher(source);

        refresher.Refresh();
        Assert.False(refresher.IsLoading);

        refresher.Refresh(force: true);
        source.Release();
        await WaitUntilIdle(refresher);

        Assert.Equal(1, source.Loads);
    }

    [Fact]
    public async Task Reports_loading_start_and_end()
    {
        var source = new FakeSource { IsStale = true };
        var refresher = new CalendarRefresher(source);
        var states = new List<bool>();
        refresher.LoadingChanged += (_, _) => states.Add(refresher.IsLoading);

        refresher.Refresh();
        source.Release();
        await WaitUntilIdle(refresher);

        Assert.Equal([true, false], states);
    }

    private static async Task WaitUntilIdle(CalendarRefresher refresher)
    {
        for (var i = 0; i < 100 && refresher.IsLoading; i++)
        {
            await Task.Delay(10);
        }
    }

    private sealed class FakeSource : ICalendarSource
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _loads;

        public event EventHandler? Updated
        {
            add { }
            remove { }
        }

        public bool IsStale { get; init; }

        public bool HasData => false;

        public int Loads => _loads;

        public IReadOnlyList<string> Errors => [];

        public void Release() => _release.TrySetResult();

        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _loads);
            await _release.Task;
        }

        public IReadOnlyList<CalendarEntry> GetEntries(DateOnly from, DateOnly toExclusive) => [];
    }
}

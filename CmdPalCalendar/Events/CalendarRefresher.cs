using System;
using System.Threading;
using System.Threading.Tasks;

namespace CmdPalCalendar.Events;

internal sealed class CalendarRefresher(ICalendarSource source)
{
    private readonly Lock _lock = new();
    private bool _isLoading;

    public event EventHandler? LoadingChanged;

    public bool IsLoading => _isLoading;

    public void Refresh(bool force = false)
    {
        lock (_lock)
        {
            if (_isLoading || (!force && !source.IsStale))
            {
                return;
            }

            _isLoading = true;
        }

        LoadingChanged?.Invoke(this, EventArgs.Empty);
        _ = Task.Run(LoadAsync);
    }

    private async Task LoadAsync()
    {
        try
        {
            await source.LoadAsync(CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
            LoadingChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

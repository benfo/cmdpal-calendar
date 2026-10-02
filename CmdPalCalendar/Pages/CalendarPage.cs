using System;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class CalendarPage : ListPage
{
    private readonly CalendarSettings _settings;
    private readonly ICalendarSource _source;
    private readonly TimeProvider _time;
    private readonly CalendarLayout _layout;
    private readonly Lock _lock = new();
    private Task? _loading;
    private DateOnly? _selectedDate;

    public CalendarPage(CalendarSettings settings, ICalendarSource source, TimeProvider time)
    {
        _settings = settings;
        _source = source;
        _time = time;
        _layout = new CalendarLayout(new CalendarRows(new NavigationCommands(
            previous: () => GoTo(SelectedDate.AddDays(-1)),
            next: () => GoTo(SelectedDate.AddDays(1)),
            today: () => GoTo(Today),
            refresh: () => LoadIfStale(force: true))));

        _settings.Settings.SettingsChanged += (_, _) => LoadIfStale(force: true);

        Id = "CmdPalCalendar.Calendar";
        Icon = new IconInfo("");
        Name = "Open";
        PlaceholderText = "Filter events";
        ShowDetails = true;
        UpdateTitle();
    }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    private DateOnly SelectedDate => _selectedDate ?? Today;

    public override IListItem[] GetItems()
    {
        if (_settings.IcsFeeds.Count == 0)
        {
            return [CalendarRows.Settings(_settings.Settings.SettingsPage)];
        }

        LoadIfStale();
        UpdateTitle();

        var date = SelectedDate;
        var entries = _source.GetEntries(date, date.AddDays(1));
        var errors = _source.Errors;

        return entries.Count == 0 && errors.Count == 0 && IsLoading
            ? []
            : _layout.Day(date, entries, errors, _time.GetLocalNow());
    }

    private void GoTo(DateOnly date)
    {
        _selectedDate = date == Today ? null : date;
        UpdateTitle();
        RaiseItemsChanged();
    }

    private void UpdateTitle() => Title = DateText.Title(SelectedDate, Today);

    private void LoadIfStale(bool force = false)
    {
        lock (_lock)
        {
            if (_loading is { IsCompleted: false } || (!force && !_source.IsStale))
            {
                return;
            }

            IsLoading = true;
            _loading = Task.Run(LoadAsync);
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            await _source.LoadAsync(CancellationToken.None);
        }
        finally
        {
            IsLoading = false;
            RaiseItemsChanged();
        }
    }
}

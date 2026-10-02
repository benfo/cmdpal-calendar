using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class CalendarPage : DynamicListPage
{
    private readonly CalendarSettings _settings;
    private readonly ICalendarSource _source;
    private readonly TimeProvider _time;
    private readonly CalendarRows _rows;
    private readonly CalendarLayout _layout;
    private readonly Lock _lock = new();
    private Task? _loading;
    private DateOnly? _selectedDate;

    public CalendarPage(CalendarSettings settings, ICalendarSource source, TimeProvider time)
    {
        _settings = settings;
        _source = source;
        _time = time;
        _rows = new CalendarRows(new NavigationCommands(
            previous: () => GoTo(SelectedDate.AddDays(-1)),
            next: () => GoTo(SelectedDate.AddDays(1)),
            today: () => GoTo(Today),
            refresh: () => LoadIfStale(force: true)));
        _layout = new CalendarLayout(_rows);

        _settings.Settings.SettingsChanged += (_, _) => LoadIfStale(force: true);

        Id = "CmdPalCalendar.Calendar";
        Icon = new IconInfo("");
        Name = "Open";
        PlaceholderText = "Filter events, or type a date (tomorrow, next fri, 27 jan)";
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
        var entries = EntriesOn(date);
        var errors = _source.Errors;

        return entries.Count == 0 && errors.Count == 0 && IsLoading
            ? []
            : _layout.Day(new DayContent(date, entries, errors, _time.GetLocalNow(), SearchText, GoToRow()));
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    private ListItem? GoToRow() =>
        DateQuery.TryParse(SearchText, Today, out var date)
            ? _rows.GoTo(date, EntriesOn(date).Count, () => GoTo(date, clearSearch: true))
            : null;

    private IReadOnlyList<CalendarEntry> EntriesOn(DateOnly date) => _source.GetEntries(date, date.AddDays(1));

    private void GoTo(DateOnly date, bool clearSearch = false)
    {
        _selectedDate = date == Today ? null : date;
        if (clearSearch)
        {
            SetSearchNoUpdate(string.Empty);
            OnPropertyChanged(nameof(SearchText));
        }

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

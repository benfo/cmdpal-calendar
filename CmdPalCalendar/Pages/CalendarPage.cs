using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class CalendarPage : DynamicListPage
{
    private const int ScheduleDays = 7;

    private readonly CalendarSettings _settings;
    private readonly ICalendarSource _source;
    private readonly TimeProvider _time;
    private readonly NavigationCommands _navigation;
    private readonly CalendarViewFilters _views = new();
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
        _navigation = new NavigationCommands(
            previous: () => GoTo(SelectedDate.AddDays(-DaysShown)),
            next: () => GoTo(SelectedDate.AddDays(DaysShown)),
            today: () => GoTo(Today),
            refresh: () => LoadIfStale(force: true));
        _rows = new CalendarRows(_navigation);
        _layout = new CalendarLayout(_rows);

        _settings.Settings.SettingsChanged += (_, _) => LoadIfStale(force: true);
        _views.PropChanged += (_, _) => OnViewChanged();
        Filters = _views;

        Id = "CmdPalCalendar.Calendar";
        Icon = new IconInfo(Glyphs.Calendar);
        Name = "Open";
        PlaceholderText = "Filter events, or type a date (tomorrow, next fri, 27 jan)";
        ShowDetails = true;
        UpdateTitle();
    }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    private DateOnly SelectedDate => _selectedDate ?? Today;

    private int DaysShown => _views.Current == CalendarView.Schedule ? ScheduleDays : 1;

    public override IListItem[] GetItems()
    {
        if (_settings.IcsFeeds.Count == 0)
        {
            return [CalendarRows.Settings(_settings.Settings.SettingsPage)];
        }

        LoadIfStale();
        UpdateTitle();

        var days = Enumerable.Range(0, DaysShown)
            .Select(i => SelectedDate.AddDays(i))
            .Select(d => new DayEntries(d, EntriesOn(d)))
            .ToList();
        var errors = _source.Errors;

        return days.All(d => d.Entries.Count == 0) && errors.Count == 0 && IsLoading
            ? []
            : _layout.Build(new CalendarContent(days, errors, _time.GetLocalNow(), SearchText, GoToRow()));
    }

    private void OnViewChanged()
    {
        _navigation.StepBy(_views.Current == CalendarView.Schedule ? "week" : "day");
        UpdateTitle();
        RaiseItemsChanged();
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

    private void UpdateTitle() =>
        Title = DaysShown == 1
            ? DateText.Title(SelectedDate, Today)
            : DateText.Span(SelectedDate, SelectedDate.AddDays(DaysShown - 1));

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

using System;
using System.Collections.Generic;
using System.Linq;
using CmdPalCalendar.Events;
using CmdPalCalendar.Feeds;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class CalendarPage : DynamicListPage
{
    public const string DefaultId = "CmdPalCalendar.Calendar";
    public const int LookAheadDays = 60;
    private const int ScheduleDays = 7;

    private readonly CalendarFeedStore _feeds;
    private readonly ICalendarSource _source;
    private readonly CalendarRefresher _refresher;
    private readonly TimeProvider _time;
    private readonly NavigationCommands _navigation;
    private readonly CalendarViewFilters _views = new();
    private readonly CalendarRows _rows;
    private readonly CalendarLayout _layout;
    private DateOnly? _selectedDate;

    public CalendarPage(
        CalendarFeedStore feeds,
        ICalendarSource source,
        CalendarRefresher refresher,
        ManageCalendarsPage manage,
        TimeProvider time,
        string id = DefaultId)
    {
        _feeds = feeds;
        _source = source;
        _refresher = refresher;
        _time = time;
        _navigation = new NavigationCommands(
            previous: () => GoTo(SelectedDate.AddDays(-DaysShown)),
            next: () => GoTo(SelectedDate.AddDays(DaysShown)),
            today: () => GoTo(Today),
            refresh: () => _refresher.Refresh(force: true));
        _rows = new CalendarRows(_navigation, manage);
        _layout = new CalendarLayout(_rows);

        _refresher.LoadingChanged += (_, _) => OnLoadingChanged();
        _feeds.Changed += (_, _) => OnFeedsChanged();
        _views.PropChanged += (_, _) => OnViewChanged();
        Filters = _views;

        Id = id;
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
        if (_feeds.EnabledFeeds.Count == 0)
        {
            return [_rows.NoCalendars(allTurnedOff: _feeds.Feeds.Count > 0)];
        }

        _refresher.Refresh();
        UpdateTitle();

        var days = Enumerable.Range(0, DaysShown)
            .Select(i => SelectedDate.AddDays(i))
            .Select(d => new DayEntries(d, EntriesOn(d)))
            .ToList();
        var errors = _source.Errors;

        return days.All(d => d.Entries.Count == 0) && errors.Count == 0 && IsLoading
            ? []
            : _layout.Build(new CalendarContent(
                days, errors, _time.GetLocalNow(), SearchText, GoToRow(), NextAfter(days[^1].Date), date => GoTo(date)));
    }

    private CalendarEntry? NextAfter(DateOnly date) =>
        _source.GetEntries(date.AddDays(1), date.AddDays(1 + LookAheadDays))
            .Where(e => DateOnly.FromDateTime(e.Start.DateTime) > date)
            .OrderBy(e => e.Start.Date)
            .ThenBy(e => e.IsAllDay)
            .ThenBy(e => e.Start)
            .FirstOrDefault();

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

    private void OnFeedsChanged()
    {
        _refresher.Refresh();
        RaiseItemsChanged();
    }

    private void OnLoadingChanged()
    {
        IsLoading = _refresher.IsLoading;
        if (!IsLoading)
        {
            RaiseItemsChanged();
        }
    }
}

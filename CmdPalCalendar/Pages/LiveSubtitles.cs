using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed class LiveSubtitles
{
    private readonly Lock _lock = new();
    private List<(ListItem Item, CalendarEntry Entry)> _rows = [];

    public IReadOnlyList<CalendarEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _rows.Select(r => r.Entry).ToList();
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _rows = [];
        }
    }

    public void Track(ListItem item, CalendarEntry entry)
    {
        lock (_lock)
        {
            _rows.Add((item, entry));
        }
    }

    public void Update(Func<CalendarEntry, string> subtitle)
    {
        List<(ListItem Item, CalendarEntry Entry)> rows;
        lock (_lock)
        {
            rows = [.. _rows];
        }

        foreach (var (item, entry) in rows)
        {
            item.Subtitle = subtitle(entry);
        }
    }
}

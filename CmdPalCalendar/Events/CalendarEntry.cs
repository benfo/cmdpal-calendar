using System;
using System.Collections.Generic;

namespace CmdPalCalendar.Events;

internal sealed record CalendarEntry(
    string Uid,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool IsAllDay,
    string? Location,
    string? Description,
    string? Organizer,
    IReadOnlyList<string> Attendees,
    string? Link,
    string Source);

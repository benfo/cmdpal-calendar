// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace CmdPalCalendar.Calendar;

/// <summary>
/// One occurrence of an event on today's calendar, normalized from its source.
/// </summary>
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

/// <summary>
/// Result of loading every configured feed: the merged entries plus any per-feed failures.
/// </summary>
internal sealed record CalendarLoadResult(
    IReadOnlyList<CalendarEntry> Entries,
    IReadOnlyList<string> Errors,
    DateTimeOffset LoadedAt);

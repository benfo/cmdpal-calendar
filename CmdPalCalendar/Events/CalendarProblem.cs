using System;

namespace CmdPalCalendar.Events;

internal sealed record CalendarProblem(string Calendar, string Message, DateTimeOffset? ShowingCopyFrom);

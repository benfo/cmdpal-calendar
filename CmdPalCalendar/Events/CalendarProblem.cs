using System;

namespace CmdPalCalendar.Events;

internal sealed record CalendarProblem(string Calendar, string Message, DateTimeOffset? ShowingCopyFrom, AccountSignIn? SignIn = null);

internal sealed record AccountSignIn(string AccountId, string Account);

using CmdPalCalendar.Events;

namespace CmdPalCalendar.Pages;

internal static class MeetingServiceText
{
    public static string? Name(MeetingService service) => service switch
    {
        MeetingService.Teams => "Teams",
        MeetingService.Zoom => "Zoom",
        MeetingService.Meet => "Google Meet",
        MeetingService.Webex => "Webex",
        _ => null,
    };

    public static string JoinLabel(MeetingService service) => service switch
    {
        MeetingService.Other => "Open link",
        MeetingService.Meet => "Join Google Meet",
        _ => $"Join {Name(service)} meeting",
    };
}

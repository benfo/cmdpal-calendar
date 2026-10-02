using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace CmdPalCalendar.Pages;

internal sealed partial class JoinMeetingCommand : InvokableCommand
{
    private readonly MeetingLink _link;

    public JoinMeetingCommand(MeetingLink link)
    {
        _link = link;
        Name = MeetingServiceText.JoinLabel(link.Service);
        Icon = new IconInfo(link.Service == MeetingService.Other ? Glyphs.Link : Glyphs.Video);
    }

    public override ICommandResult Invoke()
    {
        _ = LaunchAsync(_link);
        return CommandResult.Dismiss();
    }

    private static async Task LaunchAsync(MeetingLink link)
    {
        if (link.App is not { } app || !await TryLaunchAppAsync(app, link.Web))
        {
            await Launcher.LaunchUriAsync(link.Web);
        }
    }

    private static async Task<bool> TryLaunchAppAsync(Uri app, Uri web)
    {
        try
        {
            return await Launcher.QueryUriSupportAsync(app, LaunchQuerySupportType.Uri) == LaunchQuerySupportStatus.Available &&
                await Launcher.LaunchUriAsync(app, new LauncherOptions { FallbackUri = web });
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            return false;
        }
    }
}

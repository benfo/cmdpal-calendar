using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace CmdPalCalendar.Pages;

internal sealed partial class OpenInBrowserCommand(Uri address) : InvokableCommand
{
    public override ICommandResult Invoke()
    {
        // Apps such as the new Outlook and Teams claim their web addresses, which makes Windows ask which app to use.
        _ = Launcher.LaunchUriAsync(address, new LauncherOptions { IgnoreAppUriHandlers = true });
        return CommandResult.Dismiss();
    }
}

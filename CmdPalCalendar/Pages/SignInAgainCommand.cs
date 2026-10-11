using System;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Events;
using CmdPalCalendar.Graph;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class SignInAgainCommand : InvokableCommand
{
    private readonly IMicrosoftSignIn _signIn;
    private readonly AccountSignIn _account;
    private readonly Action _refresh;

    public SignInAgainCommand(IMicrosoftSignIn signIn, AccountSignIn account, Action refresh)
    {
        _signIn = signIn;
        _account = account;
        _refresh = refresh;
        Name = "Sign in";
        Icon = new IconInfo(Glyphs.Contact);
    }

    public override ICommandResult Invoke()
    {
        _ = Task.Run(SignInAsync);
        return CommandResult.KeepOpen();
    }

    private async Task SignInAsync()
    {
        if (await _signIn.SignInAsync(_account.Account, CancellationToken.None) is not null)
        {
            _refresh();
        }
    }
}

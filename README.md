# Calendar for Command Palette

A [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) extension that shows your meetings day by day and, eventually, joins them in one action, like Slack's Google Calendar integration.

Open Command Palette and pick **Calendar**. It opens on today, grouped into **Happening now**, **Up next**, **All day** and **Earlier today**, and the details pane shows the organizer, the attendees and the start of the description. Enter joins the meeting in Teams or Zoom (or opens the link in your browser for other services).

- **Move between days:** Ctrl+← and Ctrl+→ (by a week in Schedule view), Ctrl+T for today.
- **Jump to a date:** type it in the search box, Todoist style (`tomorrow`, `next fri`, `in 2 weeks`, `27 jan`, `2026-10-12`), then press Enter on **Go to…**. Other text filters the events.
- **Views:** the dropdown next to the search box switches between **Day** and **Schedule** (seven days with a heading per day).
- **Empty days** point to the next event; press Enter to jump there.
- Ctrl+K also has **Join in browser**, **Copy link**, **Copy meeting ID and passcode** and **Refresh** (Ctrl+R).
- **Join next meeting:** a separate command that joins the meeting on now or next, counting only meetings with a Teams, Zoom, Meet or Webex link. It joins straight away from 15 minutes before the start and asks first if it's later. With nothing to join it opens the calendar. Its subtitle shows what it will join ("Standup in 4 min · Teams"). Give it a hotkey in Command Palette settings to join with one key press.

## Install

You need [PowerToys](https://aka.ms/installpowertoys) with Command Palette. Then, in PowerShell (it asks for administrator rights, and continues in an administrator window):

```powershell
irm https://github.com/benfo/cmdpal-calendar/releases/latest/download/install.ps1 | iex
```

It downloads the latest release for your PC (x64 or ARM64), trusts its signing certificate (a self-signed `CN=ben.fourie` certificate, until the extension is in the Microsoft Store), and installs it. Then open Command Palette and run **Reload**.

To update, run the same command again. To uninstall: `Get-AppxPackage CmdPalCalendar | Remove-AppxPackage`.

## Setup

Press Ctrl+K on **Calendar** and choose **Manage calendars**, then **Add a calendar**. Enter an `https://` or `webcal://` address, or the path to a local `.ics` file. The form checks the calendar before saving, and fills in its name if you leave it blank. Each calendar has a name and a colour (shown as a tag on its events) and can be turned off without removing it. The list is saved in `calendars.json` in the extension's settings folder, with each calendar's address encrypted for your Windows account (published-calendar addresses work like passwords).

Calendars refresh in the background every 5 minutes. The last downloaded copy of each one is kept in the `cache` folder next to it, encrypted for your Windows account, so events show as soon as Command Palette starts and stay visible when a calendar can't be reached ("Problems" then says how old the copy is).

- **Outlook:** Settings > Calendar > Shared calendars > Publish a calendar. To get join links, publish with all details. Work tenants may block publishing.
- **Google:** Settings > your calendar > Integrate calendar > Secret address in iCal format. It can lag by a few hours.
- **Try it without a calendar:** use `samples/sample.ics`, which has events on weekdays.

## Plan

The extension starts with ICS feeds. The next steps are:
- a Dock item that counts down to the next meeting ("Standup · 7m") and joins it when clicked;
- Microsoft 365 and Google Calendar accounts, merged into one view.

See [BACKLOG.md](BACKLOG.md) for the details.

## Development

You only need the .NET 10 SDK. The Windows SDK files come from the `Microsoft.Windows.SDK.CPP` NuGet package.

1. Turn on **Developer Mode** (Settings > System > For developers).
2. Run `./deploy.ps1`. It builds the extension and registers it with `Add-AppxPackage -Register`.
3. In Command Palette, run **Reload**.

Run the tests with `dotnet test --project tests/CmdPalCalendar.Tests`. GitHub Actions builds and tests every push to `main`.

## Releasing

Push a version tag: `git tag v0.1.1 && git push origin v0.1.1`. The release workflow stamps the version into the package, builds and signs x64 and ARM64 packages with `package.ps1`, and publishes a GitHub release with them, the public certificate and `install.ps1`. Signing uses the `SIGNING_CERTIFICATE` (base64 `.pfx`) and `SIGNING_PASSWORD` repository secrets.

# Calendar for Command Palette

A [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) extension that shows your meetings day by day and, eventually, joins them in one action, like Slack's Google Calendar integration.

Open Command Palette and pick **Calendar**. It opens on today, grouped into **Happening now**, **Up next**, **All day** and **Earlier today**, and the details pane shows the organizer, the attendees and the start of the description. Enter joins the meeting in Teams or Zoom (or opens the link in your browser for other services).

- **Move between days:** Ctrl+← and Ctrl+→ (by a week in Schedule view), Ctrl+T for today.
- **Jump to a date:** type it in the search box, Todoist style (`tomorrow`, `next fri`, `in 2 weeks`, `27 jan`, `2026-10-12`), then press Enter on **Go to…**. Other text filters the events.
- **Views:** the dropdown next to the search box switches between **Day** and **Schedule** (seven days with a heading per day).
- **Empty days** point to the next event; press Enter to jump there.
- Ctrl+K also has **Join in browser**, **Copy link**, **Copy meeting ID and passcode** and **Refresh** (Ctrl+R).

## Setup

In the extension's settings, add one ICS feed per line: an `https://` or `webcal://` URL, or a path to a local `.ics` file.

- **Outlook:** Settings > Calendar > Shared calendars > Publish a calendar. To get join links, publish with all details. Work tenants may block publishing.
- **Google:** Settings > your calendar > Integrate calendar > Secret address in iCal format. It can lag by a few hours.
- **Try it without a calendar:** use `samples/sample.ics`, which has events on weekdays.

## Plan

The extension starts with ICS feeds. The next steps are:
- a Dock item that counts down to the next meeting ("Standup · 7m") and joins it when clicked;
- Microsoft 365 and Google Calendar accounts, merged into one view.

See [BACKLOG.md](BACKLOG.md) for the details.

## Build & install

You only need the .NET 10 SDK. The Windows SDK files come from the `Microsoft.Windows.SDK.CPP` NuGet package.

1. Turn on **Developer Mode** (Settings > System > For developers).
2. Run `./deploy.ps1`. It builds the extension and registers it with `Add-AppxPackage -Register`.
3. In Command Palette, run **Reload**.

Run the tests with `dotnet test --project tests/CmdPalCalendar.Tests`.

# Calendar for Command Palette

A [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) extension that shows the rest of today's meetings and, eventually, joins them in one action, like Slack's Google Calendar integration.

Open Command Palette and pick **Today's calendar**. Events are grouped into **Happening now**, **Up next**, **All day** and **Earlier today**, and the details pane shows the organizer, the attendees and the start of the description. Enter opens the meeting link. Ctrl+K has **Copy link** and **Refresh** (Ctrl+R).

## Setup

In the extension's settings, add one ICS feed per line: an `https://` or `webcal://` URL, or a path to a local `.ics` file.

- **Outlook:** Settings > Calendar > Shared calendars > Publish a calendar. To get join links, publish with all details. Work tenants may block publishing.
- **Google:** Settings > your calendar > Integrate calendar > Secret address in iCal format. It can lag by a few hours.
- **Try it without a calendar:** use `samples/sample.ics`, which has events every day.

## Plan

The extension starts with ICS feeds. The next steps are:
- joining meetings directly in Teams or Zoom instead of the browser;
- a Dock item that counts down to the next meeting ("Standup · 7m") and joins it when clicked;
- Microsoft 365 and Google Calendar accounts, merged into one view.

See [BACKLOG.md](BACKLOG.md) for the details.

## Build & install

You only need the .NET 10 SDK. The Windows SDK files come from the `Microsoft.Windows.SDK.CPP` NuGet package.

1. Turn on **Developer Mode** (Settings > System > For developers).
2. Run `./deploy.ps1`. It builds the extension and registers it with `Add-AppxPackage -Register`.
3. In Command Palette, run **Reload**.

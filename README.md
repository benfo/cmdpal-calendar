# Calendar for Command Palette

A [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) extension that shows your meetings day by day and, eventually, joins them in one action, like Slack's Google Calendar integration.

Open Command Palette and pick **Calendar**. It opens on today, grouped into **Happening now**, **Up next**, **All day** and **Earlier today**, and the details pane shows the organizer, the attendees and the start of the description. Enter joins the meeting in Teams or Zoom (or opens the link in your browser for other services). For a meeting more than 15 minutes away, or one that has ended, it asks first.

- **Move between days:** Ctrl+← and Ctrl+→ (by a week in Schedule view), Ctrl+T for today.
- **Jump to a date:** type it in the search box, Todoist style (`tomorrow`, `next fri`, `in 2 weeks`, `27 jan`, `2026-10-12`), then press Enter on **Go to…**. Other text filters the events.
- **Views:** the dropdown next to the search box switches between **Day** and **Schedule** (seven days with a heading per day).
- **Empty days** point to the next event; press Enter to jump there.
- **Open in Outlook or Google Calendar:** Enter on an event with nothing to join opens it on the web, and Ctrl+Enter does the same on a meeting. Microsoft 365 events open the event itself in Outlook. For published calendars, Google Calendar opens at that day; Outlook has no link to a date, so it opens the day view.
- Ctrl+K also has **Join in browser** (Ctrl+B), **Copy link**, **Copy meeting ID and passcode**, **Copy event** and **Refresh** (Ctrl+R).
- **Join next meeting:** a separate command that joins the meeting on now or next, counting only meetings with a Teams, Zoom, Meet or Webex link. It joins straight away from 15 minutes before the start and asks first if it's later. With nothing to join it opens the calendar. Its subtitle shows what it will join ("Standup in 4 min · Teams"). Give it a hotkey in Command Palette settings to join with one key press.

## Install

You need [PowerToys](https://aka.ms/installpowertoys) with Command Palette. Then, in PowerShell (it asks for administrator rights, and continues in an administrator window):

```powershell
irm https://github.com/benfo/cmdpal-calendar/releases/latest/download/install.ps1 | iex
```

It downloads the latest release for your PC (x64 or ARM64), trusts its signing certificate (a self-signed `CN=ben.fourie` certificate, until the extension is in the Microsoft Store), and installs it. Then open Command Palette and run **Reload**.

To update, run the same command again. To uninstall: `Get-AppxPackage CmdPalCalendar | Remove-AppxPackage`.

## Setup

Press Ctrl+K on **Calendar** and choose **Manage calendars**, then **Add a calendar**, and choose where the calendar comes from:

- **Microsoft 365 or Outlook.com:** sign in with the Windows account picker (your work account is one click on a work PC; a second account gets a fresh sign-in), then switch on the calendars you want and press **Save**. Names and colours come from Outlook. This works even where your organisation blocks publishing calendars, and gives the full event details and join links.
- **Calendar address or file:** an `https://` or `webcal://` address, or the path to a local `.ics` file. The form checks the calendar before saving and uses its own name.
  - **Outlook:** Settings > Calendar > Shared calendars > Publish a calendar. To get join links, publish with all details. Work tenants may block publishing; use Microsoft 365 instead.
  - **Google:** Settings > your calendar > Integrate calendar > Secret address in iCal format. It can lag by a few hours.
  - **Try it without a calendar:** use `samples/sample.ics`, which has events on weekdays.

In Manage calendars, every calendar works the same way: Enter edits its name, colour (shown as a tag on its events) and whether it's shown. Ctrl+K starts with where it comes from (**Change address…**, or **Choose calendars for *account*…**), then **Turn off**, **Remove** and, for Microsoft calendars, **Sign out**. Removing an account's last calendar signs it out.

Calendars refresh in the background every 5 minutes (Microsoft calendars fetch only what changed). The last copy of each is kept in the `cache` folder, so events show as soon as Command Palette starts and stay visible when a calendar can't be reached ("Problems" then says how old the copy is). If a Microsoft sign-in expires, Problems shows **Sign in again to *account***; press Enter. After the Windows sign-in window closes, click Command Palette once to carry on; Windows doesn't hand the focus back.

Everything is stored in the extension's settings folder and encrypted for your Windows account: `calendars.json` (published-calendar addresses work like passwords), `msal.cache.bin` (which Microsoft accounts you signed in with; Windows keeps the sign-in itself) and `cache/`.

### Work accounts and admin approval

Work accounts usually need your IT admin to approve the app once: Microsoft's default settings don't let people give third-party apps access to their calendar themselves. Outlook.com accounts sign in straight away.

- If the sign-in shows **Approval required**, add a short reason and select **Request approval**. Your admin sees it in Microsoft Entra under **Enterprise applications → Admin consent requests**. Once approved, sign in again.
- If it shows **Need admin approval** without a request button, send your admin this link. Opening it lets an admin approve the app for the whole organisation, then returns to this page:
  `https://login.microsoftonline.com/organizations/v2.0/adminconsent?client_id=2c35b16d-5b5c-427e-bb0b-d51aeafaa3e8&scope=https://graph.microsoft.com/Calendars.Read&redirect_uri=https://github.com/benfo/cmdpal-calendar`

The app only asks to read calendars (`Calendars.Read`); it can't change anything, and your data never leaves your PC except to and from Microsoft. Microsoft lists the publisher as unverified because it's published by an individual. See [PRIVACY.md](PRIVACY.md) for exactly what it reads and stores.

## Plan

The extension reads published calendars (ICS) and Microsoft 365 / Outlook.com accounts. The next steps are:
- a release with Microsoft 365 and Outlook.com;
- a Dock item that counts down to the next meeting ("Standup · 7m") and joins it when clicked;
- Google Calendar accounts.

See [BACKLOG.md](BACKLOG.md) for the details.

## Development

You only need the .NET 10 SDK. The Windows SDK files come from the `Microsoft.Windows.SDK.CPP` NuGet package.

1. Turn on **Developer Mode** (Settings > System > For developers).
2. Run `./deploy.ps1`. It builds the extension and registers it with `Add-AppxPackage -Register`.
3. In Command Palette, run **Reload**.

Run the tests with `dotnet test --project tests/CmdPalCalendar.Tests`. Release builds are trimmed; `dotnet publish tests/CmdPalCalendar.TrimCheck -c Release -o <dir>` builds a trimmed check that parses the ICS and Graph fixtures in `tests/fixtures`. GitHub Actions builds and tests every push to `main`.

## Releasing

Push a version tag: `git tag v0.1.1 && git push origin v0.1.1`. The release workflow stamps the version into the package, builds and signs x64 and ARM64 packages with `package.ps1`, and publishes a GitHub release with them, the public certificate and `install.ps1`. Signing uses the `SIGNING_CERTIFICATE` (base64 `.pfx`) and `SIGNING_PASSWORD` repository secrets.

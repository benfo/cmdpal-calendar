# Backlog

Ordered roughly by priority. Move items to **Done** when they ship. Background, research and the longer-term design are in [PLAN.md](PLAN.md).

## Next up

- [ ] **Fixed command ID**: give "Today's calendar" a stable ID (e.g. `CmdPalCalendar.Today`) so pinning to home, an alias (such as `cal`) or a global hotkey keeps working across updates.
- [ ] **Join meetings in the app**: Enter on a meeting opens Teams or Zoom directly instead of the browser.
  - Find the link: location first, then the description. Unwrap Outlook Safe Links, `google.com/url?q=` and Proofpoint wrappers before matching.
  - Teams: swap `https` for `msteams` and keep the host and path, so `/l/meetup-join/`, the newer `/meet/<id>?p=` short links, `teams.live.com` and the gov clouds all work.
  - Zoom: `https://<host>/j/<id>?pwd=<pwd>` becomes `zoommtg://<host>/join?action=join&confno=<id>&pwd=<pwd>`. `/my/` personal rooms, `/w/` webinars and register links open in the browser (`zoommtg` can't join them).
  - Launch with `Launcher.LaunchUriAsync(native, new LauncherOptions { FallbackUri = https })`, after checking `QueryUriSupportAsync` (registry checks miss the MSIX Teams and Store Slack). If the app isn't installed the browser opens with no "How do you want to open this?" prompt.
  - Ctrl+K: join in browser, copy join link, copy meeting ID and passcode, copy dial-in.
  - Meeting service icon on each row (Teams, Zoom, Meet, generic) instead of the calendar glyph.
  - Tests with real (scrubbed) invite bodies.
- [ ] **Times that tick**: "started 6 min ago" and "in 7 min" update while the page is open, and meetings move from "Up next" to "Happening now" without a refresh. Also count a meeting starting in the next few minutes as "Happening now", like Slack does.

## Later

- [ ] **Next meeting on the Dock**: a Dock band showing "Standup · 7m", then "Standup · now"; clicking it joins. Ticks locally from the cache. Copy the timer pattern of the built-in Time and Date band (only runs while the Dock is showing it). Watch PowerToys issue #50367: bands can go dead after an RDP or session switch.
- [ ] **Next meeting on the command**: the top-level subtitle reads "Next: Standup in 7m", and a separate "Join next meeting" command (with its own fixed ID, so it can have a hotkey).
- [ ] **Named feeds**: give each feed a name and colour, shown as a tag on its events, instead of the feed's host name. Probably an "Add ICS feed" form rather than one text box.
- [ ] **Keep feed URLs secret**: published-calendar URLs work like passwords, so store them encrypted (DPAPI) or in Credential Manager instead of plain `settings.json`.
- [ ] **Response status**: hide meetings you declined and tag tentative or unanswered ones. ICS only has this per attendee, so it needs your email address(es) as a setting to know which attendee is you.
- [ ] **Background refresh and instant open**: refresh every few minutes in the background and keep the last result on disk, so the page and Dock show data immediately when Command Palette starts.
- [ ] **Microsoft 365 (Graph)**: read the work calendar directly, which gets proper join links, attendees and response status. Blocked on consent: the work tenant's default policy (since about July 2026) stops users consenting to `Calendars.Read`, so it needs admin consent. Test that first; the ICS feed is the fallback. Details in PLAN.md §6.
- [ ] **Google Calendar**: personal and Workspace accounts with the desktop loopback + PKCE sign-in. Set the Google project to "In production" (unverified) to avoid Testing mode's weekly token expiry; a Workspace admin may need to allow the app.
- [ ] **Merge calendars**: when the same meeting is on two calendars, show it once with both calendars' tags. Match on start and end plus the same join link, iCal UID or title (Graph gives each occurrence its own UID, so the UID alone isn't enough).

## Ideas

- [ ] **Tomorrow when today is done**: after the last meeting, show tomorrow's first one ("Tomorrow 09:00 · Standup") instead of an empty page.
- [ ] **Meeting reminders**: a Windows notification a minute before with a Join button. Needs the Windows App SDK and a notification activator; Command Palette's own toasts only show inside its window. Outlook and Teams probably remind you already, so maybe not worth it.
- [ ] **Choose calendars per account**: once Graph or Google are in, pick which calendars to include (primary only by default).

## Engineering

- [ ] **Development setup**: move the calendar code into `CmdPalCalendar.Core`, add xUnit tests for link detection, recurrence and the now/next/all-day split (with a fixed clock, across DST changes).
- [ ] **Release build check**: publish trimmed and fix any Ical.Net trimming warnings, or swap to a small hand-written VEVENT parser.
- [ ] **Logging**: a log file next to the settings with load timings and per-feed errors, without event contents or feed URLs.
- [ ] **Deploy on build**: register the package after `dotnet build`, so the loop is build then Reload.

## Distribution

- [ ] **Icon**: replace the template's placeholder logos with a calendar icon.
- [ ] **Publish** to WinGet or the Command Palette Extension Gallery. Graph and Google need users to bring their own client IDs, or publisher and Google verification first.

## Done

- [x] Today's calendar from ICS feeds: one or more https or webcal URLs, or local `.ics` files, in the extension settings. Recurring events (including moved occurrences) and Windows time zone names are handled by Ical.Net; cancelled events are hidden.
- [x] Today page grouped into "Happening now", "Up next", "All day" and "Earlier today", with time range, relative time and location in the subtitle.
- [x] Details pane with the time, location, organizer, attendees and the first lines of the description.
- [x] Enter opens the meeting link (a known meeting service first, otherwise the first link); Ctrl+K has Copy link and Refresh (Ctrl+R). Data refreshes on open when it's more than 5 minutes old, or when the settings change.
- [x] A feed that fails to load shows under "Problems" without hiding the others.
- [x] Builds with just the .NET SDK (Windows SDK files come from NuGet); `deploy.ps1` builds and registers the package and keeps settings when it has to reinstall.
- [x] Hidden from the Start menu and app list.
- [x] Sample feed in `samples/sample.ics` with daily events, for trying it without a real calendar.

# Backlog

Ordered roughly by priority. Move items to **Done** when they ship. The original research is in git history (`13723c6`, PLAN.md).

## Next up

- [ ] **Caching**: calendar entries are available immediately, without waiting for the feeds to download.
  - Save each feed's downloaded ICS to disk (LocalState) after a successful load; load from disk at startup, then refresh in the background.
  - Keep the last good copy when a download fails, and show how old it is under "Problems".
  - Encrypt the cache with DPAPI: it holds meeting titles, attendees and links.
- [ ] **Times that tick**: relative times and sections update while the page is open.

## Later

- [ ] **Open in calendar**: open the event itself in its provider (Outlook on the web, Google Calendar), not just join it.
  - "Open in calendar" in Ctrl+K on every event that has a link to it; Enter does this for events with nothing to join.
  - Graph gives `webLink` and Google `htmlLink`. ICS feeds usually have no link back to the event (only a `URL` property, sometimes), so this mostly arrives with the Graph and Google providers.
- [ ] **Copy dial-in**: the one-tap phone number (`tel:+1...,,123#`) from the invite, in Ctrl+K.
- [ ] **Dock band** "Standup · 7m"; clicking it joins. Copy the built-in `NowDockBand` timer pattern. Watch PowerToys #50367: bands can die after an RDP or session switch.
- [ ] **Named feeds** with a colour tag per feed.
- [ ] **Encrypt feed URLs** (DPAPI): published-calendar links work like passwords.
- [ ] **Hide declined, tag tentative**: needs your email address(es) to find yourself among the attendees.
- [ ] **Microsoft 365 (Graph)**:
  - The default tenant policy needs admin consent for `Calendars.Read`; test that first.
  - WAM needs a parent window we create ourselves.
  - `iCalUId` is different for each occurrence of a series.
- [ ] **Google Calendar**:
  - Testing mode expires tokens after 7 days, so publish the app "In production" (unverified).
  - Workspace admins can block the app.
- [ ] **Merge calendars**: show the same meeting once. Match on start and end plus the join link, UID or title.

## Ideas

- [ ] Windows reminder with a Join button. Needs the Windows App SDK; possibly redundant with Outlook and Teams reminders.

## Engineering

- [ ] Tests for link detection and the layout (now/next split, empty states).
- [ ] Check the trimmed Release build for Ical.Net warnings.

## Done

- [x] Events from ICS feeds (URLs or local files), with recurrences and time zones handled by Ical.Net.
- [x] Today page with Happening now / Up next / All day / Earlier today sections and a details pane.
- [x] Open link, copy link and refresh actions. A broken feed shows under "Problems".
- [x] Builds with just the .NET SDK. `deploy.ps1` registers the package and keeps settings.
- [x] Hidden from the Start menu.
- [x] Calendar icon and logos instead of the template placeholders.
- [x] Fixed command ID (`CmdPalCalendar.Calendar`) so pins, aliases and hotkeys survive updates.
- [x] One "Calendar" command with day navigation (Ctrl+←/→, Ctrl+T), Todoist-style typed dates, and Day / Schedule views.
- [x] Empty days and finished days point to the next event.
- [x] Tests for the ICS source and date parsing.
- [x] Join in the Teams or Zoom app (Safe Links, Google and Proofpoint redirects unwrapped; falls back to the browser), with Join in browser, Copy link and Copy meeting ID and passcode in Ctrl+K, and the service shown on each row.
- [x] Current or next meeting in the command subtitle ("Next: Standup in 7 min", "Now: Review · until 10:45"), updated every 30 seconds, plus a "Join next meeting" command (`CmdPalCalendar.JoinNext`) for a hotkey.
- [x] Smarter Join next meeting: only meetings with a Teams, Zoom, Meet or Webex link count; joins straight away within 15 minutes of the start, otherwise asks first; opens the calendar when there is nothing to join; its own subtitle ("Standup in 4 min · Teams", "Next to join: Review at 14:00", "Nothing to join today").

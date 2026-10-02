# Backlog

Ordered roughly by priority. Move items to **Done** when they ship. The original research is in git history (`13723c6`, PLAN.md).

## To test

Built but not yet checked by hand in Command Palette. Tick or remove each one after trying it.

- [ ] **Formatted notes**:
  - An Outlook invite with a long agenda shows in full, formatted, without the Teams footer.
  - A Google invite with bold text and links shows them formatted and clickable.
  - A Teams invite in another language: is the footer still cut? Only the underscore line is language-independent.
- [ ] **Smarter Join next meeting**:
  - A meeting more than 15 minutes away asks for confirmation before joining.
  - A day with only in-person meetings says "Nothing to join today", and Enter opens the calendar.
  - A hotkey or alias on Join next meeting still works after its subtitle has changed a few times.
- [ ] **Next meeting subtitle**: the Calendar subtitle switches to the next meeting once it starts in under 5 minutes, and the rule feels right with a real calendar.
- [ ] **Times that tick**: leave the page open on today; "in N min" counts down without the selection jumping, and a meeting moves from Up next to Happening now (and to Earlier today) at its start and end time.
- [ ] **Encrypted addresses**: after Reload, `calendars.json` shows `protectedLocation` instead of the calendar URLs, and all calendars still load and can be edited.
- [ ] **Caching**:
  - After restarting Command Palette (or Reload), events and the Calendar subtitle appear right away instead of after the download.
  - With the network off (or a calendar's address broken), its events stay, and "Problems" shows "Couldn't refresh … · showing data from … ago".
  - `cache/` in the extension's LocalState holds one `.ics.bin` per calendar, unreadable as text.
- [ ] **Manage calendars**:
  - After Reload, the three feeds from the old settings text box show up as calendars (named after their host, coloured blue, purple and green), and the Settings page is gone.
  - Adding a calendar: a bad address shows an error in the form; a blank name uses the calendar's own name.
  - Editing a name or colour updates the event tags straight away; turning a calendar off hides its events.
  - Removing a calendar asks first.
  - With several calendars on, each row shows a coloured calendar tag; with one, only the details pane does.
  - The form's colour dropdown and on/off toggle show the saved values when editing.

## Next up


## Later

- [ ] **Open in calendar**: open the event itself in its provider (Outlook on the web, Google Calendar), not just join it.
  - "Open in calendar" in Ctrl+K on every event that has a link to it; Enter does this for events with nothing to join.
  - Graph gives `webLink` and Google `htmlLink`. ICS feeds usually have no link back to the event (only a `URL` property, sometimes), so this mostly arrives with the Graph and Google providers.
- [ ] **Copy dial-in**: the one-tap phone number (`tel:+1...,,123#`) from the invite, in Ctrl+K.
- [ ] **Dock band** "Standup · 7m"; clicking it joins. Copy the built-in `NowDockBand` timer pattern. Watch PowerToys #50367: bands can die after an RDP or session switch.
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
- [ ] Coloured service tags: brand-coloured backgrounds with white text for Teams (~`#5B5FC7`), Zoom (~`#0B5CFF`), Meet (~`#00897B`) and Webex, via `Tag.Background`/`Foreground` and `ColorHelpers.FromRgb`. Colours are fixed (not theme-aware), so stick to coloured backgrounds with white text. Check that colours survive row reuse (bookmarx lost tag icons that way), and keep other tags (feed, Tentative) neutral so the service stands out.
- [ ] Images in event notes: dropped for now, because remote images let the sender track when the event was opened. Revisit if embedded (`data:`) images turn out to matter. See [docs/description-formatting.md](docs/description-formatting.md).

## Engineering

- [ ] Tests for link detection and the layout (now/next split, empty states).
- [ ] Check the trimmed Release build for Ical.Net warnings.

## Done

- [x] Events from ICS feeds (URLs or local files), with recurrences and time zones handled by Ical.Net.
- [x] Today page with Happening now / Up next / All day / Earlier today sections and a details pane.
- [x] Open link, copy link and refresh actions. A broken feed shows under "Problems".
- [x] Builds with just the .NET SDK. `deploy.ps1` registers the package and keeps settings.
- [x] Hidden from the Start menu.
- [x] Manage calendars page (Ctrl+K on Calendar or any row): add, edit, turn off and remove ICS calendars, each with a name and colour, checked before saving and stored in `calendars.json`. The old settings text box is imported once and the Settings page removed. Events show their calendar as a coloured tag.
- [x] Caching: each calendar's last download is kept in `cache/`, encrypted with DPAPI. Events show at startup before the download finishes, and the last good copy stays visible when a download fails, with its age under "Problems". Copies of removed or turned-off calendars are deleted.
- [x] Calendar addresses encrypted in `calendars.json` with DPAPI (names, colours and on/off stay readable); plain addresses from older files are encrypted on first load.
- [x] Times that tick: with the page open, "in 7 min" / "started 3 min ago" update every 30 seconds in place (selection and scroll stay put), and the sections are rebuilt when a meeting starts or ends, or at midnight.
- [x] Calendar icon and logos instead of the template placeholders.
- [x] Fixed command ID (`CmdPalCalendar.Calendar`) so pins, aliases and hotkeys survive updates.
- [x] One "Calendar" command with day navigation (Ctrl+←/→, Ctrl+T), Todoist-style typed dates, and Day / Schedule views.
- [x] Empty days and finished days point to the next event.
- [x] Tests for the ICS source and date parsing.
- [x] Join in the Teams or Zoom app (Safe Links, Google and Proofpoint redirects unwrapped; falls back to the browser), with Join in browser, Copy link and Copy meeting ID and passcode in Ctrl+K, and the service shown on each row.
- [x] Current or next meeting in the command subtitle ("Next: Standup in 7 min", "Now: Review · until 10:45"), updated every 30 seconds, plus a "Join next meeting" command (`CmdPalCalendar.JoinNext`) for a hotkey.
- [x] Smarter Join next meeting: only meetings with a Teams, Zoom, Meet or Webex link count; joins straight away within 15 minutes of the start, otherwise asks first; opens the calendar when there is nothing to join; its own subtitle ("Standup in 4 min · Teams", "Next to join: Review at 14:00", "Nothing to join today").
- [x] Formatted event notes: HTML descriptions (Outlook's `X-ALT-DESC` first) shown as Markdown with bold, italic, links, lists and flattened tables, and the Teams/Zoom invite block cut. Rules and decisions in [docs/description-formatting.md](docs/description-formatting.md).

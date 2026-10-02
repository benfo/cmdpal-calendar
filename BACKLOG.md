# Backlog

Ordered roughly by priority. Move items to **Done** when they ship. The original research is in git history (`13723c6`, PLAN.md).

## Next up

- [ ] **Join in the app**: Enter opens Teams or Zoom directly.
  - Unwrap Safe Links, `google.com/url?q=` and Proofpoint links before matching.
  - Teams: swap `https` for `msteams`, keeping the host and path. This covers `/l/meetup-join/`, `/meet/`, `teams.live.com` and gov clouds.
  - Zoom: rewrite only `/j/<id>`, to `zoommtg://<host>/join?action=join&confno=<id>&pwd=<pwd>`. `/my/` and `/w/` links open in the browser.
  - Check with `Launcher.QueryUriSupportAsync` (registry checks miss MSIX apps), then launch with `LaunchUriAsync` and an https `FallbackUri`.
  - Ctrl+K: join in browser, copy meeting ID and passcode, copy dial-in.
- [ ] **Times that tick**: relative times and sections update while the page is open.

## Later

- [ ] **Dock band** "Standup · 7m"; clicking it joins. Copy the built-in `NowDockBand` timer pattern. Watch PowerToys #50367: bands can die after an RDP or session switch.
- [ ] **Next meeting** in the command subtitle, plus a "Join next meeting" command.
- [ ] **Named feeds** with a colour tag per feed.
- [ ] **Encrypt feed URLs** (DPAPI): published-calendar links work like passwords.
- [ ] **Hide declined, tag tentative**: needs your email address(es) to find yourself among the attendees.
- [ ] **Background refresh** with the last result kept on disk, so the page opens instantly.
- [ ] **Microsoft 365 (Graph)**:
  - The default tenant policy needs admin consent for `Calendars.Read`; test that first.
  - WAM needs a parent window we create ourselves.
  - `iCalUId` is different for each occurrence of a series.
- [ ] **Google Calendar**:
  - Testing mode expires tokens after 7 days, so publish the app "In production" (unverified).
  - Workspace admins can block the app.
- [ ] **Merge calendars**: show the same meeting once. Match on start and end plus the join link, UID or title.

## Ideas

- [ ] Show tomorrow's first meeting when today is done.
- [ ] Windows reminder with a Join button. Needs the Windows App SDK; possibly redundant with Outlook and Teams reminders.

## Engineering

- [ ] Tests for link detection and the now/next split.
- [ ] Check the trimmed Release build for Ical.Net warnings.
- [ ] Replace the template's placeholder icons.

## Done

- [x] Today's events from ICS feeds (URLs or local files), with recurrences and time zones handled by Ical.Net.
- [x] Today page with Happening now / Up next / All day / Earlier today sections and a details pane.
- [x] Open link, copy link and refresh actions. A broken feed shows under "Problems".
- [x] Builds with just the .NET SDK. `deploy.ps1` registers the package and keeps settings.
- [x] Hidden from the Start menu.
- [x] Fixed command ID (`CmdPalCalendar.Today`) so pins, aliases and hotkeys survive updates.

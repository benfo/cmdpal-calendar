# Backlog

Ordered roughly by priority. Move items to **Done** when they ship. The original research is in git history (`13723c6`, PLAN.md).

## To test

Built but not yet checked by hand in Command Palette. Tick or remove each one after trying it.

- [ ] **Add a calendar chooser** (Graph step 1):
  - Manage calendars → Add a calendar opens a chooser with "Calendar address or file" and "Microsoft 365 or Outlook.com".
  - After adding, you land back on the chooser (one step back; the host can't go back two); Esc returns to Manage calendars.
  - Existing calendars still load, edit, turn off and remove as before.
- [ ] **Microsoft sign-in and choosing calendars** (signing in, listing calendars and adding a second account are confirmed working):
  - Add a calendar → Microsoft 365 or Outlook.com: with no Microsoft account yet, the Windows account picker offers your work account; with one already added, it asks for a fresh sign-in so you can enter another account.
  - While signing in or loading, the form says "Signing in…" or "Loading your calendars…" (no "Couldn't show your calendars" flash).
  - The form shows only the signed-in account's calendars as toggles, with already added ones on (default first, "· your default calendar", "· shared by …"); Save adds and removes to match, says "Added 1 calendar, removed 1 calendar" and goes back. Adding and Ctrl+K → Choose calendars show the same form.
  - Switching from one calendar to another (toggle one off, the other on) works in any order.
  - Save with everything off asks "Sign out of …?"; Sign out removes the account's calendars and goes back; Cancel keeps the form.
  - Cancelling the Windows sign-in shows "Sign-in was cancelled" with a Try again button; going back and opening Microsoft 365 again starts a new sign-in (also for Ctrl+K → Choose calendars opened a second time). After the Windows sign-in closes (cancelled or signed in), Command Palette has the keyboard again: Esc goes back without clicking first.
  - Manage calendars: Ctrl+K on a Microsoft calendar starts with "Choose calendars for <account>", which opens the same form for that account without signing in.
  - Manage calendars shows Microsoft calendars with the account as subtitle.
  - Signing in to a new account and pressing Back without saving: within about 20 minutes (15 minutes' grace, then the next refresh) the leftover sign-in is removed, so adding that account again asks for a fresh sign-in.
- [ ] **Sign in again** (Graph step 6, first part; signing in again and keeping the sign-in across Reload are confirmed working):
  - The row reads "Sign in again to <account>" with "Your calendars can't update until you sign in" underneath.
  - Cancelling the prompt leaves the row in place.
- [ ] **Sign out** (Graph step 6):
  - In Manage calendars, Ctrl+K on a Microsoft calendar shows "Sign out of <account>"; it asks first ("Its 2 calendars will be removed…"), then removes that account's calendars only.
  - Removing a Microsoft calendar that is its account's last says "…and you'll be signed out of <account>"; afterwards, Add a calendar → Microsoft asks you to sign in again.
  - Removing one of several calendars of an account keeps you signed in.
  - The removed calendars' `.graph.bin` files disappear from `cache/` after the next refresh.
- [ ] **Join guard on calendar rows**:
  - Enter on a meeting more than 15 minutes away asks "Join …? … starts tomorrow at 09:00 (in 22 h). Join now?"; Join joins, Esc cancels.
  - Enter on a meeting that has ended asks "… ended at 10:30. Join anyway?".
  - Within 15 minutes of the start, or during the meeting, Enter joins straight away. Join next meeting behaves as before.
- [ ] **Microsoft events** (Graph step 3):
  - Events from added Microsoft calendars show on the calendar page, at the right times, all-day events on the right day.
  - Teams meetings join with Enter; notes show the agenda formatted without the Teams footer; Copy meeting ID and passcode works.
  - Ctrl+Enter (or Enter on events with nothing to join) is "Open in Outlook" and opens that event itself in the browser.
  - Renaming or recolouring a Microsoft calendar updates its event tags straight away.
  - With the network off, events stay and "Couldn't refresh …" shows.
- [ ] **Microsoft cache and delta** (Graph step 4):
  - After Reload, Microsoft events show straight away, before the network catches up.
  - With the network off and a Reload, events still show, with "Couldn't refresh … · showing data from … ago".
  - A meeting added, moved or deleted in Outlook shows up within about 5 minutes (Refresh to force).
  - Shared or secondary calendars (not your default) also update; if delta isn't supported for them, they still load.
  - `cache/` holds one `.graph.bin` per Microsoft calendar, unreadable as text; removing a calendar deletes its file.

- [ ] **Open in Outlook or Google Calendar** (opening Outlook and Google Calendar is confirmed working):
  - Ctrl+K lists the open command once, and **Copy event** copies "Sat 3 Oct, 09:30–10:00 · Standup · Room 4".
  - Other calendars: Enter still does nothing on events with nothing to join; Ctrl+Enter copies the event.
  - Ctrl+B joins in the browser on a Teams or Zoom event.
  - With the new Teams app installed, Join in browser opens straight in the browser without Windows asking which app to use.
- [ ] **Updating a release** (once v0.1.2 is out; a fresh install of v0.1.1 is confirmed working):
  - Running the install command in a normal (non-admin) PowerShell asks for admin rights and continues in an admin window.
  - Running it with an older version installed stops the running extension, says "Updated … from X to Y", and keeps the calendars.
  - Running it again says "already installed and up to date".
- [ ] **Formatted notes**:
  - An Outlook invite with a long agenda shows in full, formatted, without the Teams footer.
  - A Google invite with bold text and links shows them formatted and clickable.
  - A Teams invite in another language: is the footer still cut? Only the underscore line is language-independent.
- [ ] **Smarter Join next meeting**:
  - A meeting more than 15 minutes away asks for confirmation before joining.
  - A day with only in-person meetings says "Nothing to join today", and Enter opens the calendar.
  - A hotkey or alias on Join next meeting still works after its subtitle has changed a few times.
- [ ] **Next meeting subtitle**: the Calendar subtitle switches to the next meeting once it starts in under 5 minutes, and the rule feels right with a real calendar.
- [ ] **Encrypted addresses**: after Reload, `calendars.json` shows `protectedLocation` instead of the calendar URLs, and all calendars still load and can be edited.
- [ ] **Caching**:
  - After restarting Command Palette (or Reload), events and the Calendar subtitle appear right away instead of after the download.
  - With the network off (or a calendar's address broken), its events stay, and "Problems" shows "Couldn't refresh … · showing data from … ago".
  - `cache/` in the extension's LocalState holds one `.ics.bin` per calendar, unreadable as text.
- [ ] **Manage calendars**:
  - After Reload, the three feeds from the old settings text box show up as calendars (named after their host, coloured blue, purple and green), and the Settings page is gone.
  - Editing a name or colour updates the event tags straight away; turning a calendar off hides its events.
  - Removing a calendar asks first.
  - With several calendars on, each row shows a coloured calendar tag; with one, only the details pane does.
  - The edit form's colour dropdown and on/off toggle show the saved values.

## Next up

- [ ] **Microsoft 365 and Outlook.com (Graph)** (in progress): needed where the org blocks publishing calendars with details (StructureIt does).
  - Tested 2026-10-05 with the Graph PowerShell client ID (`14d82eec-…`): you could consent to `Calendars.Read` yourself, and `calendarView` returned bodies and `onlineMeeting.joinUrl`. Develop with that ID; register our own multi-tenant app before release and test consent for it (unverified publisher).
  - UX: "Add a calendar" opens a chooser of working sources (Microsoft 365 or Outlook.com; Calendar address or file). Microsoft signs in with the Windows account picker (WAM; a fresh sign-in once an account exists), then a form shows that account's calendars as toggles with Save (Save with none asks to sign out). Ctrl+K "Choose calendars for …" in Manage calendars opens the same form. Each calendar is its own row (name and colour from Outlook, editable), subtitled with its account.
  - Accounts are implicit: Ctrl+K "Sign out of …" removes the account's calendars; removing its last calendar signs it out.
  - Background refreshes never open dialogs. A broken sign-in shows one "Sign in again to …" row per account on the calendar page; Enter opens the picker.
  - Events: ICS parity (join link from `onlineMeeting.joinUrl`), and opening the event itself via `webLink`.
  - Data: rolling window of 2 weeks back to 3 months ahead, cached encrypted, refreshed every 5 minutes with delta; dates outside it fetched on demand.
  - Code: `Microsoft/` (`IMicrosoftSignIn`, `GraphClient` over the Graph SDK, `GraphEntryMapper`, `GraphCalendarSource`, `GraphEventCache`), `Platform/WamSignIn` (MSAL broker, own parent window), `CompositeCalendarSource`, `CalendarFeed` gains a source. Trimmed, the Graph SDK adds ~0.3 MB and the broker's `msalruntime.dll` ~3 MB; add a Graph case to the trim check.
  - Steps: 1. feed source + composite source + chooser; 2. sign-in + calendar picker; 3. events; 4. cache + delta; 5. on-demand ranges; 6. sign in again, sign out.
  - `iCalUId` is different for each occurrence of a series.

- [ ] **Back up the signing key** (user): move `Documents\CmdPalCalendar-signing\` (the `.pfx` and its password) into the password manager, then delete that folder. Until then it is the only copy outside the GitHub secrets.
- [ ] **Release v0.1.2**: `main` has installer fixes not yet released (ask for admin rights instead of failing; stop the running extension before updating; "already up to date" and "Updated from X to Y" messages). Waiting for the user's go-ahead to tag.
- [ ] **Shortcut tip** (waiting for the user's decision): there's no SDK way to give extension commands a default alias or hotkey. Suggested: the installer's closing message and the README suggest one (Calendar alias `cal`, a global hotkey for Join next meeting). Writing into Command Palette's own `settings.json` is possible but fragile, so not recommended.
- [ ] **Dock band** (recommended next feature): "Standup · 7m" in the Command Palette Dock; clicking it joins. Reuse `UpcomingMeeting`/`NextMeetingTicker` logic; copy the built-in `NowDockBand` timer pattern (`GetDockBands()`, band command with a non-empty `Id`). Watch PowerToys #50367: bands can stop updating after an RDP or session switch.
- [ ] **Microsoft Store** (blocked on the user creating the developer account) (unlisted at first, public later; replaces the self-signed sideload):
  - Register a free individual developer account at storedeveloper.microsoft.com (ID and selfie check).
  - Reserve the app name in Partner Center; "Calendar" is likely taken, so something like "Calendar for Command Palette".
  - Put the identity Partner Center assigns (package name and `CN=…` publisher) into a Store build: an unsigned `.msixupload` for x64 and ARM64, made alongside the sideload packages.
  - First submission by hand, unlisted (available by direct link only); a short privacy statement (calendar data stays on the PC).
  - Then automate submissions on each tag with the `msstore` CLI (needs an Entra app linked to Partner Center).
  - Friends move from the sideload to the Store version: uninstall, install from the Store link, re-add calendars (different package identity).
- [ ] **Extension Gallery**: once it's on the Store, add `extensions/benfo/cmdpal-calendar/extension.json` via a PR to `microsoft/CmdPal-Extensions`.

## Later

- [ ] **Show the year outside this year**: on a date in another year, titles like "Monday 5 Oct" don't say which year. Suggested: `DateText` adds the year ("Monday 5 Oct 2027", "Mon 5 Oct 2027 – Fri 9 Oct 2027") only when the date isn't in the current year, so everyday titles stay short.

- [ ] **Open the event itself**: Google calendars open the event's day on the web, and Outlook only its day view (Outlook on the web has no link to a date). Opening the event needs a link to it.
  - Graph gives `webLink` and Google `htmlLink`. ICS feeds usually have no link back to the event (only a `URL` property, sometimes), so this mostly arrives with the Graph and Google providers.
- [ ] **Copy dial-in**: the one-tap phone number (`tel:+1...,,123#`) from the invite, in Ctrl+K.
- [ ] **Hide declined, tag tentative**: needs your email address(es) to find yourself among the attendees.
- [ ] **Google Calendar**:
  - Testing mode expires tokens after 7 days, so publish the app "In production" (unverified).
  - Workspace admins can block the app.
- [ ] **Merge calendars**: show the same meeting once. Match on start and end plus the join link, UID or title.

## Ideas

- [ ] Windows reminder with a Join button. Needs the Windows App SDK; possibly redundant with Outlook and Teams reminders.
- [ ] Coloured service tags: brand-coloured backgrounds with white text for Teams (~`#5B5FC7`), Zoom (~`#0B5CFF`), Meet (~`#00897B`) and Webex, via `Tag.Background`/`Foreground` and `ColorHelpers.FromRgb`. Colours are fixed (not theme-aware), so stick to coloured backgrounds with white text. Check that colours survive row reuse (bookmarx lost tag icons that way), and keep other tags (feed, Tentative) neutral so the service stands out.
- [ ] Ask PowerToys (GitHub issue) for an SDK way to suggest a default alias or hotkey for an extension's top-level commands.
- [ ] Images in event notes: dropped for now, because remote images let the sender track when the event was opened. Revisit if embedded (`data:`) images turn out to matter. See [docs/description-formatting.md](docs/description-formatting.md).

## Engineering

- [ ] **Shared base for calendar sources**: `IcsCalendarSource` and `GraphCalendarSource` repeat the same bookkeeping (loaded calendars, 5-minute staleness, last good copy, problems). Move it into one tested class.
- [ ] **Testable page state**: move `CalendarPage`'s selected date, view and typed-date handling into a plain `CalendarPageState`, like `MicrosoftCalendarPicker`, so it can be unit-tested.
- [ ] **Safe background work from commands**: replace `_ = Task.Run(...)` with a helper that runs the work and shows failures as a toast, so exceptions aren't lost.
- [ ] **Logging to a file** in LocalState: sign-in failures, Graph errors, refresh timings; no personal data. `Microsoft.Extensions.Logging` abstractions with a small file writer of our own (trim-safe; Serilog's configuration uses reflection).
- [ ] **Calendar provider abstraction** before adding Google: one `ICalendarProvider` per kind (ICS, Microsoft, Google) bundling its source, chooser entry and editing, so a new provider doesn't touch the store, chooser, form, Manage page and wiring.
- [ ] **Store calendar sources with System.Text.Json polymorphism** (`[JsonDerivedType]`, source-generated) instead of mapping each kind by hand (`StoredMicrosoft`). Pairs with the provider abstraction.
- [ ] **Test helpers**: `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) instead of `ManualTimeProvider`, which also fires timers so the ticker and `NextMeetingTicker` become testable; Verify snapshot tests for `HtmlToMarkdown` and `EventNotes`. Test-only dependencies.
- [ ] **`Microsoft.VisualStudio.Threading.Analyzers`**: catches fire-and-forget tasks, `async void` and similar at build time.
- [ ] Tests for link detection and the layout (now/next split, empty states).

## Done

- [x] Same editing for every calendar: Enter edits name, colour and visibility (with a read-only "From …" line); Ctrl+K starts with the provider's source command ("Change address…" or "Choose calendars for …"); adding only asks where a calendar comes from.

- [x] Events from ICS feeds (URLs or local files), with recurrences and time zones handled by Ical.Net.
- [x] Today page with Happening now / Up next / All day / Earlier today sections and a details pane.
- [x] Open link, copy link and refresh actions. A broken feed shows under "Problems".
- [x] Builds with just the .NET SDK. `deploy.ps1` registers the package and keeps settings.
- [x] Hidden from the Start menu.
- [x] Manage calendars page (Ctrl+K on Calendar or any row): add, edit, turn off and remove ICS calendars, each with a name and colour, checked before saving and stored in `calendars.json`. The old settings text box is imported once and the Settings page removed. Events show their calendar as a coloured tag.
- [x] Caching: each calendar's last download is kept in `cache/`, encrypted with DPAPI. Events show at startup before the download finishes, and the last good copy stays visible when a download fails, with its age under "Problems". Copies of removed or turned-off calendars are deleted.
- [x] Calendar addresses encrypted in `calendars.json` with DPAPI (names, colours and on/off stay readable); plain addresses from older files are encrypted on first load.
- [x] Times that tick: with the page open, "in 7 min" / "started 3 min ago" update every 30 seconds in place (selection and scroll stay put), and the sections are rebuilt when a meeting starts or ends, or at midnight.
- [x] Releases: pushing a `v*` tag builds trimmed, self-contained x64 and ARM64 packages, signs them with a self-signed `CN=ben.fourie` certificate, and publishes a GitHub release that installs with one command (`install.ps1`). The trimmed build was checked: Ical.Net works, time zones included. CI builds and tests every push. MIT licence.
- [x] Calendar icon and logos instead of the template placeholders.
- [x] Fixed command ID (`CmdPalCalendar.Calendar`) so pins, aliases and hotkeys survive updates.
- [x] One "Calendar" command with day navigation (Ctrl+←/→, Ctrl+T), Todoist-style typed dates, and Day / Schedule views.
- [x] Empty days and finished days point to the next event.
- [x] Tests for the ICS source and date parsing.
- [x] Join in the Teams or Zoom app (Safe Links, Google and Proofpoint redirects unwrapped; falls back to the browser), with Join in browser, Copy link and Copy meeting ID and passcode in Ctrl+K, and the service shown on each row.
- [x] Current or next meeting in the command subtitle ("Next: Standup in 7 min", "Now: Review · until 10:45"), updated every 30 seconds, plus a "Join next meeting" command (`CmdPalCalendar.JoinNext`) for a hotkey.
- [x] Smarter Join next meeting: only meetings with a Teams, Zoom, Meet or Webex link count; joins straight away within 15 minutes of the start, otherwise asks first; opens the calendar when there is nothing to join; its own subtitle ("Standup in 4 min · Teams", "Next to join: Review at 14:00", "Nothing to join today").
- [x] Formatted event notes: HTML descriptions (Outlook's `X-ALT-DESC` first) shown as Markdown with bold, italic, links, lists and flattened tables, and the Teams/Zoom invite block cut. Rules and decisions in [docs/description-formatting.md](docs/description-formatting.md).

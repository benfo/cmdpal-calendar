# Calendar for Command Palette

A PowerToys Command Palette extension (C#, .NET 10, MSIX) that shows meetings day by day and joins them in one action. Public repo: `benfo/cmdpal-calendar` (MIT).

- `README.md`: what it does, install, development, releasing.
- `BACKLOG.md`: **To test** (built but not checked by hand), Next up, Later, Ideas, Done. Keep it current with every change.
- `docs/description-formatting.md`: rules and decisions for turning event descriptions into Markdown.

## Commands

- Deploy a Debug build for manual testing: `./deploy.ps1`, then **Reload** in Command Palette. Needs Developer Mode; no Visual Studio or Windows SDK (it comes from the `Microsoft.Windows.SDK.CPP` NuGet package). Deploy stops the running extension; if a release build is installed, deploy removes it (0x80073CFB) and keeps LocalState.
- Unit tests: `dotnet test --project tests/CmdPalCalendar.Tests` (xUnit v3 on Microsoft.Testing.Platform, see `global.json`).
- Trimmed-build check: `dotnet publish tests/CmdPalCalendar.TrimCheck -c Release -o <dir>` and run the exe (it parses `tests/fixtures/rich.ics` and checks the ICS cache). Release builds are trimmed; this catches reflection breakage.
- Release-config build (the Debug output is locked while the extension runs): `dotnet build CmdPalCalendar.sln -c Release -p:Platform=x64`. Its 3 IL2104 warnings (Ical.Net, Windows SDK, WinRT.Runtime) are expected; anything else is new.
- Signed packages locally: `./package.ps1 -Version 0.1.2 -CertificatePath <pfx> -CertificatePassword <pw>` → `artifacts/`.
- Release: push a tag `vX.Y.Z`. `.github/workflows/release.yml` tests, runs the trim check, builds x64 + ARM64, signs with the `SIGNING_CERTIFICATE`/`SIGNING_PASSWORD` secrets and publishes a GitHub release (packages, `CmdPalCalendar.cer`, `install.ps1`). CI (`ci.yml`) tests and builds every push to `main`.
- Install a release: `irm https://github.com/benfo/cmdpal-calendar/releases/latest/download/install.ps1 | iex`.

Always check exit status before committing: piping build/test output through `grep` hides failures. Run tests into a log file and test `$?`.

## Architecture

- `CmdPalCalendar/` (extension)
  - `CalendarCommandProvider.cs`: composition root. Wires the store, sources, caches, sign-in, refresher, pages and ticker; runs the unused-sign-in cleanup after each refresh. Top-level commands: **Calendar** (`CmdPalCalendar.Calendar`) and **Join next meeting** (`CmdPalCalendar.JoinNext`).
  - `Events/`: domain, no Command Palette dependencies, unit-tested. `CalendarEntry` (incl. `WebLink`), `ICalendarSource`, `CompositeCalendarSource` (merges providers), `CalendarProblem`, `CalendarRefresher`, `LocalTime`, `DateQuery` (Todoist-style dates), `DateText`/`TimeText` (shapes from the culture's long date and short time patterns, year only outside this year; words stay English), `MeetingLink` (Teams/Zoom/Meet/Webex, native `msteams://`/`zoommtg://` rewrites), `LinkUnwrapper`, `MeetingCredentials`, `UpcomingMeeting` (15-minute join window), `MeetingPhases`, `HtmlToMarkdown`, `EventNotes`, `MarkdownText`, `CalendarSite` (Outlook/Outlook.com/Google, and their web addresses).
  - `Feeds/`: `CalendarFeed` (name, colour, on/off, `FeedSource`), `FeedSource` (`IcsSource(Location)`; `Label`/`Provider` for the UI), `CalendarFeedStore` (`calendars.json`, ICS addresses DPAPI-encrypted, `Current(feed)` for saving on top of the latest state), `EncryptedFiles`, `LegacyFeedImport`, `ISecretProtector`.
  - `Ics/`: ICS provider. `IcsCalendarSource` (cache-first load, keeps last good copy, `Updated` event), `IcsFeedReader`, `IcsEntryMapper`, `IcsFeedCheck` (form validation), `IcsFeedCache` (`cache/*.ics.bin`).
  - `Platform/`: `DpapiSecretProtector`.
  - `Pages/`: Command Palette UI. `CalendarPage` (state, Day/Schedule views, typed dates, 30 s ticking), `CalendarLayout`, `CalendarRows` (Enter joins via `JoinEventCommand`, which asks outside the join window; else opens the event or calendar site; Ctrl+K order sets Ctrl+Enter), `NavigationCommands`, `OpenInBrowserCommand`, `CalendarSiteText`, `EventText`, `ManageCalendarsPage` (Enter → `CalendarDetailsFormPage`; Ctrl+K: change source, Turn off, Remove), `CalendarDetailsFormPage` (name/colour/on, the same for every provider), `IcsAddressFormPage` (ICS add and Change address), `FormValues`, `JoinMeetingCommand`, `JoinNextMeetingItem`/`Command`, `NextMeetingTicker`, `LiveSubtitles`, `Glyphs`, `CalendarColors`.
- `tests/CmdPalCalendar.Tests` links `Events/`, `Ics/`, `Feeds/` as source (no WinRT). Keep logic out of `Pages/` so it can be tested.
- Runtime data (package LocalState, `%LOCALAPPDATA%\Packages\CmdPalCalendar_q85rqfpt8ekz6\LocalState` for the dev build): `calendars.json`, `cache/*.ics.bin`. All encrypted per Windows user.

## Code conventions

- SOLID and DRY: one responsibility per class; depend on abstractions (`ICalendarSource`, `ISecretProtector`, `TimeProvider`); share helpers (`TimeText`, `DateText`, `MarkdownText`, `MeetingServiceText`) instead of repeating logic.
- No explanatory comments; names carry the meaning. Only a short "why" for something non-obvious (e.g. `Trimming.props`).
- 0 build warnings (analyzers on). Classes passed to WinRT must be `partial` (CsWinRT1028).
- Trim-safe code: System.Text.Json source generation, `GeneratedRegex`, no reflection. Ical.Net is rooted in `Trimming.props` because it uses reflection; any new reflection-heavy library needs the same plus a case in `tests/fixtures/rich.ics`. The Graph SDK and MSAL (on the Microsoft 365 branch) trim cleanly.
- Every provider behaves the same in the UI (the user's rule: consistency is king): Add asks only where a calendar comes from; Enter edits how it looks; Ctrl+K starts with the provider's "change source" command.
- Icon glyphs live in `Pages/Glyphs.cs` as `\uXXXX` escapes. Tool input decodes `\u` sequences, so write them via a script using char 92, and check for raw private-use characters.
- Non-ASCII text (·, –, …) in docs: prefer the Edit tool. `perl -CSD` and `sed` have produced mojibake (Â·, �); grep for it after edits.
- `.csproj` and `Package.appxmanifest` start with a BOM; anchor-based edits can miss, and rewrites must keep it.

## Command Palette host quirks (SDK 0.12, PowerToys 0.101)

- A command's `Id` becomes the top-level item's ID (pins, aliases, hotkeys). Swapping a command changes it unless the new command has the same `Id`.
- `CommandResult.GoToPage` is not handled by the host; swap the item's command instead (see `JoinNextMeetingItem`).
- Row shortcuts (`RequestedShortcut`) only work on the selected row, so every state renders at least one row carrying them. They are checked before the search box, so Ctrl+←/→ work while typing.
- `DynamicListPage` updates `SearchText` without notifying; clear it with `SetSearchNoUpdate("")` + `OnPropertyChanged(nameof(SearchText))`.
- Form `SubmitForm` runs on a background task per click: guard against repeat submits and show progress (`IsLoading` + status text).
- Rebuilding a list resets selection; update existing items in place for live values (`LiveSubtitles`).
- Ctrl+Enter runs the first visible command in `MoreCommands` (separators skipped), and the footer shows its name. The item's own command is also listed in Ctrl+K, so don't repeat it first or it shows twice.
- New Outlook and Teams claim `outlook.office.com`/`outlook.office365.com`/`teams.microsoft.com` as app URI handlers, so `OpenUrlCommand` shows a "select an app" picker. Use `OpenInBrowserCommand` (`IgnoreAppUriHandlers`) for links meant for the browser.
- Outlook on the web has no link to a given date (`/calendar/view/day/2026/10/3` opens today); Google Calendar's `/r/day/yyyy/M/d` works.
- No API for default aliases or hotkeys for extension commands.
- After deploying, Command Palette may keep the old extension until **Reload** or a restart.
- No page lifecycle hooks: an extension can't tell when a page is left (Back) and can't block Back. Command Palette also reuses a page object when you return to it, so reset per-visit state in `GetContent` (see `MicrosoftCalendarsPage`).
- Forms with several buttons: override `SubmitForm(string inputs, string data)`; `data` is the `Action.Submit` button's `data` object.
- WAM: MSAL forgets which accounts signed in across restarts unless its token cache is persisted (`ProtectedTokenCache`), even though Windows holds the sign-in. `Prompt.SelectAccount` can silently reuse the Windows account; use `Prompt.ForceLogin` to add a different one. WAM needs a parent window; we pass the foreground window (Command Palette).
- After the Windows sign-in (WAM) dialog closes, focus goes to the app that was active before Command Palette, not back to Command Palette; you click it once. The extension can't take it back: `SetForegroundWindow` (also with `AttachThreadInput`) and `SwitchToThisWindow` are refused (foreground lock). Untried: parenting the dialog on a hidden window of our own process.

## How we work

- Small, focused commits, one logical change each, single-line messages, no attribution (global rule). Commit after each step; update README/BACKLOG in their own commit.
- Code changes: one branch per backlog item (`feature/…`, `fix/…`), merged into `main` through a PR so CI runs first, without squashing. Docs-only changes (README, BACKLOG, CLAUDE.md, `docs/`) go straight to `main`. Tags only from `main`.
- The Microsoft 365 / Outlook.com provider (Graph, WAM sign-in, our app registration) lives on `feature/microsoft-365`: `main` plus a revert of its removal (`fba9d8f`). Merge `main` into it to keep it current; it comes back to `main` through a PR when it ships. Its own CLAUDE.md and BACKLOG describe it.
- Plan features before building: ask decisions one at a time, each with a recommendation first. Look up facts (source in the PowerToys repo, the SDK, this machine) rather than asking.
- Reuse what the sibling extension `../../bookmarx` already solved (build, deploy, patterns) before proposing new setup.
- Anything not tried by hand in Command Palette goes on BACKLOG **To test**; never claim it works until the user confirms. The user ticks items off.
- Ask before outward or irreversible actions: making things public, pushing tags/releases, deleting user data or packages.
- Check with the user before culling or renaming broadly; once agreed, be thorough.

## Release signing

Self-signed `CN=ben.fourie` code-signing certificate, thumbprint `982E6811C22A7186A5286A44EF6A3E1FB31F868F`, expires 2031-10-02. The private key lives in the GitHub secrets and the user's password manager only. The manifest `Publisher` must stay `CN=ben.fourie` for sideloaded releases. The Microsoft Store will assign its own identity (see BACKLOG).

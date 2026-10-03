# Calendar for Command Palette

A PowerToys Command Palette extension (C#, .NET 10, MSIX) that shows meetings day by day and joins them in one action. Public repo: `benfo/cmdpal-calendar` (MIT).

- `README.md`: what it does, install, development, releasing.
- `BACKLOG.md`: **To test** (built but not checked by hand), Next up, Later, Ideas, Done. Keep it current with every change.
- `docs/description-formatting.md`: rules and decisions for turning event descriptions into Markdown.

## Commands

- Deploy a Debug build for manual testing: `./deploy.ps1`, then **Reload** in Command Palette. Needs Developer Mode; no Visual Studio or Windows SDK (it comes from the `Microsoft.Windows.SDK.CPP` NuGet package). Deploy stops the running extension; if a release build is installed, deploy removes it (0x80073CFB) and keeps LocalState.
- Unit tests: `dotnet test --project tests/CmdPalCalendar.Tests` (xUnit v3 on Microsoft.Testing.Platform, see `global.json`).
- Trimmed-build check: `dotnet publish tests/CmdPalCalendar.TrimCheck -c Release -o <dir>` and run the exe against `tests/fixtures/rich.ics`. Release builds are trimmed; this catches reflection breakage.
- Signed packages locally: `./package.ps1 -Version 0.1.2 -CertificatePath <pfx> -CertificatePassword <pw>` → `artifacts/`.
- Release: push a tag `vX.Y.Z`. `.github/workflows/release.yml` tests, runs the trim check, builds x64 + ARM64, signs with the `SIGNING_CERTIFICATE`/`SIGNING_PASSWORD` secrets and publishes a GitHub release (packages, `CmdPalCalendar.cer`, `install.ps1`). CI (`ci.yml`) tests and builds every push to `main`.
- Install a release: `irm https://github.com/benfo/cmdpal-calendar/releases/latest/download/install.ps1 | iex`.

Always check exit status before committing: piping build/test output through `grep` hides failures. Run tests into a log file and test `$?`.

## Architecture

- `CmdPalCalendar/` (extension)
  - `CalendarCommandProvider.cs`: composition root. Wires the store, source, cache, refresher, pages and ticker. Top-level commands: **Calendar** (`CmdPalCalendar.Calendar`) and **Join next meeting** (`CmdPalCalendar.JoinNext`).
  - `Events/`: domain, no Command Palette dependencies, unit-tested. `CalendarEntry`, `ICalendarSource`, `CalendarRefresher`, `DateQuery` (Todoist-style dates), `MeetingLink` (Teams/Zoom/Meet/Webex, native `msteams://`/`zoommtg://` rewrites), `LinkUnwrapper`, `MeetingCredentials`, `UpcomingMeeting`, `MeetingPhases`, `HtmlToMarkdown`, `EventNotes`, `MarkdownText`.
  - `Ics/`: ICS provider. `IcsCalendarSource` (cache-first load, keeps last good copy, `Updated` event), `IcsFeedReader`, `IcsEntryMapper`, `IcsFeedCheck` (form validation), `IcsFeedCache` (encrypted copies in `cache/`).
  - `Feeds/`: `CalendarFeed`, `CalendarFeedStore` (`calendars.json`, addresses DPAPI-encrypted), `LegacyFeedImport`, `ISecretProtector`.
  - `Platform/`: `DpapiSecretProtector`.
  - `Pages/`: Command Palette UI. `CalendarPage` (state, Day/Schedule views, typed dates, 30 s ticking), `CalendarLayout`, `CalendarRows`, `NavigationCommands`, `ManageCalendarsPage`, `CalendarFeedFormPage`, `JoinMeetingCommand`, `JoinNextMeetingItem`/`Command`, `NextMeetingTicker`, `LiveSubtitles`, `Glyphs`, `CalendarColors`.
- `tests/CmdPalCalendar.Tests` links `Events/`, `Ics/`, `Feeds/` as source (no WinRT). Keep logic out of `Pages/` so it can be tested.
- Runtime data (package LocalState): `calendars.json`, `cache/*.ics.bin`. Both are encrypted per Windows user.

## Code conventions

- SOLID and DRY: one responsibility per class; depend on abstractions (`ICalendarSource`, `ISecretProtector`, `TimeProvider`); share helpers (`TimeText`, `DateText`, `MarkdownText`, `MeetingServiceText`) instead of repeating logic.
- No explanatory comments; names carry the meaning. Only a short "why" for something non-obvious (e.g. `Trimming.props`).
- 0 build warnings (analyzers on). Classes passed to WinRT must be `partial` (CsWinRT1028).
- Trim-safe code: System.Text.Json source generation, `GeneratedRegex`, no reflection. Ical.Net is rooted in `Trimming.props` because it uses reflection; any new reflection-heavy library needs the same plus a case in `tests/fixtures/rich.ics`.
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
- No API for default aliases or hotkeys for extension commands.
- After deploying, Command Palette may keep the old extension until **Reload** or a restart.

## How we work

- Small, focused commits, one logical change each, single-line messages, no attribution (global rule). Commit after each step; update README/BACKLOG in their own commit.
- Plan features before building: ask decisions one at a time, each with a recommendation first. Look up facts (source in the PowerToys repo, the SDK, this machine) rather than asking.
- Reuse what the sibling extension `../../bookmarx` already solved (build, deploy, patterns) before proposing new setup.
- Anything not tried by hand in Command Palette goes on BACKLOG **To test**; never claim it works until the user confirms. The user ticks items off.
- Ask before outward or irreversible actions: making things public, pushing tags/releases, deleting user data or packages.
- Check with the user before culling or renaming broadly; once agreed, be thorough.

## Release signing

Self-signed `CN=ben.fourie` code-signing certificate, thumbprint `982E6811C22A7186A5286A44EF6A3E1FB31F868F`, expires 2031-10-02. The private key lives in the GitHub secrets and the user's password manager only. The manifest `Publisher` must stay `CN=ben.fourie` for sideloaded releases. The Microsoft Store will assign its own identity (see BACKLOG).

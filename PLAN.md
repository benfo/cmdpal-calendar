# CmdPal Calendar: Plan

A PowerToys Command Palette extension that shows the rest of today's meetings across several calendars and joins a meeting in one action. It works like the Slack Google Calendar integration.

Status: **plan for review. No code written yet.** Research was done on 2026-10-02 against PowerToys `main` @ `19b0069`, CmdPal SDK `0.12.260812002`, and this machine.

---

## 0. Decisions so far

| Topic | Decision |
|---|---|
| Calendars | M365 work account (Graph), personal Google, Google Workspace, and ICS feeds, all merged into one view |
| M365 consent | Tenant status unknown or locked down. **Phase 0 tests consent before anything is built on Graph.** If consent is refused, the work account falls back to ICS. |
| Tentative / not responded | Shown with a tag. The Dock still treats them as "next". |
| Declined / cancelled | Hidden |
| Audience | Personal first. Designed so it can be published later: client IDs come from config, never hard-coded. |
| Runtime | .NET 10 (LTS). The template's trimmed, `IsAotCompatible` setup, with `System.Text.Json` source generation. |

---

## 1. Research findings (and corrections to the original brief)

### 1.1 Command Palette SDK (0.12, PowerToys 0.101)
- **Sections exist.** `IListItem.Section` is a string. A list item with **no command and a non-empty `Section`** renders as a section header; with no section it renders as a separator line. The Toolkit has `new Separator("Happening now")` and `new Section("Up next", items)`, which is an `IEnumerable<IListItem>` that adds the header for you. Usage: `return [.. new Section("Happening now", now), .. new Section("Up next", next)];`. Sample: `SamplePagesExtension/Pages/SectionsPages/SampleListPageWithSections.cs`.
- **Details pane:** set `ShowDetails = true` on the page and `ListItem.Details = new Details { Title, Body, HeroImage, Metadata }`. `Body` is **rendered as Markdown** (MarkdownTextBlock, tables supported). `Metadata` is an array of `DetailsElement { Key, Data }` whose `Data` is a `DetailsTags`, `DetailsLink`, `DetailsCommands` or `DetailsSeparator`. Live updates to details shipped in 0.101.
- **Context menu:** `ListItem.MoreCommands` is an array of `CommandContextItem(cmd) { Title, Icon, IsCritical, RequestedShortcut = KeyChordHelpers.FromModifiers(...) }`. A `Separator` gives a divider. Built-in commands include `OpenUrlCommand`, `CopyTextCommand`, `AnonymousCommand` and `ConfirmableCommand`. Custom commands subclass `InvokableCommand`. `CommandResult` offers `Dismiss`, `KeepOpen`, `GoBack`, `GoHome`, `Hide`, `GoToPage`, `ShowToast(ToastArgs)` and `Confirm`.
- **Tags:** `Tag { Text, Icon, ToolTip, Foreground, Background }` takes colours via `ColorHelpers`. It suits account labels and the "Tentative" tag.
- **Dock: supported (PowerToys ≥ 0.98, SDK ≥ 0.9.260303001).**
  - Override `CommandProvider.GetDockBands()` (`ICommandProvider3`). Each `ICommandItem` it returns is one band. The provider and each band's command need a **non-empty `Id`**.
  - What a band's command type does:
    - `IInvokableCommand`: one button. Clicking it calls `Invoke()` directly, which is what Join-on-click needs.
    - `IListPage`: one button per item.
    - `IContentPage`: a flyout.
  - **Live updates on a timer work.** The built-in `TimeDate/NowDockBand.cs` sets `Title`/`Subtitle` from a timer, and `PropChanged` updates the band. That band only runs its timer while the host is subscribed (`OnLoadDockBandItem`), and we copy that pattern.
  - **Known issue [#50367](https://github.com/microsoft/PowerToys/issues/50367)** (open): bands can go dead after an RDP or session switch, or after the host releases the extension. See Risks.
- **Top-level command:** `CommandItem.Title/Subtitle` changes propagate live through `PropChanged`. So the root item can read "Today's meetings: Next: Standup in 7m".
- **Template (on `main`)**:
  - Targets `net10.0-windows10.0.26100.0` (min 19041), x64 + ARM64. Uses CsWinRT 2.2 and `Shmuelie.WinRTServer`, packaged as MSIX with `com:ExeServer` + `uap3:AppExtension com.microsoft.commandpalette`.
  - Sets `IsAotCompatible`, `PublishTrimmed` (Release), trim/AOT analyzers and `ILLinkTreatWarningsAsErrors`.
  - **`PublishAot` is not set:** the template uses trimmed self-contained single-file + ReadyToRun, not full Native AOT. We keep that. Each dependency still has to be trim-safe.
  - The template pins SDK 0.11. **Bump it to 0.12.**
  - It **does not reference Windows App SDK**. We only add it if we build Windows toasts.
- **Main-branch-only APIs (avoid until they ship in a NuGet release):** `IDetails2.GetContent`, `Details.Size`, `IFormContent2.SubmitAction`, `DockLabelPresentationExtensions`/`DockLabelWidth`.
- **Process lifetime:**
  - CmdPal starts every enabled extension when CmdPal itself starts, and keeps it running while hidden.
  - On shutdown it calls `IExtension.Dispose()`, and the template's `Main` exits when the dispose event fires.
  - Background polling with a timer is the same pattern built-in extensions use (TimeDate, PerformanceMonitor), so it is reasonable. Gate the timer on whether the page or band is loaded.
- **Toasts:**
  - CmdPal-native: `ToastStatusMessage.Show()` and `CommandResult.ShowToast(ToastArgs{Message, Icon, Command})`. Both appear **inside the CmdPal window only**.
  - Real Windows toasts would need `Microsoft.WindowsAppSDK` (`AppNotificationManager`), a toast activator in the manifest, and a `Main` that handles non-COM launch arguments. Nothing in CmdPal does this today, so it is unverified.
  - Safer variant: toast buttons that are **protocol activations** (`msteams://…`, `zoommtg://…`, or https), so a click doesn't depend on the COM server.
- **Settings:**
  - `JsonSettingsManager` provides `ToggleSetting`, `TextSetting`, `ChoiceSetSetting` and `StringListSetting`.
  - Expose them with `CommandProvider.Settings`.
  - Adaptive-card forms (`FormContent` in a `ContentPage`) handle multi-field input such as "Add ICS feed (name + URL)".
- **Distribution:**
  - Through WinGet or the Microsoft Store.
  - Since 0.100, the in-app **Extension Gallery** reads `github.com/microsoft/CmdPal-Extensions`. Getting listed takes a PR adding `extensions/<author>/<name>/extension.json`, and the extension must already be on WinGet or the Store.
- **Prior art:** there is no calendar extension in the gallery yet. Useful references are the built-in `TimeDate` (live dock band, source-generated JSON) and `PerformanceMonitor` extensions, `SamplePagesExtension`, and `niels9001/Teams-meeting-control-for-Command-Palette`.

### 1.2 Meeting links (corrections to the original brief)
- **Zoom**
  - Rewrite **only `/j/<id>` and `/wc/join/<id>`**. **Keep the original host** so vanity and gov hosts work: `https://acme.zoom.us/j/123?pwd=X` → `zoommtg://acme.zoom.us/join?action=join&confno=123&pwd=X`. Carry the other query params across, but drop `tk`.
  - Zoom no longer documents the `zoommtg:` scheme ("not officially supported"), but Zoom Workplace 7.2 on this machine still registers `zoommtg`, `zoomus`, `zoomphonecall` and `sip`.
  - **`/my/<vanity>` personal rooms cannot be joined via `zoommtg:`**: open the https link. The same goes for `/w/` webinars, `/s/` start links and `/meeting/register/`.
  - Hosts: `zoom.us`, `zoom.com`, `zoomgov.com`, plus their subdomains.
- **Teams**
  - Swap the scheme `https` → `msteams` and **keep the host**: `msteams://teams.microsoft.com/l/meetup-join/...`. Never use `msteams:https://…`.
  - Keeping the host preserves `teams.live.com`, `gov.teams.microsoft.us` and `dod.teams.microsoft.us`.
  - The newer short links `https://teams.microsoft.com/meet/<digits>?p=<hash>` are generally available as of Jan 2026 and expire 60 days after the meeting. They rewrite the same way. That isn't explicitly documented, so we always set an https fallback.
  - The new Teams (MSIX) still registers both `msteams` and `ms-teams`.
- **Safe Links / redirect wrappers**
  - Teams' structured join URL isn't wrapped, but **links in the body are**: `*.safelinks.protection.outlook.com/?url=…`, `google.com/url?q=…`, and Proofpoint `urldefense.com/v3/__…__`.
  - Unwrap them by parsing the query string, up to 5 passes. Mimecast links can't be reversed and are opened as is.
  - Teams join-URL validation (MC1120871) rejects links that other tools have rewritten, so **prefer Graph `onlineMeeting.joinUrl`** whenever it exists.
- **Other services**
  - Open https as is: Google Meet (no native Windows app), Webex (no documented join scheme), GoTo, Whereby, RingCentral, Jitsi, Discord.
  - Rewrite Slack huddles: `app.slack.com/huddle/<team>/<id>` → `slack://join-huddle?team=…&id=…`.
  - Drop them entirely: **Chime** (shut down 2026-02-20), **BlueJeans** (shut down 2024) and Skype (retired 2025).
- **Detecting whether a protocol handler is registered**
  - **Registry checks and `AssocQueryString` are wrong here.** They give false negatives for packaged apps: tested on this machine, `msteams` (new Teams) and Store Slack both return `ERROR_NO_ASSOCIATION`. They also give a false positive for `tel:`, which resolves to `OpenWith.exe`.
  - **Use `Windows.System.Launcher.QueryUriSupportAsync(uri, LaunchQuerySupportType.Uri)`.** Tested on this machine: it sees both packaged and Win32 handlers. Cache the answer per scheme.
  - Launch with **`Launcher.LaunchUriAsync(nativeUri, new LauncherOptions { FallbackUri = httpsUri })`**. If no handler exists, the browser opens without the "How do you want to open this?" dialog. `FallbackUri` must be http(s), and it can't be combined with the `PreferredApplication*` options.
- **Structured fields**
  - Graph: `isOnlineMeeting`, `onlineMeetingProvider` (third-party meetings show as `unknown`), and `onlineMeeting.joinUrl`. `onlineMeetingUrl` is deprecated.
  - Google: `conferenceData.entryPoints[]` with `entryPointType`, `uri`, `pin`, `passcode`, `meetingCode` and `accessCode`; `conferenceSolution.key.type` (`hangoutsMeet` or `addOn`); and `hangoutLink`.

### 1.3 Libraries under trimming/AOT
| Need | Choice | Status |
|---|---|---|
| MS auth | `Microsoft.Identity.Client` 4.90 + `.Broker` (WAM) | Core MSAL is trimmable and uses source-generated STJ (since 4.67). The Broker (native MSALRuntime) has no AOT statement. **Spike it.** |
| MSAL cache persistence | Our own ~30-line cache: `SetBeforeAccess/AfterAccess` + DPAPI | Avoids `Microsoft.Identity.Client.Extensions.Msal`, whose AOT status is unknown. With WAM, the broker holds the refresh tokens anyway. |
| Graph | Raw `HttpClient` + source-generated DTOs | The Graph SDK/Kiota is huge and not AOT-confirmed. We only need `calendarView`. |
| Google API + auth | Raw `HttpClient` + our own PKCE loopback flow + source-generated DTOs | `Google.Apis.*` depends on Newtonsoft.Json, so it is **not** trim/AOT-safe. |
| ICS | `Ical.Net` 5.2.x (NodaTime) | No AOT statement. **Spike it** for IL2026 warnings. Fallback: our own VEVENT line parser, using Ical.Net only for RRULE expansion. |
| Token encryption | `System.Security.Cryptography.ProtectedData` (DPAPI, CurrentUser) | P/Invoke only, so trim-safe. |

### 1.4 OAuth constraints
- **Microsoft / Entra**
  - **The default consent policy (`microsoft-user-default-recommended`, fully rolled out by about July 2026) blocks user consent for `Calendars.Read`/`ReadBasic`** in work tenants. **Admin consent is needed** unless the tenant uses the older "allow user consent" setting. Personal Microsoft accounts are unaffected.
  - App registration:
    - Public client, with "Allow public client flows" enabled.
    - Redirect URIs: `ms-appx-web://microsoft.aad.brokerplugin/{client_id}` (WAM) and `http://localhost` (browser fallback).
    - Audience: multi-tenant + MSA, authority `common`, scope `Calendars.Read` (`ReadBasic` would drop the body and attendees).
  - You can register the app in the work tenant if "Users can register applications" is on. Otherwise, register it in a free personal Entra tenant as multi-tenant and still request admin consent from the work tenant.
  - Publishing: users can't consent to unverified multi-tenant apps (registered after Nov 2020) that request more than basic sign-in. **Publisher verification** requires a Microsoft AI Cloud Partner Program ID and a custom domain. Admin consent is still needed in default-policy tenants.
  - Don't borrow a first-party client ID such as Graph CLI Tools.
- **Google**
  - `calendar.readonly` / `calendar.events.readonly` are **sensitive (not restricted)** scopes. Verification requires a homepage, privacy policy, demo video and verified domain, but **no CASA security assessment**.
  - In **Testing** status, refresh tokens **expire after 7 days**, and there is a cap of 100 test users. An unverified app set to "In production" shows a warning screen and is limited to 100 users in total. Weekly expiry is a Testing-mode rule, so production status should avoid it (**confirm in Phase 0**).
  - Desktop flow: loopback `http://127.0.0.1:{random port}` + PKCE S256. The out-of-band flow is gone. The desktop "client secret" isn't actually secret.
  - Each account and client can hold at most 100 refresh tokens; once the cap is reached, Google silently revokes the oldest.
  - **Google Workspace** admins can block unconfigured third-party apps (API Controls). The work Google account may need the admin to trust our client ID.

### 1.5 Event identity (corrects "dedupe by iCal UID")
- **Graph `iCalUId` differs for each occurrence** of a recurring series. The series is identified by `seriesMasterId` + `originalStart`.
- **Google `iCalUID` is shared by all instances.** An instance is identified by `recurringEventId` + `originalStartTime`.
- ICS: `UID` + `RECURRENCE-ID`.
- So **the same meeting in two calendars won't share one key**. Dedup uses a composite key (see §4).

---

## 2. Project structure

```
cmdpal-calendar/
  PLAN.md
  Directory.Build.props            # net10, nullable, analyzers, trim/AOT warnings as errors
  Directory.Packages.props         # central package versions
  CmdPalCalendar.sln
  src/
    CmdPalCalendar/                # the extension: MSIX, COM server, CmdPal UI (from the template)
      Program.cs                   # template COM-server Main (later: handle toast activation args)
      CalendarExtension.cs         # IExtension
      CalendarCommandProvider.cs   # TopLevelCommands, GetDockBands, Settings
      Pages/
        TodayPage.cs               # ListPage with sections + details
        AccountsPage.cs            # list / add / remove / sign-in accounts
        AddIcsFeedPage.cs          # ContentPage + FormContent
      Commands/
        JoinMeetingCommand.cs      # QueryUriSupport -> LaunchUri w/ FallbackUri
        OpenInCalendarCommand.cs
        RefreshCommand.cs
        SignInCommand.cs
      Dock/
        NextMeetingDockBand.cs     # timer-driven ListItem band (NowDockBand pattern)
      Presentation/
        EventListItemFactory.cs    # CalendarEvent -> ListItem (subtitle, tags, details, MoreCommands)
        RelativeTime.cs            # "started 6 min ago", "in 7m"
      Platform/
        ProtocolLauncher.cs        # Windows.System.Launcher wrapper + per-scheme cache
        OwnerWindow.cs             # tiny Win32 window used as WAM parent
        DpapiStore.cs              # ProtectedData-backed file storage in LocalState
      Assets/                      # icons (app, Teams, Zoom, Meet, Webex, generic)
    CmdPalCalendar.Core/           # no CmdPal / WinRT dependencies, so it's unit-testable
      Model/
        CalendarEvent.cs, Attendee.cs, AccountInfo.cs, ConferenceInfo.cs, JoinTarget.cs
      Providers/
        ICalendarProvider.cs
        Graph/   GraphCalendarProvider.cs, GraphAuth.cs (MSAL), GraphDtos.cs, GraphJsonContext.cs
        Google/  GoogleCalendarProvider.cs, GoogleOAuth.cs (PKCE loopback), GoogleDtos.cs, GoogleJsonContext.cs
        Ics/     IcsCalendarProvider.cs
      Links/
        LinkPipeline.cs            # candidates -> unwrap -> classify -> JoinTarget
        UrlUnwrapper.cs            # SafeLinks, google.com/url, Proofpoint
        HtmlText.cs                # HTML -> text (strip tags, decode entities, keep hrefs)
        MeetingServices.cs         # regex + rewrite table per service
        DialInExtractor.cs
      Aggregation/
        EventMerger.cs             # dedup + account tagging
        EventFilter.cs             # declined/cancelled/all-day/eventType rules
        TodaySnapshot.cs           # now/next/all-day partition at a given instant
      Sync/
        CalendarSyncService.cs     # polling loop, per-account backoff, snapshot events
        SnapshotCache.cs           # last good snapshot persisted (DPAPI) for instant startup
      Abstractions/
        IClock.cs, ISecretStore.cs, ITokenProvider.cs
  tests/
    CmdPalCalendar.Core.Tests/
      Links/fixtures/              # real (scrubbed) invite bodies: Teams, Zoom, SafeLinks, Google-wrapped...
      LinkPipelineTests.cs, EventMergerTests.cs, TodaySnapshotTests.cs, DialInExtractorTests.cs
```

Why Core is a separate project: the link pipeline, merge logic and time math hold most of the bugs, and they can be tested without CmdPal running. Core must still be trim-safe, because it ships inside the trimmed extension.

---

## 3. Provider interface and normalised event model

Interface sketches to support review. These are not final code.

```csharp
enum ProviderKind { Graph, Google, Ics }
enum ResponseStatus { Organizer, Accepted, Tentative, NotResponded, Declined, None }
enum BusyStatus { Free, Tentative, Busy, OutOfOffice, WorkingElsewhere, Unknown }

record AccountInfo(
    string AccountId,          // stable local ID (GUID), not the email
    ProviderKind Kind,
    string DisplayName,        // "Work (dealx.com)", "Personal Gmail", "Family ICS"
    string? Upn,               // email/UPN where known
    string ColorHex,           // used for the account Tag
    IReadOnlyList<string> CalendarIds); // which calendars on the account to include (default: primary)

record ConferenceInfo(          // what the provider told us in structured fields
    string? JoinUrl,           // Graph onlineMeeting.joinUrl / Google entryPoints[video].uri / hangoutLink
    string? ProviderHint,      // teamsForBusiness / hangoutsMeet / addOn name
    IReadOnlyList<DialIn> Phones, string? MeetingCode, string? Passcode);

record CalendarEvent(
    string AccountId, ProviderKind Kind, string CalendarId,
    string ProviderEventId,            // Graph id / Google id / ICS UID+RECURRENCE-ID
    string? ICalUid, string? SeriesId, DateTimeOffset? OriginalStart,
    string Title,
    DateTimeOffset Start, DateTimeOffset End,   // always absolute; UI converts to local
    bool IsAllDay, DateOnly? AllDayDate,
    bool IsCancelled, ResponseStatus Response, BusyStatus ShowAs,
    string? EventType,                 // Google eventType (focusTime, outOfOffice, workingLocation...)
    string? Location,
    string? BodyText,                  // stripped, truncated (~4 KB)
    string? BodyHtmlForLinks,          // raw HTML kept only for link extraction, not persisted
    Attendee? Organizer, IReadOnlyList<Attendee> Attendees,
    string? WebLink,                   // open-in-calendar URL
    ConferenceInfo? Conference);

interface ICalendarProvider
{
    ProviderKind Kind { get; }
    Task<AuthState> GetAuthStateAsync(AccountInfo account, CancellationToken ct);  // SignedIn / NeedsInteraction / Error
    Task<AccountInfo> AddAccountAsync(AddAccountRequest request, CancellationToken ct); // interactive; user-initiated only
    Task RemoveAccountAsync(AccountInfo account);
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        AccountInfo account, DateTimeOffset from, DateTimeOffset to, CancellationToken ct); // silent auth only
}
```

The `JoinTarget` is computed from the event; providers don't produce it:

```csharp
record JoinTarget(MeetingService Service, Uri HttpsUrl, Uri? NativeUri,
                  LinkSource Source,   // Structured / Location / Body
                  string? MeetingId, string? Passcode, IReadOnlyList<DialIn> DialIns);
```

Provider specifics:

| | Graph | Google | ICS |
|---|---|---|---|
| Endpoint | `GET /me/calendars/{id}/calendarView?startDateTime&endDateTime` (with `/me/calendarView` for the primary calendar) | `GET /calendars/{id}/events?singleEvents=true&orderBy=startTime&timeMin&timeMax&fields=…` | HTTP GET with `ETag`/`If-Modified-Since` |
| Recurrence | Expanded by the server | Expanded by the server | Ical.Net expands RRULE/EXDATE/RECURRENCE-ID |
| Request details | `$select=subject,start,end,isAllDay,showAs,responseStatus,isCancelled,isOnlineMeeting,onlineMeetingProvider,onlineMeeting,location,body,bodyPreview,organizer,attendees,iCalUId,seriesMasterId,type,webLink`. Header `Prefer: outlook.timezone="UTC"`. Page via `@odata.nextLink`. | Own response from `attendees[self=true].responseStatus`. Fallback when there are no attendees: the user is the organizer. | Windows TZIDs mapped via `TimeZoneInfo.TryConvertWindowsIdToIanaId` |
| Body | HTML (needed for hrefs in links); `bodyPreview` for display | `description` (HTML-ish) | `DESCRIPTION`, plus `X-ALT-DESC` if present |
| Open-in-calendar | `webLink` (or `outlook.office.com/calendar/item/{id}`) | `htmlLink` | none |

---

## 4. Filtering, merging, dedup

**Filter (per event, before merging):**
- Drop: `IsCancelled`, `Response == Declined`, Google `eventType` ∈ {`workingLocation`, `outOfOffice`, `focusTime`, `birthday`}, and Graph `showAs == free` **all-day** events (holidays and the like).
- **All-day events:** removed from the timed flow. Shown as **one compact row** at the bottom ("All day: Public holiday · Alice OOO"), and never in the Dock.
- Tentative / not responded: kept, with a `Tentative` / `No response` tag.

**Merge and dedup (across accounts):**
1. Group candidates by **`(Start UTC, End UTC)`**.
2. Within a group, two events are the same meeting if **any** of these holds:
   - the same normalised join URL (after unwrapping);
   - the same `ICalUid` (this catches Google↔Google and ICS↔Google);
   - the same normalised title (case-folded, with "FW:", "RE:", "Updated invitation:" stripped).
3. Keep one **primary** copy. Prefer, in order: the copy with a structured join URL, then the one where you are organizer or accepted, then the first account in your configured order.
   - Union the `AccountId`s so every account shows as a tag.
   - Union the attendees.
4. Sort by `Start`, then `Title`.

This handles the common case where your work M365 calendar and a Google calendar both hold the same invite.

---

## 5. Link-detection pipeline

```
CalendarEvent
  ├─ 1. Structured: Graph onlineMeeting.joinUrl | Google conferenceData video entryPoint | hangoutLink
  ├─ 2. Location string
  └─ 3. Body: HTML → (keep <a href> targets + visible text) → HTML-entity decode
        │
        ▼
  Extract URL candidates (generic URL regex, with trailing punctuation and '>' trimmed)
        │
        ▼
  Unwrap (loop ≤5): SafeLinks ?url= | google.com/url?q= | urldefense v3 | &amp; → &
        │
        ▼
  Classify against MeetingServices table (ordered):
     Teams  ((gov|dod)\.)?teams\.microsoft\.(com|us) | teams\.live\.com  → /l/meetup-join/… | /meet/\d+
     Zoom   ([\w-]+\.)?zoom(gov)?\.(us|com)  → /j/\d+ | /wc/join/\d+ | /my/… | /w/… | /s/…
     Meet   meet\.google\.com/[a-z]{3}-[a-z]{4}-[a-z]{3} (+ /lookup/)
     Webex  ([\w-]+\.)?webex\.com/(meet|join)/… | …/j\.php\?MTID=
     Slack  app\.slack\.com/huddle/…
     GoTo, Whereby, RingCentral, Jitsi, Discord (open https)
        │
        ▼
  Pick best: earliest stage wins (structured > location > body). Within a stage,
  a known service beats an unknown one, and the first occurrence wins. Ignore
  "teams.microsoft.com/meetingOptions" and Zoom "/meeting/register" style
  non-join URLs.
        │
        ▼
  Rewrite to native (only where safe):
     Teams  https://H/… → msteams://H/…   (same path + query)
     Zoom   https://H/j/ID?pwd=P&… → zoommtg://H/join?action=join&confno=ID&pwd=P (drop tk)
            /my/, /w/, /s/, register → no native, https only
     Slack  /huddle/T/C → slack://join-huddle?team=T&id=C
     Others → none
        │
        ▼
  DialInExtractor: structured phones first (Google phone entryPoints; Graph onlineMeeting
  tollNumber/phones/conferenceId — check these field names first), then body regexes:
     Meeting ID   (?i)\bMeeting\s*ID\s*[:：]?\s*(\d{3}(?:[\s-]?\d{3,4}){2,4})
     Passcode     (?i)\b(?:Passcode|Password|Pass\s*code|PIN)\s*[:：]?\s*([A-Za-z0-9]{4,12})\b
     Conf ID      (?i)Phone\s+conference\s+ID\s*[:：]?\s*([\d\s]{6,15})#
     One-tap      tel:\+?[\d\-\s().]{7,}(?:[,;]+[\d*#]+)*  |  \+\d[\d\s\-()]{7,}(?:,+[\d*#]+)+
        │
        ▼
  JoinTarget
```

**Launching (`JoinMeetingCommand`):**
1. If there is a `NativeUri`, call `Launcher.QueryUriSupportAsync(native, LaunchQuerySupportType.Uri)`. The result is cached per scheme for 10 minutes, and invalidated after a failed launch.
2. If the scheme is supported, call `Launcher.LaunchUriAsync(native, new LauncherOptions { FallbackUri = https })`.
3. If it isn't supported, or there is no native URI, call `Launcher.LaunchUriAsync(https)`.
4. Return `CommandResult.Dismiss()`. If launching fails, return `ShowToast("Couldn't open …; link copied")` and copy the https URL to the clipboard.

Tests: a fixture corpus of real (scrubbed) invite bodies. Cases: Teams with SafeLinks, a Zoom vanity host, Zoom `/my/`, Google-wrapped Zoom with `%3Fpwd%3D`, Teams `/meet/` short link, gov cloud, Meet via Google `conferenceData`, Webex `j.php`, and an invite with both a `meetingOptions` link and a join link.

---

## 6. Auth and token storage

All interactive sign-in happens **only from a user action** (an Accounts page command). Polling uses silent token acquisition only. When that fails, the account is marked `NeedsInteraction`, and the UI shows a "Sign in again" item instead of throwing.

### Microsoft 365 (MSAL + WAM)
- `PublicClientApplicationBuilder.Create(clientId).WithAuthority("https://login.microsoftonline.com/common").WithBroker(new BrokerOptions(Windows)).WithParentActivityOrWindow(() => OwnerWindow.Handle)`.
- **Parent window:** the COM server has no window, and `GetConsoleWindow` doesn't apply. `OwnerWindow` creates a small top-level Win32 window ("Signing in to Microsoft…") that is shown only during interactive auth. That also gives the user context first, which Microsoft recommends. If it misbehaves, the fallback is `GetForegroundWindow()` captured while CmdPal is still in the foreground.
- First try `AcquireTokenSilent(scopes, PublicClientApplication.OperatingSystemAccount)`, which is a silent sign-in with the Windows work account. Then try interactive.
- Scope: `Calendars.Read` (plus `offline_access` and `User.Read` for the display name).
- Cache: our own serializer writes the MSAL cache blob DPAPI-encrypted to `LocalState\msal.cache.bin`. The broker keeps the refresh tokens itself.
- **Consent:** if sign-in returns `AADSTS65001`/`90094` ("admin approval required"), show a page explaining that admin consent is needed. It includes the admin-consent URL `https://login.microsoftonline.com/{tenant}/adminconsent?client_id=…`, a copy command, and an "Use ICS feed instead" link.

### Google (personal + Workspace)
- Our own PKCE loopback flow:
  1. `HttpListener` on `http://127.0.0.1:{ephemeral}/`.
  2. Open the system browser at the auth URL (`access_type=offline&prompt=consent&scope=openid email https://www.googleapis.com/auth/calendar.readonly`).
  3. Receive the code, exchange it for tokens, and serve a "You can close this tab" page.
- **Scope:** `calendar.readonly`. It is needed to list calendars and read events from several calendars. `calendar.events.readonly` + `calendar.calendarlist.readonly` is narrower; check its classification in the console.
- The refresh token is stored in a DPAPI-encrypted file `LocalState\tokens\google-{accountId}.bin`. The access token is kept in memory only. Handle `invalid_grant` by marking the account `NeedsInteraction`.
- **One Google Cloud project, "External" user type, set to "In production" (unverified).** That means one warning screen per account, and it avoids the weekly expiry of Testing mode. The Workspace account may need its admin to trust the client ID under API Controls.

### ICS
- Feed URLs are often secret capability URLs (Google "secret address", Outlook publish links), so they are **stored DPAPI-encrypted** as well, not in the plain settings JSON. Settings only hold `{accountId, displayName, color}`.

### Storage summary
`DpapiStore` wraps `ProtectedData.Protect(bytes, entropy: appSpecific, DataProtectionScope.CurrentUser)` and writes files under `ApplicationData.Current.LocalFolder` (the package's LocalState, which is removed on uninstall). Credential Manager is not used: the MSAL blob can exceed 2560 bytes, and DPAPI files are simpler.

Client IDs live in a `clients.json` config, so users can bring their own IDs later. No secrets are committed. The Google desktop "secret" counts as non-secret but is still kept out of the repo.

---

## 7. Caching and refresh

- **Window:** local midnight to the next local midnight, recalculated when the date changes. Optional, see open questions: once today has no more meetings, show tomorrow's first meeting.
- **`CalendarSyncService`**
  - Fetches all accounts in parallel with a per-account timeout of 15 s. Each account keeps **its own last-good result**, so one failing account doesn't blank the others.
  - Publishes an immutable `CalendarSnapshot { Events, FetchedAt, AccountStatuses }`.
  - Triggers:
    - timer every **5 min** (configurable 2–30);
    - **on page open** if the snapshot is older than 60 s. The cached data shows instantly and `IsLoading` stays true while the refresh runs;
    - manual "Refresh" command (Ctrl+R);
    - a midnight rollover timer;
    - optionally, when the network comes back (`NetworkInformation.NetworkStatusChanged`).
  - Backoff: on HTTP 429/5xx, honour `Retry-After` and back off exponentially per account (capped at 30 min).
- **Persisted snapshot:** the last good snapshot is written DPAPI-encrypted to `LocalState\snapshot.bin` (without raw HTML bodies), so the Dock and the page have data the moment the extension starts.
- **Local ticking:**
  - `TodaySnapshot.At(now)` puts events into Now / Next / AllDay with no network call.
  - The Dock band and the top-level subtitle recompute every 30 s, aligned to minute boundaries, plus exactly at the next start or end time.
  - The page subtitles ("started 6 min ago") recompute while the page is loaded, via `PropChanged` on the existing `ListItem`s rather than rebuilding the list.
- **Gating:** timers run only while something is subscribed (the page, the band or the top-level item), following `NowDockBand`'s `OnLoad…` pattern. The 5-minute fetch timer always runs while any account exists, because the Dock depends on it. It is cheap.

---

## 8. UI: pages, commands, dock

### Top-level command: "Today's meetings"
- Title: `Today's meetings`. Subtitle (live): `Next: Standup in 7m`, `Now: Design review (ends 10:30)`, or `No more meetings today`.
- Opens `TodayPage`. A second top-level item, "Join next meeting", joins straight from the palette.
- Provider-level `Settings` and an "Accounts" context item.

### TodayPage (`ListPage`, `ShowDetails = true`, `PlaceholderText = "Filter meetings"`)
```
── Happening now ───────────────────────────
 [Teams] Design review                       [Work]
         10:00–10:45 · started 6 min ago
── Up next ─────────────────────────────────
 [Zoom]  Vendor sync                         [Personal] [Tentative]
         11:30–12:00 · in 44 min
 [ -- ]  1:1 with Sam (no link)              [Work]
         14:00–14:30 · in 3 h
── All day ─────────────────────────────────
 [cal]   Public holiday · Alice OOO
```
- **Sections:** `Happening now` (Start ≤ now < End), `Up next` (Start > now, today), and `All day` (one row). "Happening now" also includes meetings that start within the next 5 minutes, labelled "starts in 3 min", because Slack does the same.
- **Item:**
  - Icon = service logo (Teams, Zoom, Meet, Webex, generic video, or a calendar icon when there's no link).
  - Title = the event title.
  - Subtitle = the time range and a relative phrase.
  - Tags = account (coloured), Tentative / No response, and "Organizer" when relevant.
- **Primary command:** Join, if there is a `JoinTarget`. Otherwise "Open in calendar", or a no-op for ICS events without a link.
- **MoreCommands** (each shown only when it applies):
  - Join in browser (Ctrl+Shift+Enter)
  - Open in calendar (Ctrl+O)
  - Copy join link (Ctrl+C)
  - Copy dial-in (one-tap string)
  - Copy meeting ID / passcode (`ID 123 456 789 · Passcode abc123`)
  - separator
  - Refresh (Ctrl+R), Accounts…, Settings…
- **Details:**
  - Title: the event title.
  - Body (Markdown):
    - When, plus location;
    - **Organizer**;
    - **Attendees**: up to 8, with ✓/?/✗ response markers and "+N more";
    - the first ~6 non-empty lines of the description, with signature and boilerplate trimmed (Teams "Microsoft Teams meeting / Join on your computer…" and Zoom footers are stripped);
    - the description is Markdown-escaped.
  - Metadata: Join (`DetailsLink`), Service, Meeting ID/Passcode, and Accounts (`DetailsTags`).
- **Empty / error states** via `EmptyContent`: "No more meetings today 🎉", "Sign in to Work (dealx.com)" (a command row), and "Couldn't refresh Personal Gmail. Showing data from 12 min ago".

### AccountsPage
- One row per account with its status tag (Connected / Sign-in needed / Admin consent required / Error).
- Commands: Sign in, Sign out/Remove, Choose calendars (a sub-list page with toggles), and Rename/colour.
- Add items: "Add Microsoft 365 account", "Add Google account", and "Add ICS feed" (a FormContent with name + URL).

### Dock band: "Next meeting"
- `GetDockBands()` returns one `ListItem`-based band, with `Id = "cmdpal-calendar.next"` and a stable command Id.
- **Text**: `Standup · 7m`, then `Standup · now`, then `Standup · 12m left` while it runs. Between meetings it shows the next one. After the last meeting it shows `No meetings`, or the band is hidden (setting).
- **Click** runs `JoinMeetingCommand` for that meeting, which is an `InvokableCommand` invoked directly. The band's `Command` is swapped when the "next meeting" changes. With no link, the click opens `TodayPage`.
- If several meetings overlap: show the one that started most recently, plus "+1".
- Optional second band button (setting): "Today", which opens the page.
- Not using main-only `DockLabelWidth` / tabular digits yet, so the band width will shift slightly as the countdown changes.

### Settings (`JsonSettingsManager`)
- Refresh interval: 2/5/10/15/30 min
- Show all-day row: on/off
- Show tentative / no-response: on/off (default on)
- "Happening now" lead time: 0/1/2/5 min
- Dock: show title / time only / hide when no meetings
- Prefer native app for Zoom/Teams: on/off
- Time format: 12/24 h (default from the system)
- (Phase 5) Meeting-start toast: off / 1 min / 2 min before

### Windows toasts (optional, Phase 5)
- "Vendor sync starts in 1 min", with a **Join** button.
- The button activates the protocol directly (native or https), so a click doesn't depend on the COM server.
- Requires adding the Windows App SDK, manifest entries and `Main` handling. Only built if the spike works without destabilising the COM server.

---

## 9. Risks and open questions

### Risks
| # | Risk | Impact | Mitigation |
|---|---|---|---|
| R1 | **The work M365 tenant blocks user consent to `Calendars.Read`** (default policy since about July 2026) | Graph path unusable without IT | Phase 0 consent test before any Graph work. Request admin consent with a clear justification (read-only, one user). Fallback: Outlook "Publish calendar" ICS (the tenant may also block external sharing). Full details are needed to get join links. |
| R2 | WAM from a windowless out-of-proc COM server | Interactive sign-in fails or the dialog hides behind CmdPal | Own-window approach (OwnerWindow) proven in Phase 0. Browser fallback via the `http://localhost` redirect. |
| R3 | MSAL Broker / Ical.Net not trim-safe | Release build breaks or misbehaves at runtime | Phase 0 trimmed-publish spike, with warnings as errors. Fallbacks: browser auth without the broker; our own ICS parser. |
| R4 | **Dock bands die after an RDP or session switch** ([#50367](https://github.com/microsoft/PowerToys/issues/50367)) | Countdown freezes or disappears | Keep the band stateless and quick to re-create, so values come from the snapshot when it reloads. Track the issue. Don't fight `Dispose`. |
| R5 | `zoommtg:` is undocumented; `msteams://…/meet/` is undocumented | Native join stops working after a Zoom or Teams update | Always pass an https `FallbackUri`. A "Prefer native app" setting. A fixture test per format. |
| R6 | Google OAuth: a Workspace admin blocks the app; an unverified app shows warnings; Testing mode's weekly expiry | Google work calendar unavailable, or re-auth every week | Production-unverified status for personal use. Ask the Workspace admin to trust the client ID. ICS secret address as a fallback (lags by hours). |
| R7 | Cross-provider dedup false positives or negatives | Duplicate or missing rows | Composite key + fixture tests. Account tags make duplicates visible rather than harmful. |
| R8 | Time zones and DST (ICS Windows TZIDs, all-day dates) | Wrong times | Work in UTC internally. Use `DateOnly` for all-day dates. Tests with fixed clocks across DST boundaries. |
| R9 | SDK churn (0.x versions; main-only APIs) | Breaking changes on PowerToys updates | Pin the SDK, avoid main-only APIs, test against the weekly preview channel. |
| R10 | Sensitive data at rest (titles, attendees, secret ICS URLs) | Privacy | All persisted data is DPAPI-encrypted in LocalState. No logging of bodies or URLs at the default log level. |

### Open questions for you
1. **When the day is over**, should the page and Dock show tomorrow's first meeting ("Tomorrow 09:00 · Standup")? Or stay strictly "today"?
2. **Which calendars per account?** Primary only by default, or all calendars you're subscribed to (shared team calendars, holidays)?
3. **Dock text**: is `Standup · 7m` right, or do you want the time (`Standup 10:00`)? Should it truncate titles at ~20 chars?
4. **Windows toasts** before meetings: wanted (Phase 5), or skip?
5. Is a **"Join next meeting"** top-level command with a global hotkey (CmdPal lets you bind hotkeys to top-level commands) useful in addition to the Dock?
6. For M365: would you ask IT for admin consent on a personal app registration, or go straight to the published-ICS fallback for work?

---

## 10. Phased build order

### Phase 0: Spikes and setup (de-risk before building)
1. Scaffold from the CmdPal template (`net10.0-windows10.0.26100.0`), bump the SDK to **0.12.260812002**, deploy, and confirm it loads in PowerToys 0.101.
2. **Entra app registration + consent test.** Register a public client and try to sign in with the work account.
   - If you get "Need admin approval": decide on R1 now (request consent, or ICS fallback).
   - Also try `OperatingSystemAccount` silent sign-in.
3. **WAM spike** inside the extension: owner window, interactive + silent, and a trimmed Release publish (check for `msalruntime.dll` in the MSIX).
4. **Launcher spike**: `QueryUriSupportAsync` + `LaunchUriAsync(FallbackUri)` from the COM server, for `msteams://` and `zoommtg://`.
5. **Ical.Net trim spike** (can run in parallel; not needed until Phase 4).
6. Collect 10–20 real invite bodies (scrubbed) for the link fixtures.

**Exit:** the extension loads; a Graph token is acquired (or the fallback is decided); a Teams and a Zoom link launch the native apps from the extension.

### Phase 1: Graph + Today page + Join (Teams/Zoom)
1. Core models, `IClock`, `TodaySnapshot`, `EventFilter` (declined, cancelled, all-day row), with tests.
2. Link pipeline for **Teams and Zoom**, plus unwrapping (SafeLinks, Google, Proofpoint) and the HTML-to-text step, with a fixture test suite.
3. `GraphCalendarProvider` (calendarView, paging, source-generated DTOs) + `GraphAuth` (MSAL/WAM, DPAPI cache).
4. `CalendarSyncService` (5-min timer, on-open refresh, manual refresh) + persisted snapshot.
5. `TodayPage`: sections, subtitles with relative time, account/tentative tags, details pane, MoreCommands (join in browser, open in calendar, copy link, refresh).
6. `JoinMeetingCommand` + `ProtocolLauncher`.
7. Minimal Accounts page: add/sign in/remove the Microsoft account. Settings: refresh interval.

**Exit:** for a full workday, the page is accurate and "Join" opens Teams/Zoom natively.

### Phase 2: Dock and live top-level item
1. `NextMeetingDockBand` (timer, load-gated, command swapping, overlap handling).
2. Live subtitle on the top-level command + a "Join next meeting" top-level command.
3. Check that it recovers after sleep/resume, lock/unlock and an RDP switch (R4).

### Phase 3: Google (personal + Workspace) and merging
1. `GoogleOAuth` PKCE loopback + DPAPI refresh token; Cloud project in production-unverified status.
2. `GoogleCalendarProvider` (events.list with singleEvents, eventType filtering, conferenceData).
3. Choose calendars per account (calendarList).
4. `EventMerger` dedup + multi-account tags, with tests.
5. Google Meet in the link pipeline (https only).

### Phase 4: ICS feeds
1. `IcsCalendarProvider` (Ical.Net or our own parser, per the Phase 0 spike), ETag caching, Windows TZID mapping.
2. "Add ICS feed" form; feed URLs encrypted.
3. Becomes the M365 fallback if R1 turned out badly. **Move it to right after Phase 1 in that case.**

### Phase 5: Polish
1. Remaining services: Webex, GoTo, Whereby, RingCentral, Jitsi and Discord (all https), plus the Slack huddle rewrite.
2. Dial-in / meeting ID / passcode extraction + copy commands.
3. Full settings; the Accounts page reports status (admin consent required, re-auth).
4. Optional Windows toasts with a protocol-activated Join button.
5. Error and empty states; logging (no PII).

### Phase 6 (optional): Publishing
1. Bring-your-own client ID UI; docs for Entra/Google setup and admin consent.
2. Entra publisher verification (needs a CPP ID + domain); Google sensitive-scope verification (homepage, privacy policy, demo video, domain).
3. Package signing; WinGet manifest and/or Store submission; PR to `microsoft/CmdPal-Extensions`.

---

## 11. Sources

- PowerToys CmdPal source (`main` @ 19b0069): https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal (interface definitions, Toolkit, ExtensionTemplate, `ext/SamplePagesExtension`, `ext/Microsoft.CmdPal.Ext.TimeDate/NowDockBand.cs`, `WinRTExtensionService.cs`)
- SDK NuGet: https://www.nuget.org/packages/Microsoft.CommandPalette.Extensions
- Dock docs: https://learn.microsoft.com/windows/powertoys/command-palette/adding-dock-support. Dock issue: https://github.com/microsoft/PowerToys/issues/50367
- Extension Gallery: https://github.com/microsoft/CmdPal-Extensions
- MeetingBar (link regexes and rewrites): https://github.com/leits/MeetingBar
- Teams deep links: https://learn.microsoft.com/microsoftteams/platform/concepts/build-and-test/deep-links. Short links MC772556; join-URL validation MC1120871
- Zoom URL scheme threads: https://devforum.zoom.us/t/is-there-a-documentation-of-the-zoommtg-parameters/67755
- Launcher: https://learn.microsoft.com/uwp/api/windows.system.launcher.queryurisupportasync, https://learn.microsoft.com/uwp/api/windows.system.launcheroptions.fallbackuri
- Graph calendarView / event / delta: https://learn.microsoft.com/graph/api/user-list-calendarview, https://learn.microsoft.com/graph/api/resources/event, https://learn.microsoft.com/graph/api/event-delta
- MSAL WAM: https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/desktop-mobile/wam. MSAL changelog: https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/main/CHANGELOG.md
- Entra consent policies: https://learn.microsoft.com/entra/identity/enterprise-apps/manage-app-consent-policies. Publisher verification: https://learn.microsoft.com/entra/identity-platform/publisher-verification-overview
- Google OAuth native apps: https://developers.google.com/identity/protocols/oauth2/native-app. Testing-mode limits: https://support.google.com/cloud/answer/15549945. Sensitive-scope verification: https://developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification
- Google Calendar events: https://developers.google.com/workspace/calendar/api/v3/reference/events
- Credential Locker limits: https://learn.microsoft.com/windows/apps/develop/security/credential-locker
- .NET support policy: https://learn.microsoft.com/dotnet/core/releases-and-support

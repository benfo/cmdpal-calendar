# CmdPal Calendar: Plan

A Command Palette extension that shows the rest of today's meetings across several calendars and joins them in one action, like the Slack Google Calendar integration.

What's built and what's next is tracked in [BACKLOG.md](BACKLOG.md). This file holds the decisions, design and research behind it. Research was done on 2026-10-02 against PowerToys `main` @ `19b0069` and CmdPal SDK `0.12.260812002`.

## Decisions

| Topic | Decision |
|---|---|
| Calendars | ICS feeds first (built). Then M365 work (Graph), personal Google and Google Workspace, all merged into one view. |
| M365 consent | Tenant is probably locked down. Test admin consent before building on Graph; ICS is the fallback. |
| Tentative / not responded | Shown with a tag; the Dock still counts them as "next". |
| Declined / cancelled | Hidden. |
| All-day | One compact section; never in the Dock. |
| Audience | Personal first. Client IDs come from config, never hard-coded, so it can be published later. |
| Runtime | .NET 10, the template's trimmed `IsAotCompatible` setup (not full `PublishAot`), System.Text.Json source generation. |

## Command Palette SDK notes (0.12, PowerToys 0.101)

- **Sections:** `ListItem.Section`. In the Toolkit, use `new Section("Up next", items)`; a `Separator` with a title is a header.
- **Details:** `ShowDetails = true` on the page and `ListItem.Details`. The `Body` is rendered as Markdown. `Metadata` holds `DetailsTags`, `DetailsLink` and `DetailsSeparator`.
- **Context menu:** `MoreCommands` takes `CommandContextItem`s with a `RequestedShortcut`. Built-in commands include `OpenUrlCommand`, `CopyTextCommand` and `AnonymousCommand`.
- **Dock** (PowerToys ≥ 0.98):
  - Override `GetDockBands()`; the provider and each band need a non-empty `Id`.
  - A band whose command is an `IInvokableCommand` is invoked on click.
  - Setting `Title` or `Subtitle` from a timer updates the band live; copy `TimeDate/NowDockBand.cs`, which only runs its timer while the band is loaded.
- **Top-level command:** changes to `Title` and `Subtitle` update live.
- **Lifetime:**
  - Extensions start with Command Palette, stay alive while it's hidden, and get `Dispose()` on shutdown.
  - A polling timer is fine; built-in extensions use them too.
- **Toasts:** the Toolkit's toasts only show inside the Command Palette window. Real Windows notifications need the Windows App SDK, an activator and a `Main` that handles activation; this is unverified.
- **Avoid until released:** `IDetails2.GetContent`, `Details.Size`, `IFormContent2.SubmitAction` and `DockLabelWidth` exist on `main` only.
- **Distribution:** WinGet or the Store, then a PR to `microsoft/CmdPal-Extensions` for the in-app gallery.

## Meeting links

### Finding the link
1. Structured fields first:
   - Graph: `onlineMeeting.joinUrl`. `onlineMeetingUrl` is deprecated, and third-party meetings show `onlineMeetingProvider: unknown`.
   - Google: `conferenceData.entryPoints[]`, where `entryPointType` is `video`/`phone`/`sip`/`more`, alongside `uri`, `pin`, `passcode` and `meetingCode`; then `hangoutLink`.
2. Then the location, then the body: convert HTML to text, keep `<a href>` targets, and decode entities.
3. **Unwrap redirects before matching** (up to 5 passes):
   - Outlook Safe Links (`*.safelinks.protection.outlook.com/?url=`): parse the query string; don't regex past `&data=`.
   - `google.com/url?q=`.
   - Proofpoint `urldefense.com/v3/__…__`.
   - Mimecast can't be reversed.
4. Pick the best candidate: an earlier stage wins; within a stage, a known service beats an unknown one. Ignore non-join links such as Teams `meetingOptions` and Zoom `/meeting/register`.
5. Prefer the structured Teams `joinUrl`. Teams' join-URL validation (MC1120871) rejects links that other tools have rewritten.

### Rewriting to the desktop app
| Service | Match | Rewrite |
|---|---|---|
| Teams | `((gov\|dod)\.)?teams\.microsoft\.(com\|us)` or `teams\.live\.com`, path `/l/meetup-join/…` or `/meet/\d+?p=…` | `https` → `msteams`, keeping host, path and query. Never `msteams:https://…`. `/meet/` short links are generally available since Jan 2026, expire 60 days after the meeting, and aren't documented for `msteams:`, so always keep the https fallback. The new Teams (MSIX) still registers `msteams` and `ms-teams`. |
| Zoom | `([\w-]+\.)?zoom(gov)?\.(us\|com)` with `/j/<id>` or `/wc/join/<id>` | `zoommtg://<same host>/join?action=join&confno=<id>&pwd=<pwd>`, carrying other query params but dropping `tk`. The scheme is undocumented ("not officially supported") but Zoom Workplace 7.2 registers it. |
| Zoom, no rewrite | `/my/<vanity>`, `/w/`, `/s/`, `/meeting/register/` | Open https; `zoommtg` can't join these. |
| Slack huddle | `app\.slack\.com/huddle/<team>/<id>` | `slack://join-huddle?team=…&id=…` |
| Others | Meet `meet\.google\.com/[a-z]{3}-[a-z]{4}-[a-z]{3}`, Webex `/meet/`, `/join/` and `j.php?MTID=`, GoTo, Whereby, RingCentral, Jitsi, Discord | Open https as is. |
| Dead | Chime (shut down Feb 2026), BlueJeans (2024), Skype (2025) | Drop. |

### Launching
- **Don't use registry checks or `AssocQueryString`.**
  - They miss MSIX handlers; tested here, the new Teams `msteams` and Store Slack both return `ERROR_NO_ASSOCIATION`.
  - They also report `tel:` as handled because it resolves to `OpenWith.exe`.
- **Check:** `Launcher.QueryUriSupportAsync(uri, LaunchQuerySupportType.Uri)` sees both packaged and Win32 handlers. Cache the result per scheme.
- **Launch:** `Launcher.LaunchUriAsync(native, new LauncherOptions { FallbackUri = https })`.
  - If no app is installed, the browser opens with no "How do you want to open this?" prompt.
  - `FallbackUri` must be http(s) and can't be combined with `PreferredApplication*`.
- **If launching fails,** copy the https link and show a toast.

### Dial-in details
Use structured data first: Google `phone` entry points; Graph `onlineMeeting.tollNumber`, `phones` and `conferenceId` (check these field names before relying on them). Otherwise, match the body:

| Value | Regex |
|---|---|
| Meeting ID | `(?i)\bMeeting\s*ID\s*[:：]?\s*(\d{3}(?:[\s-]?\d{3,4}){2,4})` |
| Passcode | `(?i)\b(?:Passcode\|Password\|Pass\s*code\|PIN)\s*[:：]?\s*([A-Za-z0-9]{4,12})\b` |
| Teams conference ID | `(?i)Phone\s+conference\s+ID\s*[:：]?\s*([\d\s]{6,15})#` |
| One-tap dial | `tel:\+?[\d\-\s().]{7,}(?:[,;]+[\d*#]+)*` or `\+\d[\d\s\-()]{7,}(?:,+[\d*#]+)+` |

## Events

**Fields we need, whatever the source:**
- account and calendar
- provider event ID, iCal UID, series ID and original start
- title
- start and end (always absolute), and whether it's all-day
- cancelled, my response status and show-as
- Google `eventType`
- location, body text, organizer and attendees
- web link
- structured conference info (join URL, phones, meeting code, passcode)

**Filter:**
- Drop cancelled and declined events.
- Drop Google `workingLocation`, `outOfOffice`, `focusTime` and `birthday` events.
- Drop Graph all-day events with `showAs == free`.

**Identity differs by source:**
- Graph's `iCalUId` is **different for each occurrence**; use `seriesMasterId` + `originalStart`.
- Google's `iCalUID` is **shared by every instance**; use `recurringEventId` + `originalStartTime`.
- ICS: `UID` + `RECURRENCE-ID`.

**Merging across calendars:**
1. Group by start and end.
2. Within a group, two events are the same meeting if they share a normalised join link, an iCal UID, or a normalised title (with "FW:", "RE:" and "Updated invitation:" stripped).
3. Keep the copy that has a structured join URL; failing that, the one you organise or accepted.
4. Combine the calendar tags and attendees.

## Providers

| | Graph | Google | ICS |
|---|---|---|---|
| Endpoint | `GET /me/calendars/{id}/calendarView?startDateTime&endDateTime` | `GET /calendars/{id}/events?singleEvents=true&orderBy=startTime&timeMin&timeMax&fields=…` | HTTP GET (with ETag later) |
| Recurrences | Expanded by the server | Expanded by the server | Ical.Net (RRULE, EXDATE, RECURRENCE-ID, Windows TZIDs) |
| Notes | `Prefer: outlook.timezone="UTC"`, `$select` the fields above, page via `@odata.nextLink`. Delta works on the primary calendar only and can't be filtered, so plain polling is simpler. | My response comes from `attendees[self=true].responseStatus`. All-day events use `start.date`. | Outlook published calendars only include bodies and join links at "full details"; Google's secret iCal address can lag by hours. |
| Client | Raw `HttpClient` + source-generated DTOs. The Graph SDK is huge and not confirmed to be AOT-safe. | Raw `HttpClient` + source-generated DTOs. `Google.Apis.*` uses Newtonsoft, so it isn't trim-safe. | Ical.Net 5.x: not AOT-confirmed, so check the trimmed build; the fallback is a small VEVENT parser. |

## Auth and token storage

**Microsoft 365**
- Use MSAL + Broker (WAM), with the `common` authority and the scope `Calendars.Read`.
- **Consent:** the default tenant policy (fully rolled out by about July 2026) blocks users from consenting to `Calendars.Read`, so admin consent is needed. Personal Microsoft accounts are unaffected. On `AADSTS65001`/`90094`, show the admin-consent URL and offer ICS instead.
- **App registration:**
  - Public client, with public client flows allowed.
  - Redirect URIs: `ms-appx-web://microsoft.aad.brokerplugin/{client_id}` and `http://localhost`.
  - Multi-tenant + personal accounts.
  - Register it in your own tenant if allowed, or in a free personal Entra tenant.
- **WAM needs a parent window,** which a windowless COM server doesn't have. Create a small window of our own during interactive sign-in; fall back to `GetForegroundWindow()` captured while Command Palette is in front.
- Try `AcquireTokenSilent(..., OperatingSystemAccount)` first to sign in silently with the Windows work account. Interactive sign-in only ever happens from a user action.
- Publishing: users can't consent to unverified multi-tenant apps (registered after Nov 2020) that request more than basic sign-in. Publisher verification needs a Microsoft AI Cloud Partner Program ID and a custom domain. Don't borrow a first-party client ID.
- MSAL core is trimmable. The Broker has no AOT statement, so spike it in a trimmed build.

**Google**
- Our own PKCE loopback flow: `127.0.0.1` on a random port, then the system browser. The out-of-band flow is gone, and the desktop "secret" isn't secret.
- Scope: `calendar.readonly`. It's a sensitive scope, not restricted, so verification needs no CASA assessment.
- **Testing mode expires refresh tokens after 7 days.** Set the project to "In production", unverified: you get a warning screen and a 100-user cap, and should avoid the weekly expiry (confirm when building this).
- A Workspace admin may need to trust the client ID under API Controls.
- Each account allows at most 100 refresh tokens per client; beyond that the oldest is revoked silently.

**Storage**
- Use DPAPI (`ProtectedData`, CurrentUser) files in the package's LocalState for the Google refresh tokens, the MSAL cache blob and the ICS feed URLs (they're secret links).
- Not Credential Manager: it has a 2560-byte limit, and the MSAL blob can exceed it.

## Refresh

- Load on page open when the data is older than 5 minutes, on Ctrl+R, and when settings change. Later, poll in the background every 5 minutes and keep the last result on disk so the page and Dock open instantly.
- Each feed or account fails on its own: keep its last good result and show the error, without hiding the others.
- "In 7 min", "now" and the Dock countdown are recalculated locally from the cached events, not refetched. Timers only run while something is showing them.
- On HTTP 429 or 5xx, honour `Retry-After` and back off per account.

## UI

- **Top-level command** "Today's calendar": opens the Today page. Later, its subtitle shows the next meeting, and a separate "Join next meeting" command can be bound to a hotkey.
- **Today page:**
  - Sections: Happening now, Up next, All day and Earlier today.
  - Rows show the time range and relative time.
  - Tags: calendar, Tentative.
  - Details pane: time, location, organizer, attendees and the start of the description.
  - Enter joins.
  - Ctrl+K: join in browser, open in calendar, copy link, copy meeting ID and passcode, copy dial-in, refresh.
- **Dock band:**
  - Shows "Standup · 7m", then "· now", then "· 12m left"; when two meetings overlap, show the most recent one plus "+1".
  - Clicking joins, or opens the page if the meeting has no link.

## Risks

| Risk | Mitigation |
|---|---|
| The work tenant blocks `Calendars.Read` consent | Test first; request admin consent; ICS fallback (the tenant may also block publishing calendars). |
| WAM from a windowless COM server | Own parent window; browser fallback via `http://localhost`. |
| MSAL Broker or Ical.Net not trim-safe | Spike a trimmed Release build; fallbacks are browser auth and a hand-written parser. |
| Dock bands go dead after an RDP or session switch ([#50367](https://github.com/microsoft/PowerToys/issues/50367)) | Keep bands stateless and quick to rebuild from the cache; track the issue. |
| `zoommtg:` and `msteams://…/meet/` are undocumented | Always launch with an https `FallbackUri`; fixture test per format. |
| The SDK is still 0.x | Pin the version; avoid `main`-only APIs. |

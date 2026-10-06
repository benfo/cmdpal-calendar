# Privacy

Calendar for Command Palette is a PowerToys Command Palette extension that shows your meetings and joins them. It runs entirely on your PC. There is no server, account or analytics of ours: the developer never receives your data.

## What it reads

- **Microsoft 365 and Outlook.com calendars** that you choose, through Microsoft Graph, with the delegated permission `Calendars.Read` (read-only). It reads events (title, time, location, notes, organizer, attendees, meeting link) and the names and colours of your calendars. It can't change, create or delete anything, and it doesn't read mail, contacts or files.
- **Published calendars (ICS)** at the addresses or files you add.

## Who it talks to

- **Microsoft**, to sign in (through Windows' own sign-in, Web Account Manager) and to read your calendars from Microsoft Graph.
- **The calendar addresses you add**, to download them.
- **Meeting links**, only when you choose to join or open them, in your browser or meeting app.

Nothing else. Images in event notes aren't loaded, so opening an event doesn't tell anyone you read it.

## What it stores, and where

Everything stays in the extension's own folder on your PC (`%LOCALAPPDATA%\Packages\<package>\LocalState`), encrypted with Windows (DPAPI) for your Windows account:

- `calendars.json`: the calendars you added, their names and colours; calendar addresses are encrypted.
- `msal.cache.bin`: which Microsoft accounts you signed in with (Windows keeps the sign-in itself).
- `cache/`: the last copy of each calendar, so events show straight away and stay visible offline.

Removing a calendar deletes its copy; signing out removes the account. Uninstalling the extension removes the folder.

## For IT admins

The app registration is **Calendar for Command Palette** (client ID `2c35b16d-5b5c-427e-bb0b-d51aeafaa3e8`), a public client with no secret, asking only for `Calendars.Read` plus the standard sign-in scopes (`openid`, `profile`, `offline_access`). The publisher is an individual, so Microsoft shows it as unverified. The source code is public: <https://github.com/benfo/cmdpal-calendar>.

## Contact

Questions or concerns: open an issue at <https://github.com/benfo/cmdpal-calendar/issues>.

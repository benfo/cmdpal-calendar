# Privacy

Calendar for Command Palette is a PowerToys Command Palette extension that shows your meetings and joins them. It runs entirely on your PC. There is no server, account or analytics of ours: the developer never receives your data.

## What it reads

The published calendars (ICS) at the addresses or files you add: event titles, times, locations, notes, organizers, attendees and meeting links.

## Who it talks to

- **The calendar addresses you add**, to download them.
- **Meeting links**, only when you choose to join or open them, in your browser or meeting app.

Nothing else. Images in event notes aren't loaded, so opening an event doesn't tell anyone you read it.

## What it stores, and where

Everything stays in the extension's own folder on your PC (`%LOCALAPPDATA%\Packages\<package>\LocalState`), encrypted with Windows (DPAPI) for your Windows account:

- `calendars.json`: the calendars you added, their names and colours; calendar addresses are encrypted.
- `cache/`: the last copy of each calendar, so events show straight away and stay visible offline.

Removing a calendar deletes its copy. Uninstalling the extension removes the folder.

## Contact

Questions or concerns: open an issue at <https://github.com/benfo/cmdpal-calendar/issues>.

# Spicetify Auto-Apply

A tiny, **transparent** Windows tool that re-applies
[Spicetify](https://spicetify.app) automatically after Spotify updates — so your
customizations don't disappear every time Spotify updates itself.

It registers a normal **Windows Scheduled Task** that runs at logon (and
optionally daily) and re-applies Spicetify. Everything is visible in Task
Scheduler and removable at any time.

> **No** self-copying into the Startup folder · **no** hidden background
> process · **no** blindly piped input. Just a visible scheduled task. The full
> source is in this repo, so you can read exactly what runs on your machine.

## Features

- **Graphical installer** (dark theme): set up, update, apply now, or remove —
  all in one window
- **Triggers:** at every logon (with a configurable delay) and/or daily at a
  chosen time
- **Modes:** Standard (`spicetify apply`) or Thorough (`restore backup apply`)
- Installs Spicetify for you via the official installer if it's missing
- Status panel, live log, "open log", "open Task Scheduler"
- Single **~23 KB** `.exe` · no Python · no admin rights

## Download & use

1. Download `SpicetifyAutoApply.exe` from the
   [Releases](../../releases) page.
2. Run it, choose your options, and click **Einrichten / Set up**.
3. Done — Spicetify is re-applied automatically from now on.

> The `.exe` is unsigned, so Windows SmartScreen may warn on first run:
> **More info → Run anyway**. You can also build it yourself (see below).

## Command line

| Argument        | Does                                               |
|-----------------|----------------------------------------------------|
| `--apply`       | `spicetify apply` (fallback `backup apply`) — used by the task |
| `--apply-fresh` | `spicetify restore backup apply`                   |
| `--uninstall`   | Removes the scheduled task                          |

## Build from source

Needs only the C# compiler that ships with the **.NET Framework** on Windows —
nothing to install. Just run:

```bat
build.bat
```

…or manually:

```bat
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /codepage:65001 /out:SpicetifyAutoApply.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll SpicetifyAutoApply.cs
```

## How it works

Registers a Scheduled Task named **"Spicetify Auto-Apply"** that runs
`SpicetifyAutoApply.exe --apply` at logon. On a normal login it runs
`spicetify apply`; if that fails (typically right after a Spotify update, when
Spotify's files are fresh) it falls back to `spicetify backup apply`. Actions
are logged to `%LOCALAPPDATA%\spicetify-autoapply\autoapply.log`.

## License

[MIT](LICENSE)

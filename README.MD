# mRemoteUG

A tabbed remote connections manager for **RDP and SSH** on Windows.

mRemoteUG is a fork of [mRemoteNG](https://github.com/mRemoteNG/mRemoteNG), cut down to do one
thing well: current .NET, Per-Monitor V2 HiDPI, dark mode, and a codebase small enough for one
person to maintain. If you want VNC, ICA, HTTP, credential vaults or translations, use upstream.

| Protocol | Status |
|---|---|
| RDP | Supported. The reason the fork exists. |
| SSH | Supported, via PuTTY. |
| Telnet, rlogin, RAW | Work, because they share the PuTTY host with SSH. Not a focus. |

**Connection files from mRemoteNG cannot be opened or imported.** The password encryption is
different and older files are refused, so connections have to be entered again.

## Requirements

- Windows 11 (build 22000) or newer, x64
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## Install or build

Download the MSI from the [releases](../../releases), or build it:

```
dotnet build mRemoteUG.slnx -c Release               # the application
dotnet build mRemoteUG.slnx -c "Release Installer"   # and the MSI
```

## Command line

```
mRemoteUG.exe [switches]
```

| Switch | Alias | Effect |
|---|---|---|
| `/cons <file>` | `/c` | Open this connection file instead of the default one. |
| `/reset` | | Do all three of `/resetpos`, `/resetpanels` and `/resettoolbar`. |
| `/resetpos` | `/rp` | Put the window in the middle of the primary screen's working area, 900 x 600, not maximised and not full screen. |
| `/resetpanels` | `/rpnl` | Reset the panels. |
| `/resettoolbar` | `/rtbr` | Reset the toolbar positions. |
| `/noreconnect` | `/norc` | Do not reopen the connections that were open when mRemoteUG was last closed. |
| `--verbose` | | Log debug messages for this run, without changing what is saved (see "Reporting a problem"). |

The resets and `/noreconnect` apply to this run only: when mRemoteUG next saves its settings,
on exit, they are switched off again. `--selftest` and the switches that go with it check a
build rather than serve everyday use (see
[ADR-0013](docs/adr/0013-selftest-as-the-verification-mechanism.md)).

### Accepted forms

- **Prefix.** `-`, `--` and `/` mean the same: `/c`, `-c` and `--c` are one switch, and so are
  `/cons`, `-cons` and `--cons`. The exception is `--verbose`, which is recognised only in
  that exact spelling.
- **Case.** Switch names are not case-sensitive: `/NoReconnect` works. Values keep their case.
- **Value.** A switch's value follows after a space, a `:` or an `=`:

  ```
  mRemoteUG.exe /c C:\Connections\work.xml
  mRemoteUG.exe /c:C:\Connections\work.xml
  mRemoteUG.exe --cons=C:\Connections\work.xml
  ```

  Only the first `:` or `=` of a switch separates its name from its value, so the colon in
  `C:\...` is safe. A word that does not start with `-`, `--` or `/` is always a value, taken
  whole by the switch before it, even if it contains `:` or `=`. A path that starts with `-`
  or `/` would read as a switch of its own: write it with a directory in front, such as
  `.\-work.xml`.
- **Spaces.** Quote a value that contains spaces. Quoting the value alone or the whole
  `switch:value` both work, because Windows removes the double quotes before mRemoteUG
  sees the argument:

  ```
  mRemoteUG.exe /c "C:\My Connections\work.xml"
  mRemoteUG.exe "/c:C:\My Connections\work.xml"
  ```
- **Repeats.** When a switch is given twice, the first one counts. When both `/c` and `/cons`
  are given, `/c` wins.
- **Several switches** combine: `mRemoteUG.exe /rpnl /norc`.

### Finding the connection file

`/cons` and `/c` look for the file in this order and use the first match:

1. The path as given: an absolute path, or one relative to the current directory.
2. Relative to the application directory, the one that holds `mRemoteUG.exe`.
3. Relative to the default connections directory, `%APPDATA%\mRemoteUG`.

If the file is in none of them, the switch is ignored without an error and mRemoteUG opens the
default connection file. So is a `/c` or `/cons` given without a value.

## Reporting a problem

Run `mRemoteUG.exe --verbose`, reproduce the problem, and attach
`%LOCALAPPDATA%\mRemoteUG\mRemoteUG.log`.

This repository is published from a private one at each release. Issues are welcome; pull
requests are not merged here.

## Why it is built this way

The decisions are in [docs/adr/](docs/adr/), the measured platform behaviour behind them in
[docs/platform-findings.md](docs/platform-findings.md).

## Licence

GPL v2 - see [COPYING.TXT](COPYING.TXT). Nearly all of this code was written by the mRemoteNG and
mRemote authors and contributors; see [CREDITS.TXT](CREDITS.TXT).

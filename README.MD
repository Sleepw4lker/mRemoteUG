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

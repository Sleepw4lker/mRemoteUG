mRemoteUG

A tabbed remote connections manager for RDP and SSH on Windows.

mRemoteUG is a fork of mRemoteNG, cut down to do one thing well: current .NET,
Per-Monitor V2 HiDPI, dark mode, and a codebase small enough for one person to
maintain. If you want VNC, ICA, HTTP, credential vaults or translations, use
upstream mRemoteNG: https://github.com/mRemoteNG/mRemoteNG

Supported: RDP, and SSH via PuTTY. Telnet, rlogin and RAW work because they share
the PuTTY host with SSH, but are not a focus.

IMPORTANT: connection files from mRemoteNG cannot be opened or imported. The
password encryption is different and older files are refused, so connections have
to be entered again.

Requirements
------------

 * Windows 11 (build 22000) or newer, x64.
 * .NET 10 Desktop Runtime: https://dotnet.microsoft.com/download/dotnet/10.0

Reporting a problem
-------------------

Run  mRemoteUG.exe --verbose  , reproduce the problem, and attach
%LOCALAPPDATA%\mRemoteUG\mRemoteUG.log

Licence
-------

GPL v2 - see License.txt, installed beside this file. Nearly all of this code was
written by the mRemoteNG and mRemote authors and contributors; see Credits.txt.

# Contributing

This repository is published from a private one at each release, as one commit per version.
Nothing is developed here, and pull requests are not merged: the next release would overwrite
them.

**Issues are welcome.** For a bug, run `mRemoteUG.exe --verbose`, reproduce it, and attach
`%LOCALAPPDATA%\mRemoteUG\mRemoteUG.log`. Say which version (Help > About) and which Windows
build.

The scope is deliberately narrow: RDP and SSH, done well. A request that widens it will probably
be declined - that is the point of the fork, see
[ADR-0001](../docs/adr/0001-fork-to-do-one-thing.md). For VNC, ICA, HTTP or credential vaults,
use [mRemoteNG](https://github.com/mRemoteNG/mRemoteNG).

---
date: 2026-10-01
status: proposed
---

# Write the log under %LOCALAPPDATA%, keep settings under %APPDATA%

`SettingsFileInfo.SettingsPath` — `%APPDATA%\mRemoteUG\` — has held the settings, the
connection file and the log in one directory since before the fork
([ADR-0024](0024-rename-to-mremoteug.md)). `%APPDATA%` is the roaming profile: on a domain
machine with roaming profiles enabled, everything under it follows the user from machine to
machine and counts against whatever roaming profile size quota the domain sets.

That is the right behaviour for settings and the connection file — the point of a roaming
profile is that preferences and saved connections follow the user. It is the wrong behaviour
for the log. Nobody wants last Tuesday's log from a different machine replicated onto this
one, the log is pure machine-local diagnostic noise that nothing reads back except a human
attaching it to a bug report, and on a machine where the roaming profile quota is tight, a
log that rolls up to 60 MB (`RollingLogFileSink`, six files of 10 MB) is a worse use of that
quota than settings ever were.

`Logger.GetLogDirectory` now reads `SettingsFileInfo.LogsPath` —
`%LOCALAPPDATA%\mRemoteUG\` — instead of `SettingsPath`. Settings and the connection file are
untouched; `ConnectionsFileInfo.DefaultConnectionsPath` still derives from `SettingsPath`.

## Alternative considered: move everything to %LOCALAPPDATA%

Rejected. It would stop the settings and the connection file from roaming, which is the
behaviour ADR-0024 deliberately kept when the directory moved for the rename. Nothing about
this change calls that into question — only the log is roaming-inappropriate.

## No migration

The same reasoning as the settings-directory move in ADR-0024: this has not been published
under either layout, so the only affected profile is the author's, and there is no old log
content worth carrying forward — a log is read once, for the problem it was captured for,
and then it is backup noise. A user with a custom `LogFilePath` already set under Options ->
Notifications -> Logging is unaffected either way; `Logger.EffectiveLogPath` only falls back
to the new default when that setting is empty.

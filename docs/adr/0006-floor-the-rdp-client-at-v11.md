---
date: 2026-09-22
---

# Drive the RDP control through IMsRdpClient10, floored at v11

`RdpClientCandidates` tries `AxMsRdpClient12NotSafeForScripting` first and falls back to
`AxMsRdpClient11NotSafeForScripting` (`e3e44e9`). Whichever coclass is created, the code then
talks to it through the single type `MsRdpClient11NotSafeForScripting`.

That looks like a mismatch and is not. There is no `IMsRdpClient11` or `IMsRdpClient12` in the
type library: **`IMsRdpClient10` is the newest interface that exists**, it is
`MsRdpClient11NotSafeForScripting`'s IID, and it is also the default interface of
`MsRdpClient12NotSafeForScripting`. So casting to the one type is exactly
`QueryInterface(IID_IMsRdpClient10)` and drives either coclass. Every feature the application
uses binds against `IMsRdpClient10`, which the v11 floor guarantees.

The floor is v11 rather than "whatever is present" because the alternative — probing down
through v9, v8 and v7 as upstream does — means every call site has to cope with a client that
may not support it, for the benefit of Windows versions this fork does not target anyway
(the supported baseline is Windows 11).

`AxMsRdpClient12` being unregistered is a **supported outcome, not a fault**: on Windows builds
without it the creation raises `CLASS_E_CLASSNOTAVAILABLE` and the v11 fallback takes over.
`--selftest` reports which one it got rather than failing.

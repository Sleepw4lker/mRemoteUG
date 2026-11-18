# Triage Labels

The skills speak in terms of five canonical triage roles. This file maps those roles to the labels on
this repo's Forgejo issue tracker. The first five are organisation labels, shared by every repository of
the TameMyCerts organisation.

| Label in mattpocock/skills | Label in our tracker | Meaning                                  |
| -------------------------- | -------------------- | ---------------------------------------- |
| `needs-triage`             | `needs-triage`       | Maintainer needs to evaluate this issue  |
| `needs-info`               | `needs-info`         | Waiting on reporter for more information |
| `ready-for-agent`          | `ready-for-agent`    | Fully specified, ready for an AFK agent  |
| `ready-for-human`          | `ready-for-human`    | Requires human implementation            |
| `wontfix`                  | `wontfix`            | Will not be actioned                     |

The organisation has two more triage labels, outside the canonical roles:

| Label in our tracker | Meaning                                                                                                          |
| -------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `needs-a-lab`        | Can only manually be verified in a running lab                                                                   |
| `report-to-upstream` | A bug in an upstream project that should be reported to its maintainers; a draft of the report is in the comments |

## `needs-a-lab`

In this repository the lab is a real Windows desktop, with RDP and SSH hosts it can reach: the
development machine is network-isolated and cannot open a connection anywhere, and neither can the CI
runner, so nothing behavioural is proven there. Apply `needs-a-lab` instead of `ready-for-agent` or
`ready-for-human` when the change is only proven by a live session, a protocol exchange, a disconnect
reason, or the application's behaviour on a particular Windows build, monitor setup or DPI scaling.
An agent does not pick up a `needs-a-lab` issue; when its own work leaves only such a check, it moves
the issue to this label and says in a comment what the run has to show, and which values the
diagnostic log should be read for.

## `report-to-upstream`

mRemoteUG is a fork of mRemoteNG, and several of the defects it meets are not its own. Apply
`report-to-upstream` when the defect is in an upstream project this repository builds on or was cut
from, such as mRemoteNG (for code the fork still inherits), PuTTY, .NET and WinForms, WiX, or the
Windows RDP ActiveX control (`mstscax`), and not in code this repository owns. The draft of the report
is written as a comment on the issue, with placeholder names and with facts checked against the
upstream project's tagged source or documentation. Only the maintainer posts it upstream.

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), apply the corresponding label
from the first table. A closed issue keeps whatever triage label it had; closing it is what says it is
done.

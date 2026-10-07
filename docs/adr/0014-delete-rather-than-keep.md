---
date: 2026-09-22
---

# Delete rather than keep

Roughly a third of the commits in this fork remove code. That is a standing policy, not a
cleanup phase, and it follows directly from [ADR-0001](0001-fork-to-do-one-thing.md): a feature
nobody here uses still has to build, still carries dependencies, and still has to be reasoned
about on every change. The explicit "no"s are as much a part of the design as the yes-s, so they
are recorded here rather than left to be inferred from absence.

Removed, with the reason each is not coming back:

- **The credential subsystem and `PageSequence`** (`0a38944`) — credential vaults, the
  credential repository and its provider chain. Connections store their own credentials, as they
  always could. The subsystem existed to share credentials across connections and brought a
  large abstraction surface for it; `8b7d746` then deleted three interfaces the removal
  orphaned.
- **The help feature and the WebBrowser control hosting it** (`e6d5969`) — an embedded Internet
  Explorer control rendering help pages. IE is gone from Windows; the Help menu is now About
  (`4ea9587`).
- **The in-app update checker and theme switching** (`bc0f97d`) — the updater phoned home and
  self-modified; theming is now the system's (see
  [ADR-0009](0009-system-colour-theme-at-startup.md)).
- **All localisations except en-US** (`f852332`) — twenty-odd translations that no one here can
  review, against a UI that is still changing shape. `Language.resx` is now a single file.
- **The Portable edition** — see [ADR-0003](0003-target-net-10.md).
- **Dead types and members** (`578abbd`, `c9e5091`, `7ef1e04`, `8c5064a`, `5f84844`) — including
  resources, settings and help pages orphaned by the above. `7ef1e04` is the one to know about:
  it turned **unused private members into build-time warnings** and then cleared what that
  found, so this class of residue is now caught by the build rather than by inspection.
- **Screenshot capture and management** (`a7d372d`) — a tab-menu Screenshot item captured the
  session area into an in-memory gallery, and a View ▸ Screenshots tool window listed it with
  copy, save-one and save-all. Nothing was persisted: everything in the gallery was lost on
  exit unless saved by hand first. Windows has Win+Shift+S and Print Screen, and they work on a
  remote session like anything else. Its window was also constructed eagerly at startup — a
  `MenuStrip`, a `ContextMenuStrip`, a `SaveFileDialog` and a `FolderBrowserDialog` — for a
  feature most runs never touched.
- **The PuTTY Session Settings verb** (`6abf3f8`) — a verb link in the property grid for a
  PuTTY Profile node that launched `putty.exe` and then drove its configuration dialog by Win32
  automation: select the session in the list box, click "&Load", relabel "&Cancel", hide
  "&Open". Editing a PuTTY profile is PuTTY's job, and the Advanced options page still launches
  it. It took `PropertyGridCommandSite` (a full `IMenuCommandService`/`ISite` implementation
  reached only through `PropertyGrid`'s designer-services reflection), most of
  `ProcessController`, and `EnumWindows` with it. Reached only by reflection, it was covered by
  no test and unreachable from `--selftest`, so nobody here could confirm it still worked
  against current PuTTY — which is part of the argument for removing it rather than carrying it.
- **The Xming PuTTY sessions provider** (`3026133`) — read saved sessions from Xming's on-disk
  session files rather than the registry. A stock PuTTY stores its sessions in
  `HKCU\Software\SimonTatham` and is served by the provider that remains. It was also the
  expensive half of session enumeration: its `GetSession` reached an uncached `File.Exists`
  sweep across `HomePath` and every `PATH` entry, once per session.
- **The language selector** (`689420e`) — `SupportedCultures` built its list by splitting a
  setting whose default is the single string `en-US` and which is application-scoped, so it
  could not be changed at runtime. It offered the language it was already running in.
- **The Lenovo Auto Scroll Utility warning** (`9851e24`) — every start ran
  `Process.GetProcessesByName("virtscrl")` and, on a hit, warned that the utility interferes
  with mRemoteNG. `virtscrl.exe` is the ThinkPad UltraNav driver's TrackPoint scrolling helper
  from the XP/Vista era, which injected scroll messages globally — hence the conflict with
  an embedded session. It has not shipped in a Lenovo driver package for well over a decade, so
  the check spent a process sweep on the startup path to raise a warning nobody can act on.

- **Every connection file format older than ConfVersion 2.9**, and the encryption that read
  them. `AeadCryptographyProvider` moved from a hand-written `bcrypt.dll` P/Invoke to
  `System.Security.Cryptography.AesGcm`, which accepts a 96-bit nonce and nothing else, while
  every file written before it used 128 bits. There is no conversion path that does not require
  the old implementation, so keeping one would have meant keeping the P/Invoke - which was the
  thing being removed. Files below 2.9 are refused by version with a dialog saying so, rather
  than opening with every password silently blank. Going with them:
  `LegacyRijndaelCryptographyProvider` and its MD5/Rijndael use (and the SYSLIB0021 and
  SYSLIB0022 suppressions that existed for it), `LegacyFullFileDecrypt` for whole-file-encrypted
  pre-2.6 files, and the pre-2.6 branch of `CreateDecryptor`. **This is the one removal on this
  list that destroys data rather than features:** saved passwords in an existing `confCons.xml`
  are not recoverable by this build, and the connections themselves are not readable either.
  Accepted deliberately, and the reason it is spelled out here rather than inferred.

The risk of a policy like this is deleting something a user depended on. It is accepted
knowingly: this is a personal fork with a stated scope, and the upstream project remains
available for anyone who needs the breadth. The mitigation is that everything above is one
`git revert` away in a history that starts from upstream's last stable release.

Not deleted, and worth saying so: the Panels concept ([ADR-0007](0007-static-layout-instead-of-dockpanelsuite.md)),
connection inheritance, and the PuTTY-driven protocols
([ADR-0002](0002-restore-the-putty-protocols.md)).

---
date: 2026-09-24
---

# Take the name mRemoteUG, and move the projects under `src/`

The fork was called mRemoteNG in every place a program can name itself: the assembly, the
product, the window title, the settings directory, the MSI's `UpgradeCode`, and 331 `namespace
mRemoteNG` declarations. That was fine while it was a private cut of upstream. It stops being
fine the moment it is published, because two different programs would then claim one identity —
one of them a fork with a deliberately narrower scope that upstream never agreed to.

It is now **mRemoteUG**, a fork of mRemoteNG maintained by Uwe Gradenegger. The alternative was
to keep upstream's name and distinguish the two by version, which is what most private forks do;
it was rejected because the MSI made it actively dangerous rather than merely confusing (below),
and because a bug report against "mRemoteNG 2.1.0" would land on upstream's tracker describing
behaviour upstream does not have.

The directory layout moved in the same pass: `mRemoteV1` → `src/mRemoteUG`, `mRemoteNGTests` →
`src/mRemoteUG.Tests`, and `InstallerProjects/` → `src/mRemoteUG.Installer` and
`src/mRemoteUG.Installer.CustomActions`. The old names described nothing — `mRemoteV1` was a
version number from a product that is now at 2.1.

**The solution file stays at the repository root**, and that is load-bearing rather than
cosmetic. The `.wixproj` and four `.wxs` files resolve the harvest path, the pandoc invocation,
the post-build script and the main executable through `$(SolutionDir)` / `$(var.SolutionDir)`,
and building the `.wixproj` on its own already fails with WIX0150 because that variable is only
defined by a solution build. Moving the solution would have relocated all of them silently.

## What deliberately still says mRemoteNG

This is the part that will look like an unfinished rename to anyone reading the code later. Each
of these refers to **upstream**, not to this product, and renaming it would break something
quietly:

- **`http://mremoteng.org`**, in `XmlRootNodeSerializer` and the schema. It is the connection
  file format's XML namespace, not a website. Changing it makes this build unable to read its own
  files, and every file anyone already has. It is an identifier that happens to look like a URL.
- **The comment describing upstream's behaviour** in `DeferredControlDisposal`. "mRemoteNG
  never disposes the control" is a statement about upstream, and stays true only if it keeps
  saying mRemoteNG.
- **The ADRs and `CHANGELOG.TXT`.** They are dated records. "This fork was started from
  mRemoteNG" describes what happened.

## The installer had to become a different product

The package carried upstream's `UpgradeCode`, `dd678a54-ca75-4791-8dfe-d818095684f8`. Windows
Installer treats that code as product identity, so installing this fork would have been a major
upgrade of mRemoteNG: it would have uninstalled a real mRemoteNG installation and replaced it
with something that cannot open its connection files and has most of its protocols removed. A
fresh `UpgradeCode` was not optional.

The same reasoning removed the NSIS legacy-version purge. It found
`HKLM\...\Uninstall\mRemoteNG` and ran that product's uninstall string, which was correct while
this package *was* mRemoteNG and is not defensible now.

## The settings directory was allowed to move

`SettingsFileInfo.SettingsPath` and `Logger.BuildLogFilePath` both derive from
`Application.ProductName`, so renaming `AssemblyProduct` moved the settings, the connection file
and the log from `%APPDATA%\mRemoteNG\` to `%APPDATA%\mRemoteUG\` with no code change and no
migration.

Migration code was considered and rejected. Nothing has been published under either name, so the
only affected profile is the author's; a copy-on-first-run path would have been permanent code
carrying a one-off, on a machine where it is hard to test. Copying the directory by hand is the
whole of the upgrade. This is [ADR-0014](0014-delete-rather-than-keep.md) applied to the author's
own convenience.

`LegacySettingsImporter` went with it, in the same pass and for the same reason it would have
been kept. It existed because `LocalFileSettingsProvider` derives its `user.config` directory
from application evidence, and that derivation changed between .NET Framework and modern .NET,
so an upgraded installation looked as though it had reset itself. The rename moved that
directory again - `AssemblyProduct` and `AssemblyCompany` both changed - so on the next run the
importer would have fired once more and carried 153 settings across.

It was deleted anyway, with that migration deliberately not taken, because the rename had also
turned it around to face the wrong product. Its glob is `mRemoteNG*` and it picks the newest
`user.config` by write time; 15 of the 19 matches on the author's machine belong to **upstream**
mRemoteNG 1.76.20, not to this fork. Once mRemoteUG is a separate product, reading another
product's configuration is the settings-level form of the NSIS purge removed above, and one
launch of real mRemoteNG would have been enough to make upstream's settings the newest match.
Narrowing the glob was possible and rejected: it would have kept a class, a test fixture, a
`--selftest` check and a call site alive to serve a migration that can happen exactly once.

## One thing the rename could not simply rename

`mRemoteUG.Tests` is a child namespace of `mRemoteUG`, and the product has a `Resources` class in
namespace `mRemoteUG`. A simple name in C# is resolved by walking *enclosing namespaces* before
the compilation unit's `using` directives, so inside the tests the bare name `Resources` binds to
the product's class and not to the test project's own — and a file-level `using Resources = ...`
alias does not fix it, because the alias is consulted last. The two affected files alias it as
`Fixtures`, which is what it holds.

The alternative was to keep the test namespace out from under the product's, as `mRemoteUGTests`
was. Rejected: `mRemoteUG.Tests` is the conventional name, and the collision is two lines and a
comment rather than a design problem.

## Attribution

The rename is the point at which credit stops being implicit. `AssemblyCopyright` now names Uwe
Gradenegger *and* keeps the mRemoteNG Dev Team, Riley McArdle and Felix Deimel; the About dialog
says the fork's name, its maintainer and its lineage, with the third-party notices behind its
expander; `CREDITS.TXT` gained a header saying the same and lost only the entries for components
this fork no longer ships. `AboutTextTests` asserts the original authors are still named, so a
later edit to the copyright string cannot quietly drop them.

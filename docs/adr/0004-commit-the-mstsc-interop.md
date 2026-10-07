---
date: 2026-09-22
---

# Commit the MSTSCLib interop assemblies instead of generating them

`ThirdParty/Interop/AxInterop.MSTSCLib.dll` and `Interop.MSTSCLib.dll` are checked into the
repository as binaries and referenced directly. Upstream mRemoteNG declares them as
`<COMReference>` items and lets MSBuild regenerate them on every build. Committing generated
binaries is the kind of thing a reviewer flags on sight, so: it is deliberate.

MSBuild's `ResolveComReference` task **does not exist in the .NET Core build of MSBuild**. With
`<COMReference>` in the project, `dotnet build` fails outright with MSB4803 — and because
`dotnet test` transitively builds `mRemoteV1`, the entire test suite becomes unrunnable without
Visual Studio's MSBuild. Upstream lives with that; this fork will not, because building and
testing from the command line is what makes it maintainable by one person on any machine.

The considered alternatives were: keep `<COMReference>` and require Visual Studio (rejected —
gives up command-line build and test); hand-write the COM interfaces (rejected — a large,
error-prone surface for no gain); commit the generated assemblies (chosen).

The cost is that the interop is now a build artefact under version control, which must be
regenerated when the type library changes. `Tools/regenerate-mstsc-interop.ps1` does that.
**Never regenerate by re-adding `<COMReference>`**, which is the obvious-looking fix and breaks
the build for everyone without Visual Studio.

# Generated COM interop assemblies (MSTSCLib)

These two assemblies wrap the Microsoft Terminal Services Control (`mstscax.dll`)
— the RDP ActiveX control that `Connection/Protocol/RDP/RdpProtocol.cs` drives.

| File | Produced by | Contains |
|---|---|---|
| `Interop.MSTSCLib.dll` | `tlbimp` | The raw COM interfaces and coclasses (`IMsRdpClient*`, `MsRdpClient*NotSafeForScripting`) |
| `AxInterop.MSTSCLib.dll` | `aximp` | The `AxHost`-derived WinForms wrappers (`AxMsRdpClient11`/`12NotSafeForScripting` are the ones this app creates) |

## Why they are committed instead of generated at build time

They used to be generated on every build from `<COMReference>` items in
`mRemoteUG.csproj`. MSBuild's `ResolveComReference` task exists only in the
.NET Framework build of MSBuild, so `dotnet build` fails with:

```
error MSB4803: The task "ResolveComReference" is not supported on the
.NET Core version of MSBuild.
```

Since `dotnet test` transitively builds `mRemoteUG`, keeping `<COMReference>`
would mean the test suite could not run without Visual Studio. Committing the
generated output keeps `dotnet build` and `dotnet test` working everywhere.

## Regenerating

Run `Tools\regenerate-mstsc-interop.ps1` (requires Visual Studio for the
.NET Framework MSBuild), then run the guard tests:

```
dotnet test src/mRemoteUG.Tests --filter AxInteropSurface
```

The type library (`{8C11EFA1-92C3-11D1-BC1E-00C04FA31489}` v1.0) has been stable
for years, so regeneration should essentially never be necessary.

## Note on assembly references

Both assemblies reference `System.Windows.Forms, Version=4.0.0.0,
PublicKeyToken=b77a5c561934e089` and `mscorlib` — the .NET *Framework*
identities. This is expected and harmless: the .NET runtime resolves framework
assemblies by simple name, so they bind to the .NET 10 Windows Desktop
assemblies at run time. Verified working on .NET 10.0.12.

<#
.SYNOPSIS
    Regenerates ThirdParty\Interop\{Interop,AxInterop}.MSTSCLib.dll from the
    Microsoft Terminal Services Control type library registered on this machine.

.DESCRIPTION
    mRemoteUG talks to the RDP ActiveX control (mstscax.dll) through two generated
    COM interop assemblies. They used to be produced on every build by MSBuild's
    ResolveComReference task, driven by <COMReference> items in mRemoteUG.csproj.

    That task only exists in the .NET Framework build of MSBuild: running
    'dotnet build' against a project containing <COMReference> fails with

        error MSB4803: The task "ResolveComReference" is not supported on the
        .NET Core version of MSBuild.

    Because 'dotnet test' transitively builds mRemoteUG, keeping <COMReference>
    would make the whole test suite unrunnable without Visual Studio. So the two
    assemblies are generated once by this script and committed under
    ThirdParty\Interop\, and referenced by plain <Reference><HintPath>.

    You should essentially never need to run this. The mstscax type library
    ({8C11EFA1-92C3-11D1-BC1E-00C04FA31489}, version 1.0) has been stable for
    many years. Re-run it only if you deliberately want to pick up a newer
    control version, and re-run the AxInteropSurfaceTests afterwards.

.NOTES
    Requires Visual Studio (any edition) for the .NET Framework MSBuild.exe.
    Generates against a net10.0-windows project so the wrappers are produced by
    the same toolchain the app is built with.
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\ThirdParty\Interop')
)

$ErrorActionPreference = 'Stop'

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe not found. Visual Studio (or Build Tools) is required to regenerate the interop assemblies."
    }
    $msbuild = & $vswhere -all -products * -requires Microsoft.Component.MSBuild `
                          -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (-not $msbuild) { throw "MSBuild.exe not found via vswhere." }
    return $msbuild
}

$msbuild = Find-MSBuild
Write-Host "Using MSBuild: $msbuild"

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("mstsc-interop-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    # A throwaway project whose only purpose is to make ResolveComReference run.
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <ResolveComReferenceSilent>True</ResolveComReferenceSilent>
  </PropertyGroup>
  <ItemGroup>
    <COMReference Include="AxMSTSCLib">
      <Guid>{8C11EFA1-92C3-11D1-BC1E-00C04FA31489}</Guid>
      <VersionMajor>1</VersionMajor>
      <VersionMinor>0</VersionMinor>
      <Lcid>0</Lcid>
      <WrapperTool>aximp</WrapperTool>
      <Isolated>False</Isolated>
    </COMReference>
    <COMReference Include="MSTSCLib">
      <Guid>{8C11EFA1-92C3-11D1-BC1E-00C04FA31489}</Guid>
      <VersionMajor>1</VersionMajor>
      <VersionMinor>0</VersionMinor>
      <Lcid>0</Lcid>
      <WrapperTool>tlbimp</WrapperTool>
      <Isolated>False</Isolated>
    </COMReference>
  </ItemGroup>
</Project>
'@ | Set-Content -Path (Join-Path $work 'InteropGen.csproj') -Encoding UTF8

    Push-Location $work
    try {
        & $msbuild 'InteropGen.csproj' -t:Restore,Build -p:Configuration=Release -v:minimal -nologo
        if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }
    }
    finally { Pop-Location }

    $generated = Join-Path $work 'obj\Release\net10.0-windows'
    $wanted = @('Interop.MSTSCLib.dll', 'AxInterop.MSTSCLib.dll')

    if (-not (Test-Path $OutputDirectory)) { New-Item -ItemType Directory -Path $OutputDirectory | Out-Null }

    foreach ($name in $wanted) {
        $src = Join-Path $generated $name
        if (-not (Test-Path $src)) { throw "Expected generated assembly not found: $src" }
        Copy-Item $src -Destination (Join-Path $OutputDirectory $name) -Force
        $hash = (Get-FileHash (Join-Path $OutputDirectory $name) -Algorithm SHA256).Hash
        Write-Host ("  {0,-26} {1}" -f $name, $hash)
    }

    Write-Host ""
    Write-Host "Done. Now run the interop guard tests:" -ForegroundColor Green
    Write-Host "  dotnet test src/mRemoteUG.Tests --filter AxInteropSurface"
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

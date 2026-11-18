#Requires -Version 7.6

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
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '',
    Justification = 'interactive developer tool, the console output is for the person running it')]
[CmdletBinding()]
param (
    # Where the two interop assemblies are written. Defaults to ThirdParty\Interop in this working tree.
    [string] $OutputDirectory = (Join-Path -Path $PSScriptRoot -ChildPath '..\ThirdParty\Interop')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-MSBuild {
    <#
    .SYNOPSIS
        Finds the .NET Framework MSBuild.exe of an installed Visual Studio or Build Tools through vswhere.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param ()

    $vswhere = Join-Path -Path ${env:ProgramFiles(x86)} -ChildPath 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw ('vswhere.exe not found. Visual Studio (or Build Tools) is required to regenerate the ' +
            'interop assemblies.')
    }
    $query = @(
        '-all', '-products', '*', '-requires', 'Microsoft.Component.MSBuild', '-find', 'MSBuild\**\Bin\MSBuild.exe'
    )
    $msbuild = & $vswhere @query | Select-Object -First 1
    if (-not $msbuild) { throw 'MSBuild.exe not found via vswhere.' }
    return $msbuild
}

$msbuild = Find-MSBuild
Write-Host "Using MSBuild: $msbuild"

$workName = 'mstsc-interop-' + [Guid]::NewGuid().ToString('N')
$work = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath $workName
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
'@ | Set-Content -LiteralPath (Join-Path -Path $work -ChildPath 'InteropGen.csproj') -Encoding UTF8

    Push-Location -LiteralPath $work
    try {
        & $msbuild 'InteropGen.csproj' '-t:Restore,Build' -p:Configuration=Release -v:minimal -nologo
        if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }
    } finally { Pop-Location }

    $generated = Join-Path -Path $work -ChildPath 'obj\Release\net10.0-windows'
    $wanted = @('Interop.MSTSCLib.dll', 'AxInterop.MSTSCLib.dll')

    if (-not (Test-Path -LiteralPath $OutputDirectory)) {
        $null = New-Item -ItemType Directory -Path $OutputDirectory
    }

    foreach ($name in $wanted) {
        $src = Join-Path -Path $generated -ChildPath $name
        if (-not (Test-Path -LiteralPath $src)) { throw "Expected generated assembly not found: $src" }
        $destination = Join-Path -Path $OutputDirectory -ChildPath $name
        Copy-Item -LiteralPath $src -Destination $destination -Force
        $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        Write-Host ("  {0,-26} {1}" -f $name, $hash)
    }

    Write-Host ''
    Write-Host 'Done. Now run the interop guard tests:' -ForegroundColor Green
    Write-Host '  dotnet test --project src/mRemoteUG.Tests --filter AxInteropSurface'
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

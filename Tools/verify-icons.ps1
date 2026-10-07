<#
.SYNOPSIS
    Fails if the committed artwork is not what the manifest currently describes.

.DESCRIPTION
    The icons under src/mRemoteUG/Resources/Glyphs, src/mRemoteUG/Resources/Icons and
    src/mRemoteUG/Icons are generated from src/mRemoteUG/Resources/icon-manifest.json and
    committed. That is the whole point - the application takes no rasterizer dependency and the
    art is reviewable as files rather than produced at build time (ADR-0015 is not violated
    because Tools/IconGen is outside the solution and never ships).

    The cost of committing generated output is that it can drift from its source. This is the
    check that says so. It is deliberately NOT an NUnit test: the test project would have to take
    a reference on the rasterizer and the Fluent packages to run it, and that is exactly the
    dependency the arrangement exists to avoid.

.EXAMPLE
    pwsh Tools/verify-icons.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

Write-Host 'Re-rendering the icon set and comparing it with the tree...'
& dotnet run --project (Join-Path $repo 'Tools/IconGen') -c Release -- --check

switch ($LASTEXITCODE) {
    0 { Write-Host 'Icons are in step with the manifest.' -ForegroundColor Green }
    2 { Write-Error 'The committed icons differ from a fresh render. Run: dotnet run --project Tools/IconGen' }
    default { Write-Error "IconGen failed with exit code $LASTEXITCODE." }
}

exit $LASTEXITCODE

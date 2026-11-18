#Requires -Version 7.6

<#
.SYNOPSIS
    Lists the files an MSI installs, and fails if any of them is one the package must not carry.

.DESCRIPTION
    The installer harvests the whole of src\mRemoteUG\bin\Release, so whatever the build leaves
    there is packaged unless something keeps it out. Two things must never be: a .pdb, because the
    application's symbols are embedded in mRemoteUG.dll (ADR-0041) and a loose one would only put
    the build's source paths on users' disks, and mRemoteUG.xml, the documentation file the
    harvest filter drops.

    The package's File table is read through the WindowsInstaller.Installer COM object, opened
    read-only. Nothing is installed and msiexec is not run; the File table is the cheap way to ask
    what went into a package without extracting its cab (docs/platform-findings.md, Installer).
    Its FileName column holds "SHORT~1.EXT|Long name.ext" when the two differ, and the long name is
    what is matched.

    Every row is written to the output, sorted by name, so the run's log holds the package's file
    list. A forbidden file is a terminating error naming each one.

.EXAMPLE
    pwsh Tools/verify-msi-payload.ps1 -Path src\mRemoteUG.Installer\bin\Release\en-US\mRemoteUG-Installer-2.1.1.0.msi
#>

[CmdletBinding()]
[OutputType([pscustomobject])]
param (
    # The package to inspect.
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $Path,

    # Wildcard patterns, matched against each file's long name, that fail the check.
    [ValidateNotNullOrEmpty()]
    [string[]] $Forbidden = @('*.pdb', 'mRemoteUG.xml')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$msiOpenDatabaseModeReadOnly = 0
$fullPath = (Resolve-Path -LiteralPath $Path).ProviderPath

$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase($fullPath, $msiOpenDatabaseModeReadOnly)
$view = $database.OpenView('SELECT `FileName`, `FileSize` FROM `File`')
try {
    $null = $view.Execute()
    $files = while ($record = $view.Fetch()) {
        $name = $record.StringData(1)
        [pscustomobject] @{
            Name = ($name -split '\|')[-1]
            Size = $record.IntegerData(2)
        }
    }
} finally {
    $null = $view.Close()
    # The database stays open, and the file locked, until the last reference to it is released.
    $null = [System.Runtime.InteropServices.Marshal]::ReleaseComObject($view)
    $null = [System.Runtime.InteropServices.Marshal]::ReleaseComObject($database)
    $null = [System.Runtime.InteropServices.Marshal]::ReleaseComObject($installer)
}

$files = @($files | Sort-Object -Property Name)
if ($files.Count -eq 0) {
    throw "$fullPath has no rows in its File table, so it installs nothing. That is not a package to ship."
}
$files

$offending = @($files | Where-Object {
        $file = $_
        @($Forbidden | Where-Object { $file.Name -like $_ }).Count -gt 0
    })
if ($offending.Count -gt 0) {
    throw ("$fullPath installs $($offending.Count) file(s) it must not carry: " +
        "$($offending.Name -join ', '). Forbidden: $($Forbidden -join ', '). See ADR-0041.")
}

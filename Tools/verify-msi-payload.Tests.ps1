#Requires -Version 7.6

BeforeAll {
    $script:verifier = Join-Path -Path $PSScriptRoot -ChildPath 'verify-msi-payload.ps1'

    # A database with nothing in it but a File table, written through the same COM object the
    # script reads with. Enough for the query; no installer would accept it as a package.
    function Build-TestPackage {
        [CmdletBinding()]
        [OutputType([string])]
        param (
            [Parameter(Mandatory)]
            [string] $Name,

            # FileName column values, "SHORT~1.EXT|Long name.ext" or a plain name.
            [string[]] $FileName = @()
        )

        $path = Join-Path -Path $TestDrive -ChildPath $Name
        $msiOpenDatabaseModeCreate = 3
        $installer = New-Object -ComObject WindowsInstaller.Installer
        $database = $installer.OpenDatabase($path, $msiOpenDatabaseModeCreate)
        $statements = @(
            'CREATE TABLE `File` (`File` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, ' +
            '`FileName` CHAR(255) NOT NULL LOCALIZABLE, `FileSize` LONG NOT NULL, `Version` CHAR(72), ' +
            '`Language` CHAR(20), `Attributes` SHORT, `Sequence` SHORT NOT NULL PRIMARY KEY `File`)'
        )
        for ($i = 0; $i -lt $FileName.Count; $i++) {
            $statements += 'INSERT INTO `File` (`File`, `Component_`, `FileName`, `FileSize`, `Sequence`) ' +
            "VALUES ('f$i', 'c$i', '$($FileName[$i])', $(100 + $i), $($i + 1))"
        }
        foreach ($statement in $statements) {
            $view = $database.OpenView($statement)
            $null = $view.Execute()
            $null = $view.Close()
            $null = [System.Runtime.InteropServices.Marshal]::ReleaseComObject($view)
        }
        $null = $database.Commit()
        $null = [System.Runtime.InteropServices.Marshal]::ReleaseComObject($database)
        $null = [System.Runtime.InteropServices.Marshal]::ReleaseComObject($installer)
        $path
    }
}

Describe 'verify-msi-payload' {
    Context 'when the package carries only what it should' {
        It 'lists every file by its long name, sorted, with its size' {
            $package = Build-TestPackage -Name 'clean.msi' -FileName @(
                'MREMOT~1.EXE|mRemoteUG.exe', 'MREMOT~1.DLL|mRemoteUG.dll', 'Serilog.dll')

            $files = & $verifier -Path $package

            $files.Name | Should -Be @('mRemoteUG.dll', 'mRemoteUG.exe', 'Serilog.dll')
            $files[0].Size | Should -Be 101
        }

        It 'leaves the package unlocked, so a later step can sign or upload it' {
            $package = Build-TestPackage -Name 'unlocked.msi' -FileName @('Serilog.dll')

            $null = & $verifier -Path $package

            { Remove-Item -LiteralPath $package } | Should -Not -Throw
        }
    }

    Context 'when the package carries a forbidden file' {
        It 'fails on a symbol file, naming it' {
            $package = Build-TestPackage -Name 'pdb.msi' -FileName @(
                'MREMOT~1.EXE|mRemoteUG.exe', 'MREMOT~1.PDB|mRemoteUG.pdb')

            { & $verifier -Path $package } | Should -Throw -ExpectedMessage '*mRemoteUG.pdb*'
        }

        It 'fails on the documentation file' {
            $package = Build-TestPackage -Name 'xml.msi' -FileName @('MREMOT~1.XML|mRemoteUG.xml')

            { & $verifier -Path $package } | Should -Throw -ExpectedMessage '*mRemoteUG.xml*'
        }

        It 'matches the long name, not the short one' {
            $package = Build-TestPackage -Name 'short.msi' -FileName @('SYMBOLS.PDB|Symbols.txt')

            { & $verifier -Path $package } | Should -Not -Throw
        }
    }

    Context 'when the package installs nothing' {
        It 'fails' {
            $package = Build-TestPackage -Name 'empty.msi'

            { & $verifier -Path $package } | Should -Throw -ExpectedMessage '*no rows*'
        }
    }
}

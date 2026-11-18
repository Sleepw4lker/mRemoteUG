#Requires -Version 7.6

# The script runs the self-test as soon as it is invoked, so the function under test is taken out of
# it by parsing rather than by dot-sourcing the whole file.
BeforeAll {
    $scriptPath = Join-Path -Path $PSScriptRoot -ChildPath 'run-selftest.ps1'
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref] $null, [ref] $null)
    $definition = $ast.Find({
            param ($node)
            $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -eq 'Get-LoggedUnhandledException'
        }, $true)
    . ([scriptblock]::Create($definition.Extent.Text))
}

Describe 'Get-LoggedUnhandledException' {
    BeforeAll {
        $script:log = Join-Path -Path $TestDrive -ChildPath 'mRemoteUG.log'
        Set-Content -LiteralPath $script:log -Value @(
            '2026-11-09 09:59:59,999 [1] ERROR - Unhandled exception - before the run'
            '2026-11-09 10:00:00,500 [1] ERROR - Unhandled exception - in the first millisecond'
            'System.Exception: Unhandled exception quoted in a stack trace, no timestamp'
            '2026-11-09 10:00:05,000 [1] ERROR - Unhandled exception - during the run'
            '2026-11-09 10:00:06,000 [1] INFO  - something else during the run'
            '2026-11-09 10:00:10,001 [1] ERROR - Unhandled exception - after the run'
        )
        $script:since = [datetime]::ParseExact('2026-11-09 10:00:00.5004', 'yyyy-MM-dd HH:mm:ss.ffff', $null)
        $script:until = [datetime]::ParseExact('2026-11-09 10:00:10', 'yyyy-MM-dd HH:mm:ss', $null)
    }

    Context 'when the log holds lines from before, during and after the run' {
        It 'returns only the unhandled exceptions stamped during the run' {
            $lines = @(Get-LoggedUnhandledException -Path $script:log -Since $script:since -Until $script:until)

            $lines | Should -Be @(
                '2026-11-09 10:00:00,500 [1] ERROR - Unhandled exception - in the first millisecond'
                '2026-11-09 10:00:05,000 [1] ERROR - Unhandled exception - during the run'
            )
        }
    }

    Context 'when nothing was logged during the run' {
        It 'returns nothing' {
            $later = $script:until.AddHours(1)

            @(Get-LoggedUnhandledException -Path $script:log -Since $later -Until $later.AddMinutes(1)) |
                Should -BeNullOrEmpty
        }
    }
}

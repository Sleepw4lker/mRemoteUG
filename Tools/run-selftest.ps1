<#
.SYNOPSIS
    Runs mRemoteUG --selftest and grades the run by the report it wrote, not by the exit
    code the process died with.

.DESCRIPTION
    ADR-0013 makes --selftest the verification tier that unit tests cannot reach. Its
    process exit code is not that verification: the run is known to crash intermittently
    during shutdown, after every check has run and RESULT: PASS has already been written
    to the report. One such run exited -1073740771, which is 0xC000041D,
    STATUS_UNHANDLED_EXCEPTION - Windows killing the process on the way out, not the
    self-test returning a verdict. Grading that code called a passing run a failure, so
    the step was left continue-on-error and stopped meaning anything at all.

    The verdict lives in the report, on its last line: RESULT: PASS, or RESULT: FAIL (n
    check(s) failed). SelfTest.Run writes the whole report in one File.WriteAllText at
    the very end, so the presence of a RESULT line is itself the evidence that the run
    reached the end. This script therefore treats:

        RESULT: PASS, exit 0          pass
        RESULT: PASS, exit non-zero   pass, with a warning naming the decoded status
        RESULT: FAIL                  failure, with the failing check lines repeated
        no RESULT line, or no report  failure - the run died before it finished

    Because the report path is fixed and this runner is not clean between jobs, a stale
    report from an earlier run would otherwise be graded as this run's. The file is
    deleted before the process starts, so a run that dies early is read as a missing
    report rather than as whatever passed last week.

.PARAMETER Exe
    The executable to run. Defaults to the Release build in this working tree.

.PARAMETER ReportPath
    Where SelfTest.Emit writes the report: the directory of the log file, which is
    %LOCALAPPDATA%\mRemoteUG unless the log has been moved.

.PARAMETER TimeoutSeconds
    How long to wait before killing the run and failing. The self-test constructs every
    form and shows a real task dialog, so it is not instant, but it has no reason to take
    minutes either.

.PARAMETER LargeFont
    Pass --largefont as well, repeating the run at a 1.5x UI font.

.EXAMPLE
    pwsh Tools\run-selftest.ps1

.EXAMPLE
    pwsh Tools\run-selftest.ps1 -Exe src\mRemoteUG\bin\Release\mRemoteUG.exe -LargeFont
#>
[CmdletBinding()]
param (
    [string] $Exe = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'src\mRemoteUG\bin\Release\mRemoteUG.exe'),

    [string] $ReportPath = (Join-Path $env:LOCALAPPDATA 'mRemoteUG\mRemoteUG-selftest.log'),

    [string] $LogPath = (Join-Path $env:LOCALAPPDATA 'mRemoteUG\mRemoteUG.log'),

    [int] $TimeoutSeconds = 300,

    [switch] $LargeFont
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The status codes a run is known to die with on the way out. Each is Windows terminating
# the process, and none of them says anything about the checks. Decoding one in the log is
# what turned "the self-test failed" into "the self-test passed and then the process was
# killed" the first time it happened.
#
# Keyed by the formatted hex string rather than by a numeric literal, because PowerShell
# reads a hex literal as Int32 by bit pattern when it fits in 32 bits: 0xC000041D is the
# key -1073740771 and never matches a lookup by the unsigned value, and 0xFFFFFFFF is
# Int32 -1, which makes a mask written with it a silent no-op. Both measured, both silent.
$KnownStatus = @{
    '0xC000041D' = 'STATUS_UNHANDLED_EXCEPTION'
    '0xC0000409' = 'STATUS_STACK_BUFFER_OVERRUN'
    '0xC0000005' = 'STATUS_ACCESS_VIOLATION'
}

function Format-ExitCode([int] $Code)
{
    if ($Code -ge 0)
    {
        return "$Code"
    }

    # Exit codes are surfaced signed; NTSTATUS values read as negative. Show both, plus
    # the name when it is one of the codes above.
    $unsigned = [uint32] ([int64] $Code -band 0xFFFFFFFFL)
    $hex = '0x{0:X8}' -f $unsigned
    $name = if ($KnownStatus.ContainsKey($hex)) { ", $($KnownStatus[$hex])" } else { '' }
    return "$Code ($hex$name)"
}

if (-not (Test-Path -LiteralPath $Exe))
{
    throw "Not found: $Exe. Build it first: dotnet build mRemoteUG.slnx -c Release"
}

# Delete the previous report before running, so that a crash early in the run cannot be
# graded against an older run's verdict.
if (Test-Path -LiteralPath $ReportPath)
{
    Remove-Item -LiteralPath $ReportPath -Force
}

$switches = @('--selftest')
if ($LargeFont)
{
    $switches += '--largefont'
}

"Running $Exe $($switches -join ' ')"

$process = Start-Process -FilePath $Exe -ArgumentList $switches -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000))
{
    $process.Kill($true)
    throw "The self-test did not finish within $TimeoutSeconds seconds and was killed."
}

$exitCode = $process.ExitCode

# mRemoteUG.exe is a WinExe, so it detaches from the console and its output has to be
# read back from the report file rather than from stdout.
if (-not (Test-Path -LiteralPath $ReportPath))
{
    "Self-test exit code: $(Format-ExitCode $exitCode)"
    throw "The self-test wrote no report to $ReportPath. It writes the whole report at the end of the run, so it died before finishing - the exit code above is the only evidence there is."
}

$report = Get-Content -LiteralPath $ReportPath
$report
"Self-test exit code: $(Format-ExitCode $exitCode)"

$verdict = @($report | Select-String -Pattern '^RESULT: ' -CaseSensitive)
if ($verdict.Count -eq 0)
{
    throw "The report at $ReportPath has no RESULT line. The run was cut short before it finished, whatever the exit code says."
}

# Last, not first: the report is rewritten whole on each run, so there is only ever one,
# but grading the last line is the right reading if that ever changes.
$result = $verdict[-1].Line.Trim()

if ($result -notmatch '^RESULT: PASS$')
{
    foreach ($line in $report | Select-String -Pattern '^FAIL ' -CaseSensitive)
    {
        "  $($line.Line.Trim())"
    }

    throw "$result - see the FAIL lines above."
}

# A UI-thread exception raised *after* SelfTest.Run has written the report cannot change the
# verdict in it - the checks are already scored. The log is the only place it appears, so look
# there too rather than calling the run clean on the report alone.
if (Test-Path -LiteralPath $LogPath)
{
    $onUiThread = @(Select-String -LiteralPath $LogPath -Pattern 'Unhandled exception' -SimpleMatch)
    if ($onUiThread.Count -gt 0)
    {
        "::warning::The report says PASS, but $($LogPath) records $($onUiThread.Count) unhandled exception(s). One raised after the report was written cannot fail the run, so it is reported here."
        foreach ($line in $onUiThread)
        {
            "  $($line.Line.Trim())"
        }
    }
}

if ($exitCode -ne 0)
{
    # Not a failure. Every check ran and passed; the process was killed afterwards. Kept
    # visible rather than swallowed, because the day it stops happening is worth noticing,
    # and so is the day it starts happening before the checks finish.
    "::warning::The self-test passed every check and then exited $(Format-ExitCode $exitCode). This is the known intermittent crash during shutdown after RESULT: PASS - see ADR-0013. The run is graded by the report, not by this code."
}

"$result - graded from $ReportPath."

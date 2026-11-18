# PSScriptAnalyzer settings for this repo. See docs/conventions/powershell.md.
#
# All default rules run, plus the formatting rules from the built-in CodeFormattingOTBS preset
# (copied below, since a settings file can't include a preset) and a few stricter extras.
# The VS Code PowerShell extension picks this file up automatically from the repo root; CI runs
# it with `Invoke-ScriptAnalyzer -Recurse -Settings ./PSScriptAnalyzerSettings.psd1`.
@{
    Severity     = @('Error', 'Warning', 'Information')

    # Each rule switched off here sits in one of the two blocks below; CI fails on an entry outside
    # them (docs/conventions/powershell.md, "Departing from these conventions"). A rule turned off
    # with `Enable = $false` under Rules carries the same kind of comment directly above it.
    ExcludeRules = @(
        # --- Exceptions ---------------------------------------------------------------------------
        # Permanent, for a technical reason. Each entry names its row in the departures table of
        # docs/conventions/powershell.md, or the section of that file that sets the rule.

        # "Target runtime and files": PowerShell 7 reads BOM-less UTF-8 correctly. Projects opting
        # into Windows PowerShell 5.1 remove this entry.
        'PSUseBOMForUnicodeEncodedFile'

        # --- Temporary exclusions -----------------------------------------------------------------
        # Off only until the rule's findings are fixed. Each entry names its open issue on the same
        # line, for example: 'PSAvoidUsingWriteHost' # #42
        # This list only gets shorter: the PR that fixes a rule's findings removes its entry.
    )

    Rules        = @{
        # --- Formatting: CodeFormattingOTBS preset ---------------------------------------------
        PSPlaceOpenBrace                   = @{
            Enable             = $true
            OnSameLine         = $true
            NewLineAfter       = $true
            IgnoreOneLineBlock = $true
        }

        PSPlaceCloseBrace                  = @{
            Enable             = $true
            NewLineAfter       = $false
            IgnoreOneLineBlock = $true
            NoEmptyLineBefore  = $false
        }

        PSUseConsistentIndentation         = @{
            Enable              = $true
            Kind                = 'space'
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
            IndentationSize     = 4
        }

        PSUseConsistentWhitespace          = @{
            Enable                                  = $true
            CheckInnerBrace                         = $true
            CheckOpenBrace                          = $true
            CheckOpenParen                          = $true
            CheckOperator                           = $true
            CheckPipe                               = $true
            CheckPipeForRedundantWhitespace         = $false
            CheckSeparator                          = $true
            CheckParameter                          = $false
            IgnoreAssignmentOperatorInsideHashTable = $true
        }

        PSAlignAssignmentStatement         = @{
            Enable         = $true
            CheckHashtable = $true
        }

        PSUseCorrectCasing                 = @{
            Enable = $true
        }

        # --- Additions beyond the preset --------------------------------------------------------
        # 120 rather than PoshCode's 115: matches PSScriptAnalyzer's default and the C# limit.
        PSAvoidLongLines                   = @{
            Enable            = $true
            MaximumLineLength = 120
        }

        PSAvoidSemicolonsAsLineTerminators = @{
            Enable = $true
        }

        PSAvoidUsingPositionalParameters   = @{
            Enable           = $true
            CommandAllowList = @()
        }

        # Help goes at the top, inside the function body.
        PSProvideCommentHelp               = @{
            Enable                  = $true
            ExportedOnly            = $true
            BlockComment            = $true
            VSCodeSnippetCorrection = $false
            Placement               = 'begin'
        }

        # Syntax must parse on the target version. Projects opting into Windows PowerShell 5.1
        # support add '5.1' here.
        PSUseCompatibleSyntax              = @{
            Enable         = $true
            TargetVersions = @('7.6')
        }
    }
}

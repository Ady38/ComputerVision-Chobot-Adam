# Run the SDK style check, allowing the documented constructor-colon layout.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$WhitespaceOnly
)

$ErrorActionPreference = 'Stop'
# Exit code 2 reports proposed formatting changes, which are inspected below.
$PSNativeCommandUseErrorActionPreference = $false
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$reports = Join-Path $root 'results/code-style'
New-Item -ItemType Directory -Force -Path $reports | Out-Null
$report = Join-Path $reports 'format.json'
$log = Join-Path $reports 'format.log'

# A failed formatter invocation must never reuse a previous successful report.
if (Test-Path -LiteralPath $report) {
    Remove-Item -LiteralPath $report
}

$previousLanguage = $env:DOTNET_CLI_UI_LANGUAGE
try {
    $env:DOTNET_CLI_UI_LANGUAGE = 'en'
    if ($WhitespaceOnly) {
        & dotnet format whitespace $root --folder --verify-no-changes `
            --report $report *> $log
    }
    else {
        $solution = Join-Path $root 'ComputerVision.Student.slnx'
        & dotnet format $solution --no-restore --verify-no-changes `
            --severity info --report $report *> $log
    }

    $formatExit = $LASTEXITCODE
}
finally {
    $env:DOTNET_CLI_UI_LANGUAGE = $previousLanguage
}

if ($formatExit -notin @(0, 2) -or !(Test-Path -LiteralPath $report)) {
    Get-Content -LiteralPath $log
    throw 'The formatter did not complete successfully.'
}

$unexpected = @()
$allowed = 0
$entries = @(Get-Content -Raw -LiteralPath $report | ConvertFrom-Json)
foreach ($entry in $entries) {
    $lines = [IO.File]::ReadAllLines($entry.FilePath)
    foreach ($change in $entry.FileChanges) {
        $line = $lines[$change.LineNumber - 1]
        $column = $change.CharNumber - 1

        # Accept only insertion of one space immediately before : base/: this.
        $isConstructorColon = $column -gt 0 -and $column -lt $line.Length `
            -and $line[$column - 1] -eq ')' `
            -and $line.Substring($column) -match '^: (base|this)\('
        if ($change.DiagnosticId -eq 'WHITESPACE' -and $isConstructorColon `
            -and $change.FormatDescription -ceq `
                "Fix whitespace formatting. Insert '\s'.") {
            $allowed++
        }
        else {
            $unexpected += $change
        }
    }
}

if ($unexpected.Count -gt 0 -or ($formatExit -ne 0 -and $allowed -eq 0)) {
    Get-Content -LiteralPath $log
    throw 'Code style differs from the repository conventions.'
}

Write-Output "Code style passed ($allowed constructor-colon exceptions)."

# CI must receive success after the formatter's exceptions have been accepted.
exit 0

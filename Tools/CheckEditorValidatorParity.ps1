param(
    [string]$ConsolePath = "C:\dev\Tri\console.md",
    [string]$ValidatorLogPath = "",
    [string]$RunId = "",
    [string]$RunRepo = "MoniVibe/HeadlessRebuildTool",
    [string]$DiagRoot = "C:\dev\Tri\Tools\HeadlessRebuildTool",
    [switch]$DownloadRunArtifacts,
    [int]$SampleCount = 15,
    [string]$ReportPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Require-Gh {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) not found in PATH."
    }
}

function Find-ValidatorLog {
    param(
        [string]$PreferredRoot,
        [string]$FallbackRoot
    )

    $roots = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($PreferredRoot) -and (Test-Path $PreferredRoot)) {
        $roots.Add($PreferredRoot)
    }
    if (-not [string]::IsNullOrWhiteSpace($FallbackRoot) -and (Test-Path $FallbackRoot)) {
        $roots.Add($FallbackRoot)
    }

    $candidates = New-Object System.Collections.Generic.List[System.IO.FileInfo]
    foreach ($root in $roots) {
        $files = Get-ChildItem -Path $root -Recurse -Filter "unity_build_tail.txt" -File -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            $candidates.Add($file)
        }
    }

    if ($candidates.Count -eq 0) {
        return $null
    }

    $staging = @($candidates | Where-Object { $_.FullName -match "\\staging\\" } | Sort-Object LastWriteTimeUtc -Descending)
    if ($staging.Count -gt 0) {
        return $staging[0].FullName
    }

    $sorted = @($candidates | Sort-Object LastWriteTimeUtc -Descending)
    return $sorted[0].FullName
}

function Get-ErrorData {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Path not found: $Path"
    }

    $allLines = @(Get-Content -Path $Path)
    $errorLines = New-Object System.Collections.Generic.List[string]
    $uniqueSigs = New-Object System.Collections.Generic.HashSet[string]

    foreach ($line in $allLines) {
        if ($line -match ": error ([A-Z]+\d+):\s*(.+)$") {
            $errorLines.Add($line)
            $code = $matches[1].Trim()
            $message = ($matches[2] -replace "\s+", " ").Trim()
            [void]$uniqueSigs.Add(("{0}|{1}" -f $code, $message))
        }
    }

    return [pscustomobject]@{
        ErrorLines = @($errorLines)
        UniqueSignatures = @($uniqueSigs | Sort-Object)
    }
}

function Write-Report {
    param(
        [string]$Path,
        [string[]]$Lines
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    $dir = Split-Path -Path $Path -Parent
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    Set-Content -Path $Path -Value $Lines
}

if (-not (Test-Path $ConsolePath)) {
    throw "Console path not found: $ConsolePath"
}

$runDiagRoot = ""
if (-not [string]::IsNullOrWhiteSpace($RunId)) {
    $runDiagRoot = Join-Path $DiagRoot ("diag_{0}" -f $RunId)
    if ($DownloadRunArtifacts -or [string]::IsNullOrWhiteSpace($ValidatorLogPath)) {
        $hasExistingLogs = $false
        if (Test-Path $runDiagRoot) {
            $hasExistingLogs = ($null -ne (Find-ValidatorLog -PreferredRoot $runDiagRoot -FallbackRoot ""))
        }

        if ($DownloadRunArtifacts -or (-not $hasExistingLogs)) {
            Require-Gh
            if ($DownloadRunArtifacts -and (Test-Path $runDiagRoot)) {
                Remove-Item -Path $runDiagRoot -Recurse -Force
            }
            New-Item -ItemType Directory -Path $runDiagRoot -Force | Out-Null
            & gh run download $RunId -R $RunRepo -D $runDiagRoot | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to download artifacts for run $RunId."
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ValidatorLogPath)) {
    $ValidatorLogPath = Find-ValidatorLog -PreferredRoot $runDiagRoot -FallbackRoot $DiagRoot
}

if ([string]::IsNullOrWhiteSpace($ValidatorLogPath)) {
    throw "Could not resolve validator log path. Provide -ValidatorLogPath or -RunId."
}

$console = Get-ErrorData -Path $ConsolePath
$validator = Get-ErrorData -Path $ValidatorLogPath

$consoleSet = [System.Collections.Generic.HashSet[string]]::new()
foreach ($sig in $console.UniqueSignatures) { [void]$consoleSet.Add($sig) }

$validatorSet = [System.Collections.Generic.HashSet[string]]::new()
foreach ($sig in $validator.UniqueSignatures) { [void]$validatorSet.Add($sig) }

$overlap = New-Object System.Collections.Generic.List[string]
$missingInValidator = New-Object System.Collections.Generic.List[string]
$extraInValidator = New-Object System.Collections.Generic.List[string]

foreach ($sig in $console.UniqueSignatures) {
    if ($validatorSet.Contains($sig)) {
        $overlap.Add($sig)
    } else {
        $missingInValidator.Add($sig)
    }
}

foreach ($sig in $validator.UniqueSignatures) {
    if (-not $consoleSet.Contains($sig)) {
        $extraInValidator.Add($sig)
    }
}

$summaryLines = @(
    "console_path=$ConsolePath",
    "validator_log_path=$ValidatorLogPath",
    "console_error_lines=$($console.ErrorLines.Count)",
    "validator_error_lines=$($validator.ErrorLines.Count)",
    "console_unique_signatures=$($console.UniqueSignatures.Count)",
    "validator_unique_signatures=$($validator.UniqueSignatures.Count)",
    "overlap_signatures=$($overlap.Count)",
    "missing_in_validator=$($missingInValidator.Count)",
    "extra_in_validator=$($extraInValidator.Count)"
)

foreach ($line in $summaryLines) {
    Write-Host $line
}

if ($missingInValidator.Count -gt 0) {
    Write-Host "---missing_in_validator_sample---"
    $missingInValidator | Select-Object -First $SampleCount | ForEach-Object { Write-Host $_ }
}

if ($extraInValidator.Count -gt 0) {
    Write-Host "---extra_in_validator_sample---"
    $extraInValidator | Select-Object -First $SampleCount | ForEach-Object { Write-Host $_ }
}

Write-Report -Path $ReportPath -Lines (
    $summaryLines +
    @("---missing_in_validator_sample---") +
    @($missingInValidator | Select-Object -First $SampleCount) +
    @("---extra_in_validator_sample---") +
    @($extraInValidator | Select-Object -First $SampleCount)
)

if ($missingInValidator.Count -eq 0 -and $extraInValidator.Count -eq 0) {
    exit 0
}

exit 2

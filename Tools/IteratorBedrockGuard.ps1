param(
    [string]$RepoPath = "C:\dev\Tri\space4x_ultimate",
    [string]$PuredotsRepoPath = "C:\dev\Tri\puredots_ultimate",
    [string]$Remote = "origin",
    [string]$BaseBranch = "main",
    [string]$UnityExe = "",
    [string]$CompileLogPath = "",
    [string]$ReportPath = "",
    [string]$Space4xRepoSlug = "MoniVibe/Space4x",
    [string]$PuredotsRepoSlug = "MoniVibe/PureDOTS",
    [string]$AwarenessNote = "",
    [switch]$SkipCompilePreflight,
    [switch]$SkipPrAwareness,
    [switch]$AllowDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Git {
    param(
        [string]$Path,
        [string[]]$GitArgs
    )

    $output = & git -C $Path @GitArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        $argText = $GitArgs -join " "
        $errorText = ($output | Out-String).Trim()
        throw "git -C $Path $argText failed. $errorText"
    }

    return @($output)
}

function Try-Git {
    param(
        [string]$Path,
        [string[]]$GitArgs
    )

    $output = & git -C $Path @GitArgs 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = @($output)
    }
}

function Require-Gh {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) not found in PATH."
    }
}

function Invoke-GhJson {
    param([string[]]$GhArgs)

    $raw = & gh @GhArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        $errorText = ($raw | Out-String).Trim()
        throw "gh failed: $errorText"
    }

    $joined = ($raw -join "`n").Trim()
    if ([string]::IsNullOrWhiteSpace($joined)) {
        return @()
    }

    return ($joined | ConvertFrom-Json)
}

function Get-RepoState {
    param(
        [string]$Path,
        [string]$RemoteName,
        [string]$BaseName
    )

    if (-not (Test-Path $Path)) {
        throw "Repo path not found: $Path"
    }

    Invoke-Git -Path $Path -GitArgs @("fetch", "--all", "--prune") | Out-Null

    $branch = (Invoke-Git -Path $Path -GitArgs @("rev-parse", "--abbrev-ref", "HEAD") | Select-Object -Last 1).Trim()
    $head = (Invoke-Git -Path $Path -GitArgs @("rev-parse", "--short", "HEAD") | Select-Object -Last 1).Trim()
    $baseRef = "$RemoteName/$BaseName"
    $baseSha = (Invoke-Git -Path $Path -GitArgs @("rev-parse", "--short", $baseRef) | Select-Object -Last 1).Trim()

    $distance = (Invoke-Git -Path $Path -GitArgs @("rev-list", "--left-right", "--count", "$baseRef...HEAD") | Select-Object -Last 1).Trim()
    $parts = @($distance -split "\s+" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($parts.Count -lt 2) {
        throw "Could not parse ahead/behind from '$distance' for repo $Path"
    }

    $behind = [int]$parts[0]
    $ahead = [int]$parts[1]

    $statusLines = @(Invoke-Git -Path $Path -GitArgs @("status", "--porcelain"))
    $dirtyLines = @($statusLines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $dirtyCount = $dirtyLines.Count

    $mergeBase = (Invoke-Git -Path $Path -GitArgs @("merge-base", $baseRef, "HEAD") | Select-Object -Last 1).Trim()
    $upstreamTry = Try-Git -Path $Path -GitArgs @("rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}")
    $hasUpstream = $upstreamTry.ExitCode -eq 0
    $upstream = ""
    if ($hasUpstream) {
        $upstream = ($upstreamTry.Output | Select-Object -Last 1).Trim()
    }

    return [pscustomobject]@{
        Path = $Path
        Branch = $branch
        Head = $head
        BaseRef = $baseRef
        BaseSha = $baseSha
        Behind = $behind
        Ahead = $ahead
        DirtyCount = $dirtyCount
        DirtySample = @($dirtyLines | Select-Object -First 10)
        MergeBase = $mergeBase
        HasUpstream = $hasUpstream
        Upstream = $upstream
    }
}

function New-ParentDir {
    param([string]$Path)
    $dir = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $RepoPath ("Temp\iterator_bedrock_guard_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
}

if ([string]::IsNullOrWhiteSpace($CompileLogPath)) {
    $CompileLogPath = Join-Path $RepoPath "Temp\iterator_compile_preflight.log"
}

$failures = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]
$space4xState = $null
$puredotsState = $null
$compileResult = [ordered]@{
    enabled = (-not $SkipCompilePreflight.IsPresent)
    exit_code = $null
    passed = $false
    log_path = $CompileLogPath
}
$prAwarenessResult = [ordered]@{
    enabled = (-not $SkipPrAwareness.IsPresent)
    note = $AwarenessNote
    space4x_open_needs_validate = @()
    puredots_open_needs_validate = @()
}

try {
    $space4xState = Get-RepoState -Path $RepoPath -RemoteName $Remote -BaseName $BaseBranch
    $puredotsState = Get-RepoState -Path $PuredotsRepoPath -RemoteName $Remote -BaseName $BaseBranch

    Write-Host ("[Bedrock] space4x branch={0} head={1} base={2} base_sha={3} behind={4} ahead={5} dirty={6}" -f $space4xState.Branch, $space4xState.Head, $space4xState.BaseRef, $space4xState.BaseSha, $space4xState.Behind, $space4xState.Ahead, $space4xState.DirtyCount)
    Write-Host ("[Bedrock] puredots branch={0} head={1} base={2} base_sha={3} behind={4} ahead={5} dirty={6}" -f $puredotsState.Branch, $puredotsState.Head, $puredotsState.BaseRef, $puredotsState.BaseSha, $puredotsState.Behind, $puredotsState.Ahead, $puredotsState.DirtyCount)

    if ($space4xState.Branch -eq "HEAD") {
        $failures.Add("space4x is in detached HEAD state.")
    }
    if ($space4xState.Branch -eq $BaseBranch) {
        $failures.Add("space4x is on '$BaseBranch'. Use a feature/fix branch for iterator work.")
    }

    if ($puredotsState.Branch -eq "HEAD") {
        $failures.Add("puredots is in detached HEAD state.")
    }
    if ($puredotsState.Branch -eq $BaseBranch -and $puredotsState.Ahead -gt 0) {
        $failures.Add("puredots has local commits ahead of '$BaseBranch' while on '$BaseBranch'. Use a feature/fix branch.")
    }

    if ($space4xState.Behind -gt 0) {
        $failures.Add("space4x is behind $($space4xState.BaseRef) by $($space4xState.Behind) commit(s).")
    }
    if ($puredotsState.Behind -gt 0) {
        $failures.Add("puredots is behind $($puredotsState.BaseRef) by $($puredotsState.Behind) commit(s).")
    }

    if (-not $AllowDirty.IsPresent) {
        if ($space4xState.DirtyCount -gt 0) {
            $sample = ($space4xState.DirtySample -join "; ")
            $failures.Add("space4x has dirty files ($($space4xState.DirtyCount)). Sample: $sample")
        }
        if ($puredotsState.DirtyCount -gt 0) {
            $sample = ($puredotsState.DirtySample -join "; ")
            $failures.Add("puredots has dirty files ($($puredotsState.DirtyCount)). Sample: $sample")
        }
    }

    if (-not $SkipPrAwareness.IsPresent) {
        Require-Gh
        $space4xPrs = @(Invoke-GhJson -GhArgs @("pr", "list", "-R", $Space4xRepoSlug, "-S", "is:open label:needs-validate", "-L", "100", "--json", "number,title,headRefName,baseRefName,url,updatedAt"))
        $puredotsPrs = @(Invoke-GhJson -GhArgs @("pr", "list", "-R", $PuredotsRepoSlug, "-S", "is:open label:needs-validate", "-L", "100", "--json", "number,title,headRefName,baseRefName,url,updatedAt"))

        $prAwarenessResult.space4x_open_needs_validate = $space4xPrs
        $prAwarenessResult.puredots_open_needs_validate = $puredotsPrs

        Write-Host ("[Awareness] space4x needs-validate open={0}" -f $space4xPrs.Count)
        Write-Host ("[Awareness] puredots needs-validate open={0}" -f $puredotsPrs.Count)

        $totalOpen = $space4xPrs.Count + $puredotsPrs.Count
        if ($totalOpen -gt 0 -and [string]::IsNullOrWhiteSpace($AwarenessNote)) {
            $failures.Add("Open needs-validate PRs detected ($totalOpen total). Re-run with -AwarenessNote after reviewing queue.")
        }
    }

    if (-not $SkipCompilePreflight.IsPresent) {
        $preflightScript = Join-Path $RepoPath "Tools\IteratorCompilePreflight.ps1"
        if (-not (Test-Path $preflightScript)) {
            throw "Missing preflight script: $preflightScript"
        }

        $preflightArgs = @{
            RepoPath = $RepoPath
            LogPath = $CompileLogPath
        }
        if (-not [string]::IsNullOrWhiteSpace($UnityExe)) {
            $preflightArgs.UnityExe = $UnityExe
        }

        & $preflightScript @preflightArgs
        $compileExit = $LASTEXITCODE
        $compileResult.exit_code = $compileExit
        $compileResult.passed = $compileExit -eq 0
        if ($compileExit -ne 0) {
            $failures.Add("Iterator compile preflight failed (exit=$compileExit).")
        }
    }
}
catch {
    $failures.Add($_.Exception.Message)
}

$status = if ($failures.Count -eq 0) { "PASS" } else { "FAIL" }
$reportObj = [ordered]@{
    timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
    status = $status
    awareness_note = $AwarenessNote
    failures = @($failures)
    warnings = @($warnings)
    space4x = if ($null -eq $space4xState) { $null } else { [ordered]@{
        path = $space4xState.Path
        branch = $space4xState.Branch
        head = $space4xState.Head
        base_ref = $space4xState.BaseRef
        base_sha = $space4xState.BaseSha
        behind = $space4xState.Behind
        ahead = $space4xState.Ahead
        dirty_count = $space4xState.DirtyCount
        has_upstream = $space4xState.HasUpstream
        upstream = $space4xState.Upstream
        merge_base = $space4xState.MergeBase
    } }
    puredots = if ($null -eq $puredotsState) { $null } else { [ordered]@{
        path = $puredotsState.Path
        branch = $puredotsState.Branch
        head = $puredotsState.Head
        base_ref = $puredotsState.BaseRef
        base_sha = $puredotsState.BaseSha
        behind = $puredotsState.Behind
        ahead = $puredotsState.Ahead
        dirty_count = $puredotsState.DirtyCount
        has_upstream = $puredotsState.HasUpstream
        upstream = $puredotsState.Upstream
        merge_base = $puredotsState.MergeBase
    } }
    compile_preflight = $compileResult
    pr_awareness = $prAwarenessResult
}

New-ParentDir -Path $ReportPath
$reportJson = $reportObj | ConvertTo-Json -Depth 8
Set-Content -Path $ReportPath -Value $reportJson
Write-Host "[Bedrock] report=$ReportPath"

if ($status -eq "PASS") {
    Write-Host "[Bedrock] PASS"
    exit 0
}

Write-Host "[Bedrock] FAIL"
foreach ($f in $failures) {
    Write-Host ("[Bedrock] failure: {0}" -f $f)
}
exit 2

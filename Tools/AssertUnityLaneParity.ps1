param(
    [string]$EditorRepoPath = "C:\dev\Tri\space4x_ultimate",
    [string]$CliRepoPath = "C:\dev\Tri\space4x",
    [switch]$AllowCommitDrift,
    [switch]$AllowEditorVersionDrift,
    [switch]$AllowManifestDrift,
    [switch]$AllowPackagesLockDrift,
    [switch]$EmitJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-RepoPath {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path $Path)) {
        throw "$Label path not found: $Path"
    }

    $gitPath = Join-Path $Path ".git"
    if (-not (Test-Path $gitPath)) {
        throw "$Label is not a git repo: $Path"
    }
}

function Get-Head {
    param([string]$Path)
    return (git -C $Path rev-parse HEAD).Trim()
}

function Compare-FileHash {
    param(
        [string]$EditorRoot,
        [string]$CliRoot,
        [string]$RelativePath
    )

    $editorPath = Join-Path $EditorRoot $RelativePath
    $cliPath = Join-Path $CliRoot $RelativePath

    if (-not (Test-Path $editorPath)) {
        throw "Editor lane missing file: $editorPath"
    }
    if (-not (Test-Path $cliPath)) {
        throw "CLI lane missing file: $cliPath"
    }

    $editorHash = (Get-FileHash -Algorithm SHA256 -Path $editorPath).Hash
    $cliHash = (Get-FileHash -Algorithm SHA256 -Path $cliPath).Hash

    return [pscustomobject]@{
        relative_path = $RelativePath
        editor_hash = $editorHash
        cli_hash = $cliHash
        match = $editorHash -eq $cliHash
    }
}

Assert-RepoPath -Path $EditorRepoPath -Label "EditorRepoPath"
Assert-RepoPath -Path $CliRepoPath -Label "CliRepoPath"

$editorHead = Get-Head -Path $EditorRepoPath
$cliHead = Get-Head -Path $CliRepoPath
$commitMatch = $editorHead -eq $cliHead

$editorVersion = Compare-FileHash -EditorRoot $EditorRepoPath -CliRoot $CliRepoPath -RelativePath "ProjectSettings\ProjectVersion.txt"
$manifest = Compare-FileHash -EditorRoot $EditorRepoPath -CliRoot $CliRepoPath -RelativePath "Packages\manifest.json"
$packagesLock = Compare-FileHash -EditorRoot $EditorRepoPath -CliRoot $CliRepoPath -RelativePath "Packages\packages-lock.json"

$failures = New-Object System.Collections.Generic.List[string]

if (-not $commitMatch -and -not $AllowCommitDrift.IsPresent) {
    $failures.Add("commit drift: editor_head=$editorHead cli_head=$cliHead") | Out-Null
}

if (-not $editorVersion.match -and -not $AllowEditorVersionDrift.IsPresent) {
    $failures.Add("editor version drift: ProjectSettings/ProjectVersion.txt differs") | Out-Null
}

if (-not $manifest.match -and -not $AllowManifestDrift.IsPresent) {
    $failures.Add("package manifest drift: Packages/manifest.json differs") | Out-Null
}

if (-not $packagesLock.match -and -not $AllowPackagesLockDrift.IsPresent) {
    $failures.Add("package lock drift: Packages/packages-lock.json differs") | Out-Null
}

$summary = [ordered]@{
    editor_repo_path = [System.IO.Path]::GetFullPath($EditorRepoPath)
    cli_repo_path = [System.IO.Path]::GetFullPath($CliRepoPath)
    editor_head = $editorHead
    cli_head = $cliHead
    commit_match = $commitMatch
    project_version_match = $editorVersion.match
    manifest_match = $manifest.match
    packages_lock_match = $packagesLock.match
    failures = @($failures)
}

if ($EmitJson.IsPresent) {
    $summary | ConvertTo-Json -Depth 6
}
else {
    Write-Host "[AssertUnityLaneParity] editor_repo=$($summary.editor_repo_path)"
    Write-Host "[AssertUnityLaneParity] cli_repo=$($summary.cli_repo_path)"
    Write-Host "[AssertUnityLaneParity] commit_match=$($summary.commit_match)"
    Write-Host "[AssertUnityLaneParity] project_version_match=$($summary.project_version_match)"
    Write-Host "[AssertUnityLaneParity] manifest_match=$($summary.manifest_match)"
    Write-Host "[AssertUnityLaneParity] packages_lock_match=$($summary.packages_lock_match)"
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "[AssertUnityLaneParity] FAIL $failure"
    }
    exit 1
}

Write-Host "[AssertUnityLaneParity] PASS"
exit 0

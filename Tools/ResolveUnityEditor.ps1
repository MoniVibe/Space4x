param(
    [string]$RepoPath = "",
    [string]$UnityExe = "",
    [string]$HubRoot = "C:\Program Files\Unity\Hub\Editor",
    [switch]$PreferEnvUnityExe,
    [switch]$EmitJson,
    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
    param([string]$Path)

    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
}

function Test-UnityExe {
    param(
        [string]$Path,
        [string]$Source
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    if (-not (Test-Path $Path)) {
        return $null
    }

    return [pscustomobject]@{
        unity_exe = [System.IO.Path]::GetFullPath($Path)
        source = $Source
    }
}

function Parse-UnityTag {
    param([string]$Tag)

    if ([string]::IsNullOrWhiteSpace($Tag)) {
        return $null
    }

    if ($Tag -match '^(\d+)\.(\d+)\.(\d+)([abfp])(\d+)$') {
        $channel = $Matches[4]
        $channelRank = switch ($channel) {
            "a" { 0 }
            "b" { 1 }
            "f" { 2 }
            "p" { 3 }
            default { 0 }
        }

        return [pscustomobject]@{
            tag = $Tag
            major = [int]$Matches[1]
            minor = [int]$Matches[2]
            patch = [int]$Matches[3]
            channel = $channel
            channel_rank = $channelRank
            iteration = [int]$Matches[5]
        }
    }

    if ($Tag -match '^(\d+)\.(\d+)\.(\d+)$') {
        return [pscustomobject]@{
            tag = $Tag
            major = [int]$Matches[1]
            minor = [int]$Matches[2]
            patch = [int]$Matches[3]
            channel = "f"
            channel_rank = 2
            iteration = 0
        }
    }

    return $null
}

function Get-ProjectEditorVersion {
    param([string]$ResolvedRepoPath)

    $projectVersionPath = Join-Path $ResolvedRepoPath "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path $projectVersionPath)) {
        return ""
    }

    $line = Get-Content $projectVersionPath | Select-String -Pattern '^m_EditorVersion:\s*(\S+)' | Select-Object -First 1
    if ($null -eq $line) {
        return ""
    }

    return $line.Matches[0].Groups[1].Value.Trim()
}

function Resolve-LatestInstalledUnity {
    param([string]$ResolvedHubRoot)

    if (-not (Test-Path $ResolvedHubRoot)) {
        return $null
    }

    $candidates = New-Object System.Collections.Generic.List[object]
    foreach ($dir in Get-ChildItem -Path $ResolvedHubRoot -Directory) {
        $parsed = Parse-UnityTag -Tag $dir.Name
        if ($null -eq $parsed) {
            continue
        }

        $exe = Join-Path $dir.FullName "Editor\Unity.exe"
        if (-not (Test-Path $exe)) {
            continue
        }

        $candidates.Add([pscustomobject]@{
            tag = $dir.Name
            exe = $exe
            major = $parsed.major
            minor = $parsed.minor
            patch = $parsed.patch
            channel_rank = $parsed.channel_rank
            iteration = $parsed.iteration
        }) | Out-Null
    }

    if ($candidates.Count -eq 0) {
        return $null
    }

    return $candidates |
        Sort-Object -Property `
            @{ Expression = "major"; Descending = $true }, `
            @{ Expression = "minor"; Descending = $true }, `
            @{ Expression = "patch"; Descending = $true }, `
            @{ Expression = "channel_rank"; Descending = $true }, `
            @{ Expression = "iteration"; Descending = $true } |
        Select-Object -First 1
}

$resolvedRepoPath = Resolve-RepoPath -Path $RepoPath

$selection = Test-UnityExe -Path $UnityExe -Source "explicit"
if ($null -eq $selection -and $PreferEnvUnityExe.IsPresent -and -not [string]::IsNullOrWhiteSpace($env:UNITY_EXE)) {
    $selection = Test-UnityExe -Path $env:UNITY_EXE -Source "env:UNITY_EXE(preferred)"
}

$projectEditorVersion = Get-ProjectEditorVersion -ResolvedRepoPath $resolvedRepoPath
if ($null -eq $selection -and -not [string]::IsNullOrWhiteSpace($projectEditorVersion)) {
    $projectCandidate = Join-Path $HubRoot "$projectEditorVersion\Editor\Unity.exe"
    $selection = Test-UnityExe -Path $projectCandidate -Source "project:ProjectVersion.txt"
}

if ($null -eq $selection -and -not $PreferEnvUnityExe.IsPresent -and -not [string]::IsNullOrWhiteSpace($env:UNITY_EXE)) {
    $selection = Test-UnityExe -Path $env:UNITY_EXE -Source "env:UNITY_EXE"
}

if ($null -eq $selection) {
    $latest = Resolve-LatestInstalledUnity -ResolvedHubRoot $HubRoot
    if ($null -ne $latest) {
        $selection = Test-UnityExe -Path $latest.exe -Source "hub:latest_installed"
    }
}

if ($null -eq $selection) {
    $knownEditors = @()
    if (Test-Path $HubRoot) {
        $knownEditors = @(Get-ChildItem -Path $HubRoot -Directory | Select-Object -ExpandProperty Name)
    }

    $knownText = if ($knownEditors.Count -eq 0) { "<none>" } else { $knownEditors -join ", " }
    throw "Unable to resolve Unity editor. Repo='$resolvedRepoPath' ProjectVersion='$projectEditorVersion' HubRoot='$HubRoot' InstalledEditors=$knownText"
}

$result = [ordered]@{
    repo_path = $resolvedRepoPath
    unity_exe = $selection.unity_exe
    source = $selection.source
    project_editor_version = $projectEditorVersion
}

if ($EmitJson) {
    $result | ConvertTo-Json -Depth 4
    exit 0
}

if (-not $Quiet.IsPresent) {
    Write-Host ("[ResolveUnityEditor] repo={0}" -f $result.repo_path)
    Write-Host ("[ResolveUnityEditor] project_editor={0}" -f $result.project_editor_version)
    Write-Host ("[ResolveUnityEditor] source={0}" -f $result.source)
}

Write-Output $result.unity_exe
exit 0

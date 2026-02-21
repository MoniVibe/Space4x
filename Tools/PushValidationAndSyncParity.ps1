param(
    [string]$RepoPath = "C:\dev\Tri\space4x",
    [ValidateSet("iterator", "validator")]
    [string]$Mode = "iterator",
    [string]$Remote = "origin",
    [string]$PushBranch = "",
    [switch]$SkipPush,
    [switch]$SkipLocalParity,
    [switch]$SkipLaptopParity,
    [string]$LocalParityBranch = "validator/ultimate-checkout",
    [string]$LocalParityUpstreamRef = "",
    [string]$LaptopHost = "25.29.69.246",
    [string]$LaptopUser = "shonh",
    [string]$LaptopRepoPath = "C:\dev\unity_clean_fleetcrawl",
    [string]$LaptopParityBranch = "validator/ultimate-checkout",
    [string]$LaptopParityUpstreamRef = "",
    [string]$LaptopKeyPath = "",
    [ValidateSet("fail", "stash-allowed")]
    [string]$DirtyPolicy = "fail",
    [string[]]$AllowedDirtyRegex = @(),
    [switch]$AllowMetaDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Escape-SingleQuoted {
    param([string]$Text)
    if ($null -eq $Text) {
        return ""
    }

    return $Text.Replace("'", "''")
}

function Resolve-LaptopKeyPath {
    param([string]$Requested)

    if (-not [string]::IsNullOrWhiteSpace($Requested)) {
        if (-not (Test-Path $Requested)) {
            throw "Laptop key file not found: $Requested"
        }

        return $Requested
    }

    $candidates = @(
        "$env:USERPROFILE\.ssh\buildbox_laptop_ed25519",
        "$env:USERPROFILE\.ssh\desktop_to_laptop_ed25519"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "No laptop SSH key found. Checked: $($candidates -join ', ')"
}

function Parse-StatusPaths {
    param([string[]]$StatusLines)

    $paths = New-Object System.Collections.Generic.List[string]
    foreach ($line in $StatusLines) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $trimmed = $line.TrimEnd()
        if ($trimmed.Length -lt 4 -or $trimmed[2] -ne ' ') {
            continue
        }

        $path = $trimmed.Substring(3).Trim()
        if ($path.Contains(" -> ")) {
            $path = $path.Split(" -> ")[-1].Trim()
        }

        if ($path.StartsWith('"') -and $path.EndsWith('"') -and $path.Length -ge 2) {
            $path = $path.Substring(1, $path.Length - 2)
        }

        if (-not [string]::IsNullOrWhiteSpace($path)) {
            $paths.Add($path)
        }
    }

    return $paths.ToArray()
}

function Get-DisallowedDirtyPaths {
    param(
        [string[]]$Paths,
        [string[]]$Regexes
    )

    $disallowed = New-Object System.Collections.Generic.List[string]
    foreach ($path in $Paths) {
        $matched = $false
        foreach ($rx in $Regexes) {
            if ($path -match $rx) {
                $matched = $true
                break
            }
        }

        if (-not $matched) {
            $disallowed.Add($path)
        }
    }

    return $disallowed.ToArray()
}

function Ensure-AllowedDirtyState {
    param(
        [string]$Label,
        [string[]]$DirtyPaths,
        [string[]]$EffectiveRegexes
    )

    $dirtyArray = @($DirtyPaths)
    if ($dirtyArray.Count -eq 0) {
        return $true
    }

    $disallowed = @(Get-DisallowedDirtyPaths -Paths $dirtyArray -Regexes $EffectiveRegexes)
    if ($disallowed.Count -gt 0) {
        $sample = ($disallowed | Select-Object -First 10) -join ", "
        throw "$Label has disallowed dirty files: $sample"
    }

    if ($DirtyPolicy -eq "fail") {
        $sample = ($dirtyArray | Select-Object -First 10) -join ", "
        throw "$Label has dirty files that match allowed criteria, but DirtyPolicy=fail: $sample"
    }

    return $true
}

function Get-LocalDirtyPaths {
    param([string]$Path)

    Push-Location $Path
    try {
        $statusLines = @(git status --porcelain)
        return Parse-StatusPaths -StatusLines $statusLines
    }
    finally {
        Pop-Location
    }
}

function Stash-LocalChanges {
    param(
        [string]$Path,
        [string]$Label
    )

    $stashMessage = "parity-sync/$Label/$(Get-Date -Format yyyyMMdd_HHmmss)"
    Push-Location $Path
    try {
        git stash push -u -m $stashMessage | Out-Null
        $stashRef = (git stash list -n 1 --pretty=%gd).Trim()
        if ([string]::IsNullOrWhiteSpace($stashRef)) {
            throw "$Label failed to create stash for dirty-state handling."
        }

        return $stashRef
    }
    finally {
        Pop-Location
    }
}

function Sync-LocalCheckout {
    param(
        [string]$Path,
        [string]$Branch,
        [string]$UpstreamRef
    )

    Push-Location $Path
    try {
        git fetch --all --prune | Out-Null
        git rev-parse --verify $UpstreamRef | Out-Null

        $branchExists = (git branch --list $Branch)
        if ([string]::IsNullOrWhiteSpace($branchExists)) {
            git switch -c $Branch $UpstreamRef | Out-Null
        }
        else {
            git switch $Branch | Out-Null
            git merge --ff-only $UpstreamRef | Out-Null
        }

        $head = (git rev-parse --short HEAD).Trim()
        Write-Host "[LocalParity] branch=$Branch head=$head upstream=$UpstreamRef"
    }
    finally {
        Pop-Location
    }
}

function Invoke-LaptopCommand {
    param(
        [string]$KeyPath,
        [string]$User,
        [string]$HostName,
        [string]$Script
    )

    $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($Script))
    $output = & ssh -i $KeyPath -o IdentitiesOnly=yes "$User@$HostName" "powershell -NoProfile -EncodedCommand $encoded" 2>&1
    if ($LASTEXITCODE -ne 0) {
        $errorText = ($output | Out-String).Trim()
        throw "Laptop command failed for $User@$HostName. $errorText"
    }

    return @($output)
}

function Get-LaptopDirtyPaths {
    param(
        [string]$KeyPath,
        [string]$User,
        [string]$HostName,
        [string]$Path
    )

    $pathEsc = Escape-SingleQuoted $Path
    $script = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path '{0}')) {{
    throw "Laptop repo path not found: {0}"
}}

Push-Location '{0}'
try {{
    git status --porcelain
}}
finally {{
    Pop-Location
}}
'@ -f $pathEsc

    $statusLines = Invoke-LaptopCommand -KeyPath $KeyPath -User $User -HostName $HostName -Script $script
    return Parse-StatusPaths -StatusLines $statusLines
}

function Stash-LaptopChanges {
    param(
        [string]$KeyPath,
        [string]$User,
        [string]$HostName,
        [string]$Path
    )

    $pathEsc = Escape-SingleQuoted $Path
    $stashMessageEsc = Escape-SingleQuoted ("parity-sync/LaptopRepo/{0}" -f (Get-Date -Format yyyyMMdd_HHmmss))
    $script = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Push-Location '{0}'
try {{
    git stash push -u -m '{1}' | Out-Null
    $stashRef = (git stash list -n 1 --pretty=%gd).Trim()
    if ([string]::IsNullOrWhiteSpace($stashRef)) {{
        throw "Laptop failed to create stash for dirty-state handling."
    }}

    Write-Output $stashRef
}}
finally {{
    Pop-Location
}}
'@ -f $pathEsc, $stashMessageEsc

    $output = Invoke-LaptopCommand -KeyPath $KeyPath -User $User -HostName $HostName -Script $script
    return ($output | Select-Object -Last 1).Trim()
}

function Sync-LaptopCheckout {
    param(
        [string]$KeyPath,
        [string]$User,
        [string]$HostName,
        [string]$RepoPathValue,
        [string]$Branch,
        [string]$UpstreamRef
    )

    $repoEsc = Escape-SingleQuoted $RepoPathValue
    $branchEsc = Escape-SingleQuoted $Branch
    $upstreamEsc = Escape-SingleQuoted $UpstreamRef

    $script = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path '{0}')) {{
    throw "Laptop repo path not found: {0}"
}}

Push-Location '{0}'
try {{
    git fetch --all --prune | Out-Null
    git rev-parse --verify '{2}' | Out-Null

    $branchExists = (git branch --list '{1}')
    if ([string]::IsNullOrWhiteSpace($branchExists)) {{
        git switch -c '{1}' '{2}' | Out-Null
    }}
    else {{
        git switch '{1}' | Out-Null
        git merge --ff-only '{2}' | Out-Null
    }}

    $head = (git rev-parse --short HEAD).Trim()
    Write-Output "[LaptopParity] branch={1} head=$head upstream={2}"
}}
finally {{
    Pop-Location
}}
'@ -f $repoEsc, $branchEsc, $upstreamEsc

    $result = Invoke-LaptopCommand -KeyPath $KeyPath -User $User -HostName $HostName -Script $script
    foreach ($line in $result) {
        if (-not [string]::IsNullOrWhiteSpace($line)) {
            Write-Host $line
        }
    }
}

if (-not (Test-Path $RepoPath)) {
    throw "Repo path not found: $RepoPath"
}

$effectiveAllowedRegex = New-Object System.Collections.Generic.List[string]
foreach ($rx in $AllowedDirtyRegex) {
    if (-not [string]::IsNullOrWhiteSpace($rx)) {
        $effectiveAllowedRegex.Add($rx)
    }
}

if ($AllowMetaDirty) {
    $effectiveAllowedRegex.Add('\.meta$')
}

Push-Location $RepoPath
try {
    git fetch --all --prune | Out-Null

    $currentBranch = (git rev-parse --abbrev-ref HEAD).Trim()
    if ([string]::IsNullOrWhiteSpace($PushBranch)) {
        if ($Mode -eq "validator") {
            $PushBranch = "main"
        }
        else {
            $PushBranch = $currentBranch
        }
    }

    $pushUpstreamRef = "$Remote/$PushBranch"
    if ([string]::IsNullOrWhiteSpace($LocalParityUpstreamRef)) {
        if ($Mode -eq "validator") {
            $LocalParityUpstreamRef = "$Remote/main"
        }
        else {
            $LocalParityUpstreamRef = $pushUpstreamRef
        }
    }

    if ([string]::IsNullOrWhiteSpace($LaptopParityUpstreamRef)) {
        $LaptopParityUpstreamRef = $LocalParityUpstreamRef
    }
}
finally {
    Pop-Location
}

$localStashRef = ""
$laptopStashRef = ""

if ((-not $SkipPush) -or (-not $SkipLocalParity)) {
    $localDirtyPaths = @(Get-LocalDirtyPaths -Path $RepoPath)
    if ($localDirtyPaths.Count -gt 0) {
        Ensure-AllowedDirtyState -Label "Local repo" -DirtyPaths $localDirtyPaths -EffectiveRegexes $effectiveAllowedRegex.ToArray() | Out-Null
        if ($DirtyPolicy -eq "stash-allowed") {
            $localStashRef = Stash-LocalChanges -Path $RepoPath -Label "LocalRepo"
            Write-Host "[LocalDirty] stashed at $localStashRef"
        }
    }
}

if (-not $SkipPush) {
    Push-Location $RepoPath
    try {
        git rev-parse --verify "refs/heads/$PushBranch" | Out-Null
        $null = git rev-parse --abbrev-ref --symbolic-full-name "$PushBranch@{u}" 2>$null
        $hasUpstream = ($LASTEXITCODE -eq 0)

        if ($hasUpstream) {
            git push $Remote $PushBranch
        }
        else {
            git push --set-upstream $Remote $PushBranch
        }
    }
    finally {
        Pop-Location
    }
}

if (-not $SkipLocalParity) {
    Sync-LocalCheckout -Path $RepoPath -Branch $LocalParityBranch -UpstreamRef $LocalParityUpstreamRef
}

if (-not $SkipLaptopParity) {
    $resolvedKey = Resolve-LaptopKeyPath -Requested $LaptopKeyPath
    $laptopDirtyPaths = @(Get-LaptopDirtyPaths -KeyPath $resolvedKey -User $LaptopUser -HostName $LaptopHost -Path $LaptopRepoPath)
    if ($laptopDirtyPaths.Count -gt 0) {
        Ensure-AllowedDirtyState -Label "Laptop repo" -DirtyPaths $laptopDirtyPaths -EffectiveRegexes $effectiveAllowedRegex.ToArray() | Out-Null
        if ($DirtyPolicy -eq "stash-allowed") {
            $laptopStashRef = Stash-LaptopChanges -KeyPath $resolvedKey -User $LaptopUser -HostName $LaptopHost -Path $LaptopRepoPath
            Write-Host "[LaptopDirty] stashed at $laptopStashRef"
        }
    }

    Sync-LaptopCheckout -KeyPath $resolvedKey -User $LaptopUser -HostName $LaptopHost -RepoPathValue $LaptopRepoPath -Branch $LaptopParityBranch -UpstreamRef $LaptopParityUpstreamRef
}

Push-Location $RepoPath
try {
    $localHead = (git rev-parse --short HEAD).Trim()
    Write-Host "[Done] mode=$Mode"
    Write-Host "[Done] repo=$RepoPath"
    Write-Host "[Done] push_ref=$PushBranch"
    Write-Host "[Done] local_head=$localHead"
    Write-Host "[Done] local_parity=$LocalParityBranch <= $LocalParityUpstreamRef"
    if (-not $SkipLaptopParity) {
        Write-Host "[Done] laptop_parity=$LaptopParityBranch <= $LaptopParityUpstreamRef @ $LaptopUser@$LaptopHost"
    }

    if (-not [string]::IsNullOrWhiteSpace($localStashRef)) {
        Write-Host "[Done] local_stash=$localStashRef"
    }

    if (-not [string]::IsNullOrWhiteSpace($laptopStashRef)) {
        Write-Host "[Done] laptop_stash=$laptopStashRef"
    }
}
finally {
    Pop-Location
}

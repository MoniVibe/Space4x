param(
    [string]$Space4xRoot,
    [string]$PureDotsRoot,
    [string]$ReportPath,
    [switch]$FailOnMismatch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-TypeRef {
    param([string]$TypeRef)
    if ([string]::IsNullOrWhiteSpace($TypeRef)) {
        return ""
    }

    $normalized = $TypeRef.Trim()
    $normalized = $normalized -replace "^global::", ""
    $normalized = $normalized -replace "\s+", ""
    return $normalized
}

function Get-DefaultPath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    if (Test-Path $Path) {
        return (Resolve-Path $Path).Path
    }

    return $null
}

function Parse-SystemDeclarations {
    param([string]$FilePath)

    $text = Get-Content -Path $FilePath -Raw
    if ($null -eq $text) {
        return [System.Collections.Generic.List[object]]::new()
    }

    $namespaceMatch = [regex]::Match($text, "(?m)^\s*namespace\s+([A-Za-z0-9_.]+)\s*(?:;|\{)")
    $namespace = if ($namespaceMatch.Success) { $namespaceMatch.Groups[1].Value } else { "" }

    $usingAliases = @{}
    foreach ($match in [regex]::Matches($text, "(?m)^\s*using\s+([A-Za-z_]\w*)\s*=\s*([A-Za-z0-9_.:]+)\s*;")) {
        $alias = $match.Groups[1].Value
        $target = Normalize-TypeRef $match.Groups[2].Value
        $usingAliases[$alias] = $target
    }

    $usingNamespaces = [System.Collections.Generic.List[string]]::new()
    foreach ($match in [regex]::Matches($text, "(?m)^\s*using\s+([A-Za-z0-9_.:]+)\s*;")) {
        $candidate = Normalize-TypeRef $match.Groups[1].Value
        if ($candidate.Contains("=")) {
            continue
        }

        if (-not $usingNamespaces.Contains($candidate)) {
            $usingNamespaces.Add($candidate)
        }
    }

    $declarations = [System.Collections.Generic.List[object]]::new()
    $declarationPattern = "(?ms)(?<attrs>(?:\s*\[[^\]]+\]\s*)+)\s*(?:public|internal|protected|private)?\s*(?:partial\s+)?(?:struct|class)\s+(?<name>[A-Za-z_]\w*)(?:\s*:\s*(?<base>[^\{\n]+))?"
    foreach ($match in [regex]::Matches($text, $declarationPattern)) {
        $attrs = $match.Groups["attrs"].Value
        if ($attrs -notmatch "Update(?:InGroup|After|Before)\s*\(") {
            continue
        }

        $name = $match.Groups["name"].Value
        $fullName = if ([string]::IsNullOrWhiteSpace($namespace)) { $name } else { "$namespace.$name" }

        $groups = @([regex]::Matches($attrs, "UpdateInGroup\s*\(\s*typeof\(([^)]+)\)") | ForEach-Object { Normalize-TypeRef $_.Groups[1].Value })
        $after = @([regex]::Matches($attrs, "UpdateAfter\s*\(\s*typeof\(([^)]+)\)") | ForEach-Object { Normalize-TypeRef $_.Groups[1].Value })
        $before = @([regex]::Matches($attrs, "UpdateBefore\s*\(\s*typeof\(([^)]+)\)") | ForEach-Object { Normalize-TypeRef $_.Groups[1].Value })

        $declarations.Add([pscustomobject]@{
                FilePath        = $FilePath
                Namespace       = $namespace
                Name            = $name
                FullName        = $fullName
                UsingAliases    = $usingAliases.Clone()
                UsingNamespaces = @($usingNamespaces)
                GroupsRaw       = @($groups)
                AfterRaw        = @($after)
                BeforeRaw       = @($before)
            })
    }

    return $declarations
}

function Resolve-TypeCandidates {
    param(
        [string]$TypeRef,
        [pscustomobject]$Declaration,
        [hashtable]$SimpleNameMap
    )

    $token = Normalize-TypeRef $TypeRef
    if ([string]::IsNullOrWhiteSpace($token)) {
        return @()
    }

    if ($Declaration.UsingAliases.ContainsKey($token)) {
        return @($Declaration.UsingAliases[$token])
    }

    if ($token.Contains(".")) {
        return @($token)
    }

    $candidates = [System.Collections.Generic.HashSet[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($Declaration.Namespace)) {
        $segments = $Declaration.Namespace.Split(".")
        for ($i = $segments.Length; $i -ge 1; $i--) {
            $prefix = ($segments[0..($i - 1)] -join ".")
            $candidates.Add("$prefix.$token") | Out-Null
        }
    }

    foreach ($ns in $Declaration.UsingNamespaces) {
        if ([string]::IsNullOrWhiteSpace($ns)) {
            continue
        }

        if ($ns.StartsWith("static")) {
            continue
        }

        $candidates.Add("$ns.$token") | Out-Null
    }

    if ($SimpleNameMap.ContainsKey($token) -and $SimpleNameMap[$token].Count -eq 1) {
        $candidates.Add($SimpleNameMap[$token][0]) | Out-Null
    }

    $candidates.Add($token) | Out-Null
    return @($candidates)
}

function Resolve-Groups {
    param(
        [string[]]$GroupRefs,
        [pscustomobject]$Declaration,
        [hashtable]$FullNameMap,
        [hashtable]$SimpleNameMap
    )

    $resolved = [System.Collections.Generic.HashSet[string]]::new()
    $unresolved = [System.Collections.Generic.List[string]]::new()

    foreach ($groupRef in $GroupRefs) {
        $resolvedCurrent = $false
        foreach ($candidate in Resolve-TypeCandidates -TypeRef $groupRef -Declaration $Declaration -SimpleNameMap $SimpleNameMap) {
            if ($FullNameMap.ContainsKey($candidate)) {
                $resolved.Add($candidate) | Out-Null
                $resolvedCurrent = $true
                break
            }

            if ($script:KnownExternalTypeGroups.ContainsKey($candidate)) {
                $resolved.Add($candidate) | Out-Null
                $resolvedCurrent = $true
                break
            }
        }

        if (-not $resolvedCurrent) {
            $unresolved.Add($groupRef) | Out-Null
        }
    }

    return [pscustomobject]@{
        Resolved   = @($resolved)
        Unresolved = @($unresolved)
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Space4xRoot)) {
    $Space4xRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
} else {
    $Space4xRoot = (Resolve-Path $Space4xRoot).Path
}

if ([string]::IsNullOrWhiteSpace($PureDotsRoot)) {
    $pureDotsCandidate = Get-DefaultPath (Join-Path $scriptRoot "..\..\puredots")
    if ($null -ne $pureDotsCandidate) {
        $PureDotsRoot = $pureDotsCandidate
    } else {
        $PureDotsRoot = (Resolve-Path (Join-Path $Space4xRoot "..\puredots")).Path
    }
} else {
    $PureDotsRoot = (Resolve-Path $PureDotsRoot).Path
}

$scanRoots = [System.Collections.Generic.List[string]]::new()
$space4xScripts = Join-Path $Space4xRoot "Assets\Scripts"
$pureDotsRuntime = Join-Path $PureDotsRoot "Packages\com.moni.puredots\Runtime"

if (Test-Path $space4xScripts) {
    $scanRoots.Add($space4xScripts)
}

if (Test-Path $pureDotsRuntime) {
    $scanRoots.Add($pureDotsRuntime)
}

if ($scanRoots.Count -eq 0) {
    throw "No scan roots found. Checked '$space4xScripts' and '$pureDotsRuntime'."
}

$script:KnownExternalTypeGroups = @{
    "Unity.Rendering.EntitiesGraphicsSystem" = @("Unity.Entities.PresentationSystemGroup")
    "Unity.Physics.Systems.BuildPhysicsWorld" = @("Unity.Physics.Systems.PhysicsInitializeGroup")
    "Unity.Physics.Systems.PhysicsInitializeGroup" = @("Unity.Physics.Systems.PhysicsSystemGroup")
    "Unity.Physics.Systems.PhysicsSimulationGroup" = @("Unity.Physics.Systems.PhysicsSystemGroup")
    "Unity.Physics.Systems.PhysicsSystemGroup" = @("Unity.Entities.FixedStepSimulationSystemGroup")
}

$files = @($scanRoots | ForEach-Object { Get-ChildItem -Path $_ -Filter *.cs -File -Recurse })

$declarations = [System.Collections.Generic.List[object]]::new()
foreach ($file in $files) {
    foreach ($decl in Parse-SystemDeclarations -FilePath $file.FullName) {
        $declarations.Add($decl)
    }
}

$fullNameMap = @{}
$simpleNameMap = @{}
foreach ($decl in $declarations) {
    $fullNameMap[$decl.FullName] = $decl
    if (-not $simpleNameMap.ContainsKey($decl.Name)) {
        $simpleNameMap[$decl.Name] = [System.Collections.Generic.List[string]]::new()
    }

    $simpleNameMap[$decl.Name].Add($decl.FullName)
}

$resolvedGroupMap = @{}
foreach ($decl in $declarations) {
    $resolvedGroupMap[$decl.FullName] = Resolve-Groups -GroupRefs $decl.GroupsRaw -Declaration $decl -FullNameMap $fullNameMap -SimpleNameMap $simpleNameMap
}

$mismatches = [System.Collections.Generic.List[object]]::new()
$unresolvedRelations = [System.Collections.Generic.List[object]]::new()
$relationCount = 0

foreach ($decl in $declarations) {
    $sourceGroupInfo = $resolvedGroupMap[$decl.FullName]
    $sourceGroups = @($sourceGroupInfo.Resolved)
    $relations = [System.Collections.Generic.List[object]]::new()
    foreach ($target in $decl.AfterRaw) {
        $relations.Add([pscustomobject]@{ Kind = "UpdateAfter"; Target = $target })
    }
    foreach ($target in $decl.BeforeRaw) {
        $relations.Add([pscustomobject]@{ Kind = "UpdateBefore"; Target = $target })
    }

    foreach ($relation in $relations) {
        $relationCount++
        $targetGroups = @()
        $resolvedTargetType = $null

        foreach ($candidate in Resolve-TypeCandidates -TypeRef $relation.Target -Declaration $decl -SimpleNameMap $simpleNameMap) {
            if ($fullNameMap.ContainsKey($candidate)) {
                $resolvedTargetType = $candidate
                $targetDecl = $fullNameMap[$candidate]
                $targetGroups = @((Resolve-Groups -GroupRefs $targetDecl.GroupsRaw -Declaration $targetDecl -FullNameMap $fullNameMap -SimpleNameMap $simpleNameMap).Resolved)
                break
            }

            if ($script:KnownExternalTypeGroups.ContainsKey($candidate)) {
                $resolvedTargetType = $candidate
                $targetGroups = @($script:KnownExternalTypeGroups[$candidate])
                break
            }
        }

        if ($sourceGroups.Count -eq 0 -or $targetGroups.Count -eq 0) {
            $unresolvedRelations.Add([pscustomobject]@{
                    Source        = $decl.FullName
                    SourceFile    = $decl.FilePath
                    Kind          = $relation.Kind
                    Target        = $relation.Target
                    ResolvedTarget = if ($null -eq $resolvedTargetType) { "" } else { $resolvedTargetType }
                    SourceGroups  = ($sourceGroups -join ", ")
                    TargetGroups  = ($targetGroups -join ", ")
                })
            continue
        }

        $sharedGroups = @($sourceGroups | Where-Object { $targetGroups -contains $_ })
        if ($sharedGroups.Count -eq 0) {
            $mismatches.Add([pscustomobject]@{
                    Source        = $decl.FullName
                    SourceFile    = $decl.FilePath
                    Kind          = $relation.Kind
                    Target        = if ($null -eq $resolvedTargetType) { $relation.Target } else { $resolvedTargetType }
                    SourceGroups  = ($sourceGroups -join ", ")
                    TargetGroups  = ($targetGroups -join ", ")
                })
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $ReportPath = Join-Path $Space4xRoot "reports\runtime_ordering_audit_$timestamp.md"
}

$reportDir = Split-Path -Parent $ReportPath
if (-not (Test-Path $reportDir)) {
    New-Item -Path $reportDir -ItemType Directory -Force | Out-Null
}

$reportLines = [System.Collections.Generic.List[string]]::new()
$reportLines.Add("# Runtime Ordering Audit")
$reportLines.Add("")
$reportLines.Add("- Generated: $(Get-Date -Format o)")
$reportLines.Add("- Space4X root: $Space4xRoot")
$reportLines.Add("- PureDOTS root: $PureDotsRoot")
$reportLines.Add("- Files scanned: $($files.Count)")
$reportLines.Add("- Declarations parsed: $($declarations.Count)")
$reportLines.Add("- Ordering relations checked: $relationCount")
$reportLines.Add("- Potential mismatches: $($mismatches.Count)")
$reportLines.Add("- Unresolved relations: $($unresolvedRelations.Count)")
$reportLines.Add("")

$reportLines.Add("## Potential Mismatches")
$reportLines.Add("")
if ($mismatches.Count -eq 0) {
    $reportLines.Add("None.")
} else {
    $reportLines.Add("| Kind | Source | Target | Source Groups | Target Groups | File |")
    $reportLines.Add("| --- | --- | --- | --- | --- | --- |")
    foreach ($item in $mismatches | Sort-Object Source, Kind, Target) {
        $reportLines.Add("| $($item.Kind) | $($item.Source) | $($item.Target) | $($item.SourceGroups) | $($item.TargetGroups) | $($item.SourceFile) |")
    }
}

$reportLines.Add("")
$reportLines.Add("## Unresolved Relations")
$reportLines.Add("")
if ($unresolvedRelations.Count -eq 0) {
    $reportLines.Add("None.")
} else {
    $reportLines.Add("| Kind | Source | Target | Resolved Target | Source Groups | Target Groups | File |")
    $reportLines.Add("| --- | --- | --- | --- | --- | --- | --- |")
    foreach ($item in $unresolvedRelations | Sort-Object Source, Kind, Target) {
        $reportLines.Add("| $($item.Kind) | $($item.Source) | $($item.Target) | $($item.ResolvedTarget) | $($item.SourceGroups) | $($item.TargetGroups) | $($item.SourceFile) |")
    }
}

Set-Content -Path $ReportPath -Value $reportLines -Encoding UTF8

Write-Host "Runtime ordering audit complete."
Write-Host "  Files scanned: $($files.Count)"
Write-Host "  Declarations parsed: $($declarations.Count)"
Write-Host "  Relations checked: $relationCount"
Write-Host "  Potential mismatches: $($mismatches.Count)"
Write-Host "  Unresolved relations: $($unresolvedRelations.Count)"
Write-Host "  Report: $ReportPath"

if ($FailOnMismatch -and $mismatches.Count -gt 0) {
    exit 2
}

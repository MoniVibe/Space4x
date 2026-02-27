# Space4X Offline Tools

Most scripts here are offline post-processing helpers. A few preflight scripts invoke Unity directly.

## Telemetry Summarizer

Input: telemetry NDJSON, optional metrics JSON/CSV.  
Output: `summary.json` + `summary.md`.

```bash
python Tools/Telemetry/space4x_summarize_run.py \
  --telemetry path/to/telemetry.ndjson \
  --metrics_json path/to/metrics.json \
  --metrics_csv path/to/metrics.csv \
  --out_dir path/to/output
```

## Scenario Beats Generator

Input: Space4X scenario JSON.  
Output: beats JSON for shot-direction timelines.

```bash
python Tools/Scenarios/space4x_generate_beats.py \
  --scenario Assets/Scenarios/space4x_mining_combat.json \
  --out path/to/beats.json
```

## Iterator Compile Preflight

Runs a compile-only Unity batch pass and fails if compile errors are detected.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/IteratorCompilePreflight.ps1 `
  -RepoPath C:\dev\Tri\space4x_ultimate
```

Optional:
- `-UnityExe <path>` to override editor selection.
- `-LogPath <path>` to override output log path.
- `-TimeoutSec <seconds>` to control compile timeout.
- If `-UnityExe` is omitted, the script auto-resolves via:
  1. `ProjectSettings/ProjectVersion.txt`,
  2. `UNITY_EXE` env var,
  3. latest installed Hub editor.

## Resolve Unity Editor

Resolves which Unity executable should be used for a repo lane.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/ResolveUnityEditor.ps1 `
  -RepoPath C:\dev\Tri\space4x
```

Optional:
- `-UnityExe <path>` explicit override.
- `-PreferEnvUnityExe` to force env var precedence over project version.
- `-EmitJson` to return a JSON object with source metadata.

## Unity CLI Test Lane Runner

Runs EditMode/PlayMode tests in a dedicated CLI lane while your interactive editor can remain open in another clone/worktree.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/RunUnityCliTests.ps1 `
  -RepoPath C:\dev\Tri\space4x `
  -TestPlatform PlayMode `
  -TestFilter Space4XUiUxKernelTests
```

Notes:
- Use a separate repo lane path from your open editor lane.
- The script blocks if `Temp/UnityLockfile` exists (unless `-IgnoreProjectLock` is passed).
- Unity version is auto-resolved with `Tools/ResolveUnityEditor.ps1` when `-UnityExe` is omitted.
- `-AssemblyNames` can be used for deterministic assembly-targeted runs.
- PlayMode runs default to a headless-safe env (`PUREDOTS_EXIT_POLICY=nevernonzero`) so scenario invariant systems do not terminate Unity before test XML is written.
- Pass `-AllowHeadlessProcessExit` to opt out and keep legacy exit behavior.
- If Unity hangs after writing test XML, the script now parses persisted results and reports them.

## Unity Parallel Lane Wrapper (Recommended)

Runs a parity gate first, then executes CLI tests in the dedicated lane.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/RunUnityCliLane.ps1 `
  -EditorRepoPath C:\dev\Tri\space4x_ultimate `
  -CliRepoPath C:\dev\Tri\space4x `
  -TestPlatform PlayMode `
  -TestFilter Space4XUiUxKernelTests
```

Parity gate checks by default:
- `ProjectSettings/ProjectVersion.txt`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- git `HEAD` commit

Use targeted drift overrides only when intentional:
- `-AllowCommitDrift`
- `-AllowEditorVersionDrift`
- `-AllowManifestDrift`
- `-AllowPackagesLockDrift`

## Assert Unity Lane Parity

Standalone parity check between editor lane and CLI lane.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/AssertUnityLaneParity.ps1 `
  -EditorRepoPath C:\dev\Tri\space4x_ultimate `
  -CliRepoPath C:\dev\Tri\space4x
```

## Iterator Bedrock Guard (Strict)

Enforces iterator drift guardrails before handoff:
- both `space4x` and `puredots` are refreshed and checked against `origin/main`,
- no behind drift,
- optional dirty-tree enforcement,
- compile preflight,
- open `needs-validate` PR awareness snapshot.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/IteratorBedrockGuard.ps1 `
  -RepoPath C:\dev\Tri\space4x_ultimate `
  -PuredotsRepoPath C:\dev\Tri\puredots_ultimate `
  -AwarenessNote "Reviewed open needs-validate queue before handoff"
```

Outputs:
- `Temp/iterator_bedrock_guard_<timestamp>.json` (pass/fail report).

## Iterator Guarded Handoff (Guard + Push + Parity Sync)

Single command to enforce guardrails, then push + parity sync if and only if guard passes.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/IteratorGuardedHandoff.ps1 `
  -RepoPath C:\dev\Tri\space4x_ultimate `
  -PuredotsRepoPath C:\dev\Tri\puredots_ultimate `
  -PushBranch <branch-name> `
  -AwarenessNote "Reviewed open needs-validate queue before handoff"
```

## Push + Cross-Machine Parity Sync

Pushes the active branch, fast-forwards local validator checkout, and fast-forwards laptop checkout to the same upstream ref.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/PushValidationAndSyncParity.ps1 `
  -RepoPath C:\dev\Tri\space4x_ultimate `
  -Mode iterator `
  -PushBranch feat/fleetcrawl-data-pass `
  -LocalParityBranch validator/ultimate-checkout `
  -LocalParityUpstreamRef origin/feat/fleetcrawl-data-pass `
  -LaptopRepoPath C:\dev\unity_clean\space4x `
  -LaptopParityBranch validator/ultimate-checkout `
  -LaptopParityUpstreamRef origin/feat/fleetcrawl-data-pass
```

Validator post-merge (sync both machines to `origin/main`):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/PushValidationAndSyncParity.ps1 `
  -RepoPath C:\dev\Tri\space4x_ultimate `
  -Mode validator `
  -PushBranch main `
  -LocalParityBranch validator/ultimate-checkout `
  -LaptopRepoPath C:\dev\unity_clean\space4x `
  -LaptopParityBranch validator/ultimate-checkout
```

Defaults:
- If `-PushBranch` is omitted, current branch is used.
- In `validator` mode, omitted `-PushBranch` defaults to `main`.
- In `validator` mode, omitted parity upstream defaults to `origin/main`.
- In `iterator` mode, omitted parity upstream defaults to `origin/<push-branch>`.
- If `-LaptopParityUpstreamRef` is omitted, it uses local parity upstream.
- SSH key fallback order: `buildbox_laptop_ed25519`, then `desktop_to_laptop_ed25519`.

Dirty repo options:
- Default `-DirtyPolicy fail` blocks sync if either repo is dirty.
- `-DirtyPolicy stash-allowed` stashes dirty state only if files match allowed regex filters.
- Add allow filters with `-AllowedDirtyRegex`.
- `-AllowMetaDirty` opt-in allows `.meta` files to be stashed automatically.

Example (allow only `.meta` and `console.md` dirty state to be auto-stashed):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/PushValidationAndSyncParity.ps1 `
  -RepoPath C:\dev\Tri\space4x_ultimate `
  -PushBranch feat/fleetcrawl-data-pass `
  -DirtyPolicy stash-allowed `
  -AllowedDirtyRegex '^console\.md$' `
  -AllowMetaDirty
```

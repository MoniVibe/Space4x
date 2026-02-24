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
  -UnityExe "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
  -RepoPath C:\dev\Tri\space4x
```

Optional:
- `-LogPath <path>` to override output log path.
- `-TimeoutSec <seconds>` to control compile timeout.

## Iterator Bedrock Guard (Strict)

Enforces iterator drift guardrails before handoff:
- both `space4x` and `puredots` are refreshed and checked against `origin/main`,
- no behind drift,
- optional dirty-tree enforcement,
- compile preflight,
- open `needs-validate` PR awareness snapshot.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/IteratorBedrockGuard.ps1 `
  -RepoPath C:\dev\Tri\space4x `
  -PuredotsRepoPath C:\dev\Tri\puredots `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
  -AwarenessNote "Reviewed open needs-validate queue before handoff"
```

Outputs:
- `Temp/iterator_bedrock_guard_<timestamp>.json` (pass/fail report).

## Iterator Guarded Handoff (Guard + Push + Parity Sync)

Single command to enforce guardrails, then push + parity sync if and only if guard passes.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/IteratorGuardedHandoff.ps1 `
  -RepoPath C:\dev\Tri\space4x `
  -PuredotsRepoPath C:\dev\Tri\puredots `
  -PushBranch <branch-name> `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
  -AwarenessNote "Reviewed open needs-validate queue before handoff"
```

## Push + Cross-Machine Parity Sync

Pushes the active branch, fast-forwards local validator checkout, and fast-forwards laptop checkout to the same upstream ref.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/PushValidationAndSyncParity.ps1 `
  -RepoPath C:\dev\Tri\space4x `
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
  -RepoPath C:\dev\Tri\space4x `
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
  -RepoPath C:\dev\Tri\space4x `
  -PushBranch feat/fleetcrawl-data-pass `
  -DirtyPolicy stash-allowed `
  -AllowedDirtyRegex '^console\.md$' `
  -AllowMetaDirty
```

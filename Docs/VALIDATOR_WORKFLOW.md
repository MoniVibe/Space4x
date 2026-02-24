# Validator Workflow (Iterators + Validator)

This repo uses a hub-and-spoke model:
- Many iterator agents push changes.
- One validator agent runs Buildbox and merges.

Goal: keep the expensive truth (Buildbox headless verdict) serialized and consistent, while iterators move quickly.

## Definitions

Super green:
- **space4x**: Buildbox headless smoke `SUCCESS` on `Assets/Scenarios/space4x_collision_micro.json`
- **godgame**: Buildbox headless smoke `SUCCESS` on `Assets/Scenarios/Godgame/godgame_smoke.json`

Validator priority:
1. Restore super green (both titles) if broken.
2. Then greenify PR backlog.

## Iterator Agent Rules (No Validation)

Iterators propose, they do not prove:
- Create a branch (one goal).
- Implement changes.
- Run guarded handoff (`Tools/IteratorGuardedHandoff.ps1`) until green.
- Open a PR.
- Add label `needs-validate`.
- Stop. Do not trigger Buildbox / nightlies / queues.

Detailed iterator contract:
- `Docs/Operations/ITERATORS.md`

Required local preflight before every push:
- Refresh from current `main` first (`fetch` + `pull --ff-only` on `main`, then branch from updated `main`).
- Run the guarded handoff script so freshness + awareness + compile + parity sync are enforced together.
- If preflight introduces unexpected large deletions/churn, stop and triage; do not continue with blind merge replay.

Guarded handoff command (mandatory for desktop iterators):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/IteratorGuardedHandoff.ps1 `
  -RepoPath C:\dev\Tri\space4x_ultimate `
  -PuredotsRepoPath C:\dev\Tri\puredots_ultimate `
  -PushBranch <branch-name> `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
  -AwarenessNote "Reviewed open needs-validate queue before handoff"
```

Guarded handoff gates:
- `space4x` and `puredots` not behind `origin/main`.
- No `error CS*` entries in the compile log.
- No `Compilation failed` markers in the compile log.
- Open `needs-validate` queue reviewed (`-AwarenessNote` recorded in guard report).

PR intent card (required in PR description):
- Summary (1-5 bullets)
- Invariants (what must remain true)
- Acceptance checks (how to tell it worked)
- Risk flags: `Assets/.meta`, `Packages/*lock*`, cross-repo pin changes, determinism risk
- Guard report path (`Temp/iterator_bedrock_guard_<timestamp>.json`)
- Compile preflight log path (`Temp/iterator_compile_preflight.log`)
- Burst plan (see below)

Burst plan for iterators:
- Default fast loop: validate logic with **Burst-off** first when you do local headless runs.
- Set one of:
  - `PUREDOTS_DISABLE_BURST=1`
  - `TRI_DISABLE_BURST=1`
- Still avoid introducing Burst hazards (static `FixedString*Bytes` initializers, managed APIs in Burst jobs).

## Cross-Machine Parity Sync (Required Before Manual Editor Validation)

When an iterator pushes a branch for validation, sync both desktop and laptop validation checkouts to the same ref:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/PushValidationAndSyncParity.ps1 `
  -RepoPath C:\dev\Tri\space4x_ultimate `
  -PushBranch <branch-name> `
  -LocalParityBranch validator/ultimate-checkout `
  -LocalParityUpstreamRef origin/<branch-name> `
  -LaptopRepoPath C:\dev\unity_clean\space4x `
  -LaptopParityBranch validator/ultimate-checkout `
  -LaptopParityUpstreamRef origin/<branch-name>
```

Policy:
- Do not validate from floating `main` while iterating feature branches.
- Validate from `validator/ultimate-checkout` only.
- Keep parity branch fast-forward only (`merge --ff-only`); no direct feature commits on parity branch.
- If either machine has a dirty tree, stop and resolve before sync.

## Complete Pairing (Space4x + PureDOTS + Buildbox)

`space4x` branch parity alone is not enough. Full parity requires a locked `puredots` ref and validator dispatch with both refs.

1. Sync `space4x` parity branch on both machines (commands above).
2. Ensure `Packages/manifest.json` resolves `com.moni.puredots` on both machines:
   - Desktop: `C:\dev\Tri\puredots_ultimate\Packages\com.moni.puredots\package.json`
   - Laptop: `C:\dev\puredots\Packages\com.moni.puredots\package.json`
3. Pin `puredots` to one explicit ref on both machines.
4. Trigger Buildbox with both refs (`-Ref` and `-PuredotsRef`).

`puredots` pin example:

```powershell
git -C C:\dev\Tri\puredots_ultimate fetch --all --prune
git -C C:\dev\Tri\puredots_ultimate checkout <puredots-ref>
```

Buildbox dual-ref dispatch example:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\dev\Tri\Tools\HeadlessRebuildTool\scripts\trigger_buildbox.ps1 `
  -Title space4x `
  -Ref <space4x-ref-or-branch> `
  -PuredotsRef <puredots-ref-or-branch> `
  -WaitForResult
```

Fast movement/camera drift lane (2-3 minute timeout, strict runtime + PlayMode probe):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\dev\Tri\Tools\HeadlessRebuildTool\scripts\trigger_buildbox.ps1 `
  -Title space4x `
  -Ref <space4x-ref-or-branch> `
  -ScenarioRel Assets/Scenarios/space4x_smoke.json `
  -TimeoutSec 180 `
  -PureGreen `
  -PureGreenPlayMode `
  -EnvJson '{"TRI_RUNTIME_HEALTH_STRICT":"1","TRI_RUNTIME_PHYSICS_STRICT":"1","TRI_PLAYMODE_PHYSICAL_PROBE":"1","TRI_PLAYMODE_PHYSICAL_STRICT":"1","SPACE4X_ENTITY_VISIBILITY_PROBE":"1","SPACE4X_FLAGSHIP_PIPELINE_PROBE":"1","SPACE4X_FLAGSHIP_PIPELINE_PROBE_ECHO":"0"}' `
  -WaitForResult
```

Nightly movement-polish deck (local queue, short loops):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\dev\Tri\Tools\HeadlessRebuildTool\Polish\run_deck.ps1 `
  -DeckPath C:\dev\Tri\Tools\HeadlessRebuildTool\Polish\Decks\space4x_movement_polish_20260223.json `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
  -AllowLocalBuild `
  -Mode run
```

If `puredots` is not explicitly paired, validator can produce extra/missing compile errors versus editor.

Parity check command (editor vs validator artifacts):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/CheckEditorValidatorParity.ps1 `
  -ConsolePath C:\dev\Tri\console.md `
  -RunId <buildbox-run-id> `
  -RunRepo MoniVibe/HeadlessRebuildTool
```

Validator post-greenify sync (both machines -> `origin/main`):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/PushValidationAndSyncParity.ps1 `
  -RepoPath C:\dev\Tri\space4x_ultimate `
  -Mode validator `
  -PushBranch main `
  -LocalParityBranch validator/ultimate-checkout `
  -LaptopRepoPath C:\dev\unity_clean\space4x `
  -LaptopParityBranch validator/ultimate-checkout
```

Dirty criteria guidance:
- Prefer clean trees (`-DirtyPolicy fail`).
- Use `-DirtyPolicy stash-allowed` only for known transient diffs.
- Do not blanket-ignore `.meta` by default; only opt in (`-AllowMetaDirty`) when intentionally stashing temporary presentation churn.

## Validator Agent Responsibilities (Buildbox Only)

Validator is the only actor allowed to:
- Trigger Buildbox runs.
- Apply fix-up commits to make PR branches green.
- Merge PRs (or stage then merge).

Evidence order (do not freestyle):
1. `out/run_summary_min.json`
2. `out/run_summary.json`
3. `meta.json`
4. `out/watchdog.json` + `out/player.log`/`out/stdout.log`/`out/stderr.log`

Stop conditions:
- 5 iterations without improvement
- same failure twice
- infra failure twice

Local intake ledger:
- `C:\polish\queue\reports\pending_prs_to_greenify.md`

Validator exec runner (buildbox machine):
```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\polish\queue\reports\validator_exec.ps1
```

## Optional Burst Gate (Before Merge)

Recommended merge gate:
1. Burst-off smoke (fast sanity)
2. Burst-on smoke (real performance path)
3. Longer Burst-on matrix in nightlies

Note: `PUREDOTS_DISABLE_BURST` disables Burst at runtime; it may not remove all build-time Burst cost.

## Runtime Health Signals (Space4X)

Buildbox smoke now surfaces scenario/presentation health from `out/player.log`:
- `runtime_scenario_id`
- `runtime_scenario_path`
- `runtime_render_catalog_ready`
- `runtime_presentation_has_catalog`
- `runtime_render_validation_count`
- `runtime_render_validation_first`
- `runtime_movement_diag_fail_count`
- `runtime_movement_diag_fail_first`
- `runtime_movement_diag_warn_count`
- `runtime_movement_diag_warn_first`
- `runtime_movement_diag_turnrate_signals`
- `runtime_movement_diag_turnrate_first`

Optional deep movement/render ownership signal:
- Enable `SPACE4X_FLAGSHIP_PIPELINE_PROBE=1` to emit JSONL records to `space4x_flagship_pipeline_probe.jsonl`.
- Probe captures: controlled/target/tracked entity chain, movement writer hints, render writer hints, variant-vs-catalog range checks, and disappearance reason classification.
- `runtime_visibility_event_count`
- `runtime_visibility_drop_event_count`
- `runtime_visibility_flagship_lost_count`
- `runtime_visibility_flagship_non_renderable_count`
- `runtime_visibility_first_event`

Strict mode (fails the run when issues are detected):
- `PURE_GREEN` runs are strict by default.
- Override with `TRI_RUNTIME_HEALTH_STRICT=1` for all Space4X smoke runs.
- Add `TRI_RUNTIME_PHYSICS_STRICT=1` to fail on movement warnings/turn-rate drift signals.
- Runtime visibility probe can be enabled in batch/headless with:
  - `SPACE4X_ENTITY_VISIBILITY_PROBE=1`
  - `SPACE4X_ENTITY_VISIBILITY_PROBE_OUT=<path>`
- `pipeline_smoke.ps1` auto-injects `SPACE4X_ENTITY_VISIBILITY_PROBE=1` when `TRI_RUNTIME_PHYSICS_STRICT=1` (unless explicitly provided).
- Runtime strict thresholds:
  - `TRI_RUNTIME_PHYSICS_MAX_FLAGSHIP_LOST_EVENTS`
  - `TRI_RUNTIME_PHYSICS_MAX_FLAGSHIP_NON_RENDERABLE_EVENTS`

PlayMode physical probe gate (camera<->flagship drift + jitter signals):
- Enable probe capture only: `TRI_PLAYMODE_PHYSICAL_PROBE=1`
- Enable strict gate: `TRI_PLAYMODE_PHYSICAL_STRICT=1`
- Probe outputs are written under `reports/` as:
  - `pure_green_playmode_camera_probe_<build_id>.jsonl`
  - `pure_green_playmode_movement_probe_<build_id>.jsonl`
  - `pure_green_playmode_entity_visibility_probe_<build_id>.jsonl`
- Optional threshold overrides:
  - `TRI_PLAYMODE_PHYSICAL_MIN_CONTROLLED_SAMPLES`
  - `TRI_PLAYMODE_PHYSICAL_MIN_ENTITY_SAMPLES`
  - `TRI_PLAYMODE_PHYSICAL_MIN_ALIGNED_RATIO`
  - `TRI_PLAYMODE_PHYSICAL_MAX_SPIN_DEG_S`
  - `TRI_PLAYMODE_PHYSICAL_MAX_UNEXPECTED_JUMPS`
  - `TRI_PLAYMODE_PHYSICAL_MAX_PARITY_GAPS`
  - `TRI_PLAYMODE_PHYSICAL_MAX_ROGUE_ORBIT_HITS`
  - `TRI_PLAYMODE_PHYSICAL_MAX_LTW_GAP`
  - `TRI_PLAYMODE_PHYSICAL_MAX_FLAGSHIP_LOST_EVENTS`
  - `TRI_PLAYMODE_PHYSICAL_MAX_FLAGSHIP_NON_RENDERABLE_EVENTS`
  - `TRI_PLAYMODE_PHYSICAL_MAX_ENTITY_DROP_EVENTS`
  - `TRI_PLAYMODE_PHYSICAL_MAX_MATERIAL_MESH_DROP_FRACTION`

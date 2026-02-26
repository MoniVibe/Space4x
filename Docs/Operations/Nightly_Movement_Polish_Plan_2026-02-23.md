# Nightly Plan: Movement Polish + Camera Sanity (2026-02-23)

## Scope

Focus the nightly on Space4X movement feel and camera/flagship coherence.

Primary risks to burn down:
- Mode 1 fighter variant regressions.
- Camera/flagship jitter and parity gaps.
- Flagship disappearance during collision/docking flows.
- Silent entity visibility drops.

## Primary Goals (Must-Hit)

1. Keep flagship visible and controllable for the full smoke loop.
2. Keep Mode 1 variant ("fighter mode") functioning through mode cycles.
3. Reduce movement jitter signals to stable bounds in strict probes.
4. Block docking-related crash cascades from aborting scenario progression.

## Acceptance Gates

Use these as nightly pass criteria for movement-polish:

1. No docking ECB crash/exception signatures in run logs.
2. `runtime_visibility_flagship_lost_count == 0`.
3. `runtime_visibility_flagship_non_renderable_count == 0`.
4. `runtime_movement_diag_fail_count == 0`.
5. PlayMode physical probe:
   - aligned ratio stays above configured minimum.
   - parity gaps do not exceed configured maximum.
   - unexpected jumps do not exceed configured maximum.
6. Manual sanity spot-check at end:
   - Mode 1 default and Mode 1 variant both selectable.
   - no persistent camera drift from flagship center.

## Nightly Task Queue

Priority order for validator/operator loop:

1. Run fast strict lane on current branch tip.
2. If red, fix only the top movement/camera blocker and rerun fast strict lane.
3. Run movement-polish deck (smoke + turnrate + collision) for repeated short loops.
4. Triage failures into one of:
   - camera/follow parity,
   - movement dynamics,
   - docking/collision lifecycle,
   - visibility/render lifecycle.
5. Apply smallest safe fix and rerun only impacted lane first, then deck.
6. Stop when acceptance gates hold or stop conditions trigger.

## Stop Conditions

Stop and checkpoint if any condition occurs:

1. Same failure signature repeats twice with no metric improvement.
2. Five edit+rerun iterations without reduced fail counts.
3. Infra/queue/runner failure repeats twice.

## Secondary Priority (Only If Primary Is Green)

Implement these only after primary goals pass:

1. Add docking telemetry counters:
   - rejected docking for flagship-tagged entities,
   - docking playback exception count,
   - undocking playback exception count.
2. Add a validator parser check for docking exception signatures in `player.log`.
3. Reduce noisy editor telemetry spam (asteroid chunk logs) behind an env toggle.
4. Add a small PlayMode contract for mode cycling:
   - Mode 1 default -> Mode 1 variant -> Mode 2 -> Mode 3 -> Mode 1.

## Execution Commands

## A) Fast strict lane (first gate)

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\dev\Tri\Tools\HeadlessRebuildTool\Polish\Ops\validator_dispatch_buildbox.ps1 `
  -Project space4x `
  -PrNumber <PR_NUMBER> `
  -PrRepo MoniVibe/Space4x `
  -MovementFast
```

## B) Nightly movement deck (repeat loops)

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\dev\Tri\Tools\HeadlessRebuildTool\Polish\run_deck.ps1 `
  -DeckPath C:\dev\Tri\Tools\HeadlessRebuildTool\Polish\Decks\space4x_movement_polish_20260223.json `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
  -AllowLocalBuild `
  -Mode run
```

## C) Explicit strict smoke (non-PR branch sanity)

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\dev\Tri\Tools\HeadlessRebuildTool\scripts\trigger_buildbox.ps1 `
  -Title space4x `
  -Ref <SPACE4X_REF> `
  -ScenarioRel Assets/Scenarios/space4x_smoke.json `
  -TimeoutSec 180 `
  -PureGreen `
  -PureGreenPlayMode `
  -EnvJson '{"TRI_RUNTIME_HEALTH_STRICT":"1","TRI_RUNTIME_PHYSICS_STRICT":"1","TRI_PLAYMODE_PHYSICAL_PROBE":"1","TRI_PLAYMODE_PHYSICAL_STRICT":"1","SPACE4X_ENTITY_VISIBILITY_PROBE":"1"}' `
  -WaitForResult
```

## Reporting Template (Per Iteration)

Record after each run:

1. run id + scenario id.
2. top failure signature.
3. fail/warn metric deltas.
4. fix applied (one line).
5. rerun verdict.


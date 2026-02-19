# Space4X Presentation Nuisance Filter

Purpose: remove repeatable nuisance checks from the human loop so visual review time stays focused on feel and polish.

## Fastest Path (Headless Agent)

Run one command:

```powershell
pwsh -NoProfile -File .\scripts\presentation_mode1_headless.ps1
```

What it does:
- runs a dedicated PlayMode contract test for Mode 1 camera follow in Unity batch mode
- captures camera probe JSONL automatically
- runs the nuisance checker and returns a final JSON envelope (`pass|warn|fail|insufficient_evidence`)

Precondition:
- Unity Editor instances should be closed on that machine (or use an isolated runner). Use `-AllowRunningUnity` only when intentionally sharing that box.

## Tier Contract

### Tier 1 (hard fail, blocks visual pass)

- Simulation/run contract failed (`exit_code != 0`, failing invariants, incomplete ticks).
- Movement safety failed (NaN/Inf, stuck, teleport spikes).
- Camera ownership failed (follow target drift, excessive spin, large yaw mismatch).

Action:
- Stop. Fix Tier 1 before asking for a human visual pass.

### Tier 2 (warn, narrows visual pass)

- Movement drift warnings (heading oscillation, approach-flip churn, high overshoot/settle).
- Camera drift warnings (small but non-zero alignment drift, elevated spin/yaw delta).

Action:
- Continue, but only review warned areas. Do not do broad visual polish passes yet.

### Tier 3 (human-only)

- Feel/readability/drama.
- Input comfort and perceived responsiveness.
- Aesthetic coherence and polish.

Action:
- Human reviews only after Tier 1 is green and Tier 2 is understood.

## Agent Entry Point

Use:
- `scripts/presentation_mode1_headless.ps1` (recommended single-command headless Mode 1 check)
- `scripts/presentation_nuisance_filter.ps1`
- `scripts/presentation_mode1_check.ps1` (single-command wrapper focused on Mode 1 camera follow checks)

Inputs (any subset; more evidence = higher confidence):
- `run_summary.json`
- `invariants.json`
- movement metrics JSON (`space4x_movement_observe_metrics.json` or equivalent)
- camera probe JSONL (`SPACE4X_CAMERA_PROBE_OUT`)

Outputs:
- machine verdict JSON (`green`/`yellow`/`red`)
- concise markdown digest

Quick run example:

```powershell
pwsh -NoProfile -File .\scripts\presentation_nuisance_filter.ps1 `
  -RunSummaryPath <path-to-run_summary.json> `
  -InvariantsPath <path-to-invariants.json> `
  -MovementMetricsPath <path-to-space4x_movement_observe_metrics.json> `
  -CameraProbePath <path-to-space4x_camera_follow_probe.jsonl> `
  -OutJsonPath <path-to-verdict.json> `
  -OutMarkdownPath <path-to-verdict.md>
```

Mode 1 wrapper example:

```powershell
pwsh -NoProfile -File .\scripts\presentation_mode1_check.ps1
```

Mode 1 headless contract example:

```powershell
pwsh -NoProfile -File .\scripts\presentation_mode1_headless.ps1 `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\6000.3.1f1\Editor\Unity.exe"
```

Outputs include:
- `mode1_headless_envelope.json`
- `mode1_playmode_results.xml`
- `mode1_playmode_editor.log`
- `mode1_camera_probe.jsonl`

The wrapper auto-discovers latest probe/run/invariants paths and returns:
- `mode1_status`: `pass|warn|fail|insufficient_evidence`
- output JSON/markdown artifact paths

Unity editor dropdown:
- `Space4X/Diagnostics/Presentation Nuisance Filter`
- `Tools/Space4X/Presentation Nuisance Filter`
- Backed by: `Assets/Scripts/Space4x/Editor/Space4XPresentationNuisanceWindow.cs`

## Camera Probe Source

`Assets/Scripts/Space4x/Diagnostics/Space4XCameraFollowProbe.cs`

- Enable with environment variable:
  - `SPACE4X_CAMERA_PROBE=1`
- Optional output override:
  - `SPACE4X_CAMERA_PROBE_OUT=<path-to-jsonl>`
- Batch mode note:
  - probe now bootstraps in `-batchmode` when `SPACE4X_CAMERA_PROBE=1`
- Runtime toggle:
  - `F7`

## Threshold Tuning

Threshold file:
- `Docs/Presentation/Space4X_Presentation_Nuisance_Thresholds.json`

Tune this file first before editing script logic.

## Operating Rule

Agents should always run the nuisance filter before requesting a manual presentation pass.

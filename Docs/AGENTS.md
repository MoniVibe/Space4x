# Agent Quickstart (Space4X)

- Start here: `../TRI_PROJECT_BRIEFING.md` (canonical orientation).
- Smoke scene: `Assets/Scenes/TRI_Space4X_Smoke.unity` (wraps mining demo SubScene in `Assets/Scenes/Demo/Space4X_MiningDemo_SubScene.unity`).
- Avoid legacy: anything under `_Archive_Space4XLegacy/**` or `Assets/_Archive/**` is reference-only.
- Rendering: use Space4X render catalog asset (`Assets/Data/Space4XRenderCatalog.asset`) + PureDOTS.Resolve/Apply presenter systems; align with `Docs/Rendering/Space4X_RenderKey_TruthSource.md`.
- Frontend + runtime rendering contract (REQUIRED before UI/menu/camera edits): `Docs/Guides/Space4X_Frontend_Rendering_Contract.md`.
- Runtime ordering guardrails (REQUIRED before ECS boot/order changes): `Docs/Simulation/Space4X_Runtime_Ordering_Guardrails.md`.
- PureDOTS lifecycle truth-source (authoritative ordering baseline): `../../puredots/Docs/TruthSources/RuntimeLifecycle_TruthSource.md`.
- Presentation nuisance-filter contract (run before human visual pass): `Docs/Presentation/Space4X_Presentation_Nuisance_Filter.md`.
- Presentation implementation order (active): `Docs/Presentation/Space4X_Presentation_Slice_v1.md`.
- Where to add game code: `Assets/Scripts/Space4x/...` (presentation + gameplay), PureDOTS lives in package.
- Smoke bring-up: ensure TimeState/TickTimeState/RewindState + SpatialGridState + registry singletons exist in smoke SubScene; add a demo validation system to log counts and missing components.
- Headless verdict: check `out/run_summary_min.json` first, then `out/run_summary.json` + `meta.json`/logs for details.
- One-command Mode 1 headless contract check: `scripts/presentation_mode1_headless.ps1` (runs PlayMode test + probe + nuisance verdict). Prefer isolated runner / no open Unity editors.
- Nuisance filter script: `scripts/presentation_nuisance_filter.ps1` (produces `green/yellow/red` triage from run + camera evidence).
- Mode 1 wrapper: `scripts/presentation_mode1_check.ps1` (single prompt, auto-discovers latest evidence).
- Editor dropdown tool: `Space4X/Diagnostics/Presentation Nuisance Filter`.
- Editor dropdown alias: `Tools/Space4X/Presentation Nuisance Filter`.
- Nuisance thresholds: `Docs/Presentation/Space4X_Presentation_Nuisance_Thresholds.json` (tune thresholds here, not in script logic).
- Camera probe env: `SPACE4X_CAMERA_PROBE=1` and optional `SPACE4X_CAMERA_PROBE_OUT=<jsonl-path>`.
- Cameras: live in `Assets/Scripts/Space4x/Camera/`, drive motion with `Time.deltaTime`; do not add camera Monos to PureDOTS.
- Cross-OS split: WSL headless/logic uses `/home/oni/Tri` (ext4); Windows presentation uses `C:\dev\Tri`. Avoid `/mnt/c` for active WSL work.
- Ownership boundary: keep `Assets/` + `.meta` changes on Windows; keep PureDOTS/logic changes on WSL to avoid GUID/PPtr churn.
- Keep `Packages/manifest.json` and `Packages/packages-lock.json` synced across clones when logic changes so headless/editor compile against the same dependencies.
- WSL is case-sensitive; fix casing mismatches that Windows may tolerate.

Headless automation is driven by `Docs/Headless/*.md` and the ops bus protocol (`../../puredots/Docs/Headless/OPS_BUS_PROTOCOL.md`); do not invent ad-hoc comms or side channels.

Workflow (iterators + validator): `Docs/VALIDATOR_WORKFLOW.md`.

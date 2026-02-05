# Agent Quickstart (Space4X)

- Start here: `../TRI_PROJECT_BRIEFING.md` (canonical orientation).
- Smoke scene: `Assets/Scenes/TRI_Space4X_Smoke.unity` (wraps mining demo SubScene in `Assets/Scenes/Demo/Space4X_MiningDemo_SubScene.unity`).
- Avoid legacy: anything under `_Archive_Space4XLegacy/**` or `Assets/_Archive/**` is reference-only.
- Rendering: use Space4X render catalog asset (`Assets/Data/Space4XRenderCatalog.asset`) + PureDOTS.Resolve/Apply presenter systems; align with `Docs/Rendering/Space4X_RenderKey_TruthSource.md`.
- Where to add game code: `Assets/Scripts/Space4x/...` (presentation + gameplay), PureDOTS lives in package.
- Smoke bring-up: ensure TimeState/TickTimeState/RewindState + SpatialGridState + registry singletons exist in smoke SubScene; add a demo validation system to log counts and missing components.
- Headless verdict: check `out/run_summary_min.json` first, then `out/run_summary.json` + `meta.json`/logs for details.
- Cameras: live in `Assets/Scripts/Space4x/Camera/`, drive motion with `Time.deltaTime`; do not add camera Monos to PureDOTS.
- Cross-OS split: WSL headless/logic uses `/home/oni/Tri` (ext4); Windows presentation uses `C:\dev\Tri`. Avoid `/mnt/c` for active WSL work.
- Ownership boundary: keep `Assets/` + `.meta` changes on Windows; keep PureDOTS/logic changes on WSL to avoid GUID/PPtr churn.
- Keep `Packages/manifest.json` and `Packages/packages-lock.json` synced across clones when logic changes so headless/editor compile against the same dependencies.
- WSL is case-sensitive; fix casing mismatches that Windows may tolerate.

Headless automation is driven by `Docs/Headless/*.md` and the ops bus protocol (`../../puredots/Docs/Headless/OPS_BUS_PROTOCOL.md`); do not invent ad-hoc comms or side channels.

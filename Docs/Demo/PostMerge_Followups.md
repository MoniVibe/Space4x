# Post-Merge Followups

## Followup 1: Optional HUD Digest Visibility (after #70 merge)
- Goal: show determinism digest in HUD on demand when available.
- Files: `Assets/Scripts/Space4x/Camera/Space4XCameraHudOverlay.cs` (and only minimal related camera HUD wiring if needed).
- Avoid: simulation systems, headless question/validator files, combat logic.
- Validation scenario: `space4x_capital_20_vs_20_supergreen` then same-seed rerun.
- PASS rule: digest display can be toggled and shows non-zero value when combat totals are present; same-seed rerun displays same digest.

## Followup 2: Spectator Bridge Coverage Extension (after #71 merge)
- Goal: include any missing sim-backed entity type observed in `100v100` footage.
- Files: `Assets/Scripts/Space4x/Presentation/Space4XSpectatorRenderBridgeSystem.cs` and related bridge component file only if required.
- Avoid: spawning synthetic combat entities, camera/hud behavior changes, headless hotspots.
- Validation scenario: `space4x_capital_100_vs_100_proper`.
- PASS rule: new proxy coverage appears for the target entity type, proxies still map to real sim entities, and headless mode remains unaffected.

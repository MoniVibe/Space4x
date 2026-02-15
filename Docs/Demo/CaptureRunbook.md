# Space4x Capture Runbook

## Goal
Get repeatable, honest sim-truth footage for pitch capture using:
- `space4x_capital_20_vs_20_supergreen`
- `space4x_capital_100_vs_100_proper`

## Scene Setup
1. Open `TRI_Space4X_Smoke` (or your standard render scene for Space4x capture).
2. Create an empty GameObject named `SpectatorRenderBridgeConfig` (or similar).
3. Add component: `Space4XSpectatorRenderBridgeConfigAuthoring`.
4. Ensure:
   - `Enabled = true`
   - `OnlyCapitalBattleScenarios = true` (recommended)

## Scenario Order
Run in this order every time:
1. `space4x_capital_20_vs_20_supergreen` (stability pass, framing pass)
2. `space4x_capital_100_vs_100_proper` (hero footage pass)

## Camera + HUD Controls
From camera/HUD stack:
- `F`: focus selected ship/fleet
- `G`: toggle follow selected ship/fleet
- `C`: toggle cinematic take
- `H`: toggle HUD

## Suggested Capture Sequence
1. Start `space4x_capital_20_vs_20_supergreen`.
2. Let simulation settle for a few seconds.
3. Frame a readable engagement (`F` / `G` as needed).
4. Start cinematic path with `C`.
5. Capture 30-60 seconds.
6. Repeat on `space4x_capital_100_vs_100_proper`.

## Truth Markers To Keep Visible
Use HUD during at least one pass per scenario:
- tick/time
- alive counts (side0/side1)
- determinism digest (if available in that scenario path)

This keeps footage grounded in real sim state instead of pure cinematic overlay.

## Recorder Recommendation
Use Unity Recorder (or equivalent editor capture) with a stable preset:
- Resolution: `1920x1080` (baseline) or `2560x1440` (hero)
- Frame rate: `60 fps`
- Clip duration: `45-60s` per take
- Output path: `Temp/Reports/Capture/` or project-local capture folder under `Temp/`

Keep naming deterministic per take, for example:
- `space4x_capital_20v20_take01.mp4`
- `space4x_capital_100v100_take01.mp4`

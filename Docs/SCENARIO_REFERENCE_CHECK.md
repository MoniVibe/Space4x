# Scenario Reference Check

Use this when editor/runtime appears to load the wrong Space4X slice.

## Command

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\dev\Tri\space4x\Tools\ScenarioReferenceCheck.ps1
```

Optional (only inspect the most recent console window):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File C:\dev\Tri\space4x\Tools\ScenarioReferenceCheck.ps1 -TailLines 3000
```

## What It Verifies

- `space4x` and `puredots` branch/head and `origin/main` refs.
- Dirty working tree state on both repos.
- Active env routing:
  - `SPACE4X_MODE`
  - `SPACE4X_SCENARIO_PATH`
- Canonical scenario files existence:
  - `Assets/Scenarios/space4x_fleetcrawl_core_micro.json`
  - `Assets/Scenarios/space4x_smoke.json`
- Recent scenario-related and runtime-error lines from `C:\dev\Tri\console.md`.

## Expected FleetCrawl Baseline

- Mode should be `FleetCrawl`.
- Scenario id/path should point to `space4x_fleetcrawl_core_micro`.
- `console.md` should contain:
  - `[Space4XRunStart] ... space4x_fleetcrawl_core_micro ...`
  - `[Space4XRunStartScenarioSelector] Injected ScenarioInfo id='space4x_fleetcrawl_core_micro' ...`
  - `[Space4XMiningScenario] Loaded '...space4x_fleetcrawl_core_micro.json' ...`

If these do not line up, fix refs first, then rerun Unity.

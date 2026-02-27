# Space4X Capital Shooting Range Suite (v1)

## Purpose
- Provide deterministic mini firing-range scenarios for validating whether entities can acquire, track, and hit targets under varying crew quality, motion profiles, and loadouts.
- Produce a comparable scorecard in one batch run for armory iteration decisions.

## Suite Asset
- `Assets/Scenarios/space4x_capital_shooting_range_suite.v1.json`

Included cases:
- `space4x_capital_shooting_range_micro` (legacy control)
- `space4x_capital_shooting_range_static_rookie_micro`
- `space4x_capital_shooting_range_static_veteran_micro`
- `space4x_capital_shooting_range_static_elite_micro`
- `space4x_capital_shooting_range_crossing_experienced_micro`
- `space4x_capital_shooting_range_crossing_veteran_missile_micro`
- `space4x_capital_shooting_range_zigzag_rookie_micro`
- `space4x_capital_shooting_range_swarm_elite_micro`
- `space4x_capital_shooting_range_offset_veteran_micro`

## Crew Templates
- `Assets/Scenarios/Templates/crew.capital_range.rookie.v0.json`
- `Assets/Scenarios/Templates/crew.capital_range.experienced.v0.json`
- `Assets/Scenarios/Templates/crew.capital_range.veteran.v0.json`
- `Assets/Scenarios/Templates/crew.capital_range.elite.v0.json`

These templates are wired to the firing ship (`range-capital-alpha`) per scenario to expose officer-skill differences.

## Batch Execution
Run from CLI lane:

```powershell
Unity.exe -batchmode -nographics -projectPath C:\dev\Tri\space4x -executeMethod Space4X.Editor.Diagnostics.Space4XCapitalRangeBatchRunner.Run --suite Assets/Scenarios/space4x_capital_shooting_range_suite.v1.json --outDir Temp/capital_range_batch
```

Or use the orchestration script (recommended for long runs and stuck-exit handling):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/RunCapitalRangeSuite.ps1 -RepoPath C:\dev\Tri\space4x -SuitePath Assets/Scenarios/space4x_capital_shooting_range_suite.v1.json -OutDir Reports/capital_range_suite_v1 -ScenarioTimeoutSec 1800 -CompletionGraceSec 12
```

Runner outputs:
- `Temp/capital_range_batch/capital_range_result.json` (suite scorecard + ranking)
- `Temp/capital_range_batch/capital_range_result_<case>.json` (per-case snapshot)
- `Temp/capital_range_batch/capital_range_telemetry_<case>.ndjson`

Script outputs:
- `Reports/capital_range_suite_v1/capital_range_suite_result.json`
- `Reports/capital_range_suite_v1/capital_range_suite_result.md`
- Per-case subfolders with `unity.log`, `telemetry.ndjson`, `operator_report.json`, `headless_answers.json`

## Notes
- Capital range telemetry now accepts all scenario IDs matching `space4x_capital_shooting_range_*_micro` (plus legacy baseline id).
- Capital range score metrics are side-isolated to scenario side `0` (the evaluated ship lane) instead of both sides combined.
- Carrier speed and escort speed can be scenario-driven via `spawn.speed` and `Combat.interceptSpeed` for better motion coverage in tests.
- Suite entries can declare `gateRequired: false` to keep a scenario advisory (included in ranking, excluded from suite pass/fail gating). `baseline` uses this mode.
- Avoid writing suite outputs under project `Temp/` for multi-process runs; Unity may clean `Temp` between launches.

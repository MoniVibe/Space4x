# Space4X Nightly Weapons/Modules/Cohesion Suite (v1)

## Goal

Run a local overnight-style suite that validates:
- weapon usage quality across crew tiers,
- module pipeline/quality/provenance sanity,
- extraction loop behavior,
- movement/collision cohesion guards.

## Suite Config

- `Tools/ScenarioSuites/space4x_nightly_weapons_modules_cohesion_suite.v1.json`

## Runner

- `Tools/RunScenarioNightlySuite.ps1`

### Example command

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/RunScenarioNightlySuite.ps1 `
  -RepoPath C:\dev\Tri\space4x `
  -SuitePath Tools/ScenarioSuites/space4x_nightly_weapons_modules_cohesion_suite.v1.json `
  -OutDir Reports/nightly_weapons_modules_cohesion_v1 `
  -ScenarioTimeoutSec 900 `
  -CompletionGraceSec 20
```

## Artifacts

Per-case folder:
- `unity.log`
- `progress.json`
- `operator_report.json`
- `headless_answers.json`
- `telemetry.ndjson`

Suite summary:
- `scenario_nightly_suite_result.json`
- `scenario_nightly_suite_result.md`

## Notes

- Uses `Space4XCapitalRangeBatchRunner` as a generic host to drive headless scenario playmode runs and gather report artifacts.
- Case pass/fail is based on required headless questions in `operator_report.json` (not on gunnery-only metrics).
- `fleetcrawl_core` is currently advisory in this suite (`gateRequired=false`) to keep nightly visibility without hard fail while the mixed mining/combat loop is still being tuned.

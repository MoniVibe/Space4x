# Scenario Schemas

This folder exists to support `$schema` references inside `Assets/Scenarios/*.json` without forcing scenario edits.

## Current schema
- `scenario.schema.json`
  - Permissive schema supporting multiple known scenario shapes:
    - stress capital ships list (`ships` array)
    - mining-style scenarios (`scenarioConfig` + `spawn` + `actions`)
    - ScenarioRunner registry-count scenarios (`runTicks` + `entityCounts`)

## How scenarios reference this
Scenarios typically include:

```json
{
  "$schema": "schemas/scenario.schema.json",
  "scenarioId": "space4x_example",
  "...": "..."
}
```

The path is relative to `Assets/Scenarios/`.


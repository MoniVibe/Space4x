# Capital Battle Scenario Inventory

Single index for the Capital Battle suite: what exists, why it exists, and which features it is meant to exercise.

## Variants

| scenarioId | File | Scale Class | Intended Purpose | Features Exercised (Tags) |
|---|---|---:|---|---|
| `space4x_stress_capital_100_vs_100` | `Assets/Scenarios/space4x_stress_capital_100_vs_100.json` | 100v100 | Baseline stress + behavior probe | `perf`, `memory`, `targeting`, `strikecraft`, `collisions`, `telemetry` |
| `space4x_stress_capital_200_vs_200` | `Assets/Scenarios/space4x_stress_capital_200_vs_200.json` | 200v200 | Stress escalation + regression detector | `perf`, `memory`, `structural`, `strikecraft`, `telemetry` |
| `space4x_stress_capital_2k_vs_2k` | `Assets/Scenarios/space4x_stress_capital_2k_vs_2k.json` | 2kv2k | Scale wall / perf ceiling | `perf`, `memory`, `structural`, `dogfight`, `floating_origin`, `telemetry` |

## When To Use Which Variant
- `space4x_stress_capital_100_vs_100`: default local iteration + behavior probes; should run on most machines without extreme tuning.
- `space4x_stress_capital_200_vs_200`: catch perf regressions and structural churn cliffs earlier than 2k; use in CI/nightly more often than 2k.
- `space4x_stress_capital_2k_vs_2k`: scale wall and perf ceiling; use sparingly, and pair with a companion downscale variant for debugging.

## Ownership / Edit Rules
- Treat the `space4x_stress_capital_*` JSONs as owned artifacts. Add new variants rather than editing existing ones unless explicitly coordinated.
- Extend documentation first: update the goal card + addendum, then add or adjust scenarios.

## Links
- Goal card: `Docs/Scenarios/GoalCards/space4x_goalcard_capital_battle.md`
- Notes: `Docs/Scenarios/GoalCards/Addenda/space4x_capital_battle_notes.md`
- 2k notes: `Docs/Scenarios/capital_battle_2k_notes.md`

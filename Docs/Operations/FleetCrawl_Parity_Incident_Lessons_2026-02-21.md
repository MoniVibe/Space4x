# FleetCrawl Parity Incident Lessons (February 21, 2026)

This document captures the concrete lessons from the FleetCrawl parity ordeal so we do not repeat it.

## What Failed

- Scenario authority drifted across multiple entry points and fallbacks, so `New Game` loaded unexpected content.
- Branch mixing and blind merges reintroduced stale code paths (camera/movement jitter, drift, missing RTS behavior).
- Validator and editor were not always validating the same scenario path, causing conflicting truths.
- Package pins drifted to unsupported versions (`com.unity.entities/com.unity.collections 6.4.0`), causing buildbox failure before compile.
- Runtime effects (orbit/local frame/scale-side effects) leaked into FleetCrawl slice and broke movement/presentation expectations.

## Never-Again Rules

1. Single Scenario Authority
- `New Game` must resolve through one canonical path.
- Any fallback must be explicit, logged, and testable.
- No hidden secondary selector that can override canonical scenario at runtime.

2. Explicit Validator Scenario
- Buildbox dispatch for FleetCrawl must pass:
  - `-ScenarioRel Assets/Scenarios/space4x_fleetcrawl_survivors_v1.json`
- Never assume validator defaults to editor intent.

3. Package Pin Discipline
- Do not change DOTS package major lines without validator confirmation.
- `Packages/manifest.json` pins must remain validator-supported.
- If restore fails, fix package pins first before chasing compile/runtime errors.

4. No Blind Cross-Branch Merges
- Do not merge stale feature branches into active parity branch without diff triage.
- Prefer cherry-picking scoped commits for movement/camera/scenario wiring.
- When parity is unstable, freeze feature churn and restore baseline first.

5. Slice Isolation
- FleetCrawl runtime must gate out non-slice simulation effects by config/flag (not hardcoded hacks).
- Orbit/frame special effects must be opt-in for scenarios that need them.
- Movement behavior must remain module/profile driven, not mode-hardwired turn-rate overrides.

6. Startup Telemetry Is Mandatory
- On scenario load, log:
  - selected mode
  - selected scenario id/path
  - active runtime gates/flags (orbit, frame, fallback)
- If behavior mismatches expectation, telemetry is the first source of truth.

## Operational Checklist (Before Merge)

- Editor check:
  - `New Game` spawns expected FleetCrawl scenario.
  - Flagship spawns and remains camera-stable (no drift/jitter regression).
  - Modes `1/2/3` and known variants respond.
- Validator check:
  - run buildbox with explicit FleetCrawl `scenario_rel`.
  - confirm package restore, build, and run phases complete.
- Branch check:
  - ensure parity branch is based on current `main`.
  - ensure no stale branch replay has reintroduced old selectors/fallbacks.

## Fast Triage Order (When Regressions Reappear)

1. Confirm active scenario from startup logs.
2. Confirm validator run used the same `scenario_rel`.
3. Confirm package pins in `Packages/manifest.json`.
4. Confirm no stale selector/fallback path is active.
5. Confirm orbit/frame gates for FleetCrawl mode.
6. Only then debug movement/camera math.

## Ownership Split (To Reduce Drift)

- PureDOTS: reusable simulation contracts and deterministic behavior primitives.
- Space4x: FleetCrawl slice wiring, scenario selection, presentation, and mode-specific controls.
- Validator: source of truth for green mergeability.
- Editor runtime: source of truth for immediate UX feel, but must be reconciled with validator scenario parity.


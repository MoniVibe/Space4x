# Scenario Build Documentation

## Principles

All scenario builds follow these core principles:

### Data-First
Load everything from catalogs/scenarios; no scene-hardcoded logic. All demo actions are driven by the ScenarioRunner or hotkeys that map to time/rewind + spawn systems.

### Deterministic
Every demo action is driven by the ScenarioRunner or hotkeys that map to time/rewind + spawn systems. Determinism is validated across different frame rates (30/60/120Hz).

### Idempotent Tooling
Prefab Maker runs before a scenario build; bindings switch Minimal ↔ Fancy live without requiring rebuilds.

### Isolation
One build per game, gated by defines so they never compile each other's code.

## Build Defines

| Define | Purpose | Scope |
|--------|---------|-------|
| `SPACE4X_SCENARIO` | Space4X demo build | Space4X assemblies only |
| `GODGAME_SCENARIO` | Godgame demo build | Godgame assemblies only |
| `SPACE4X_TESTS` | Space4X test opt-in | Optional test assemblies |
| `GODGAME_TESTS` | Godgame test opt-in | Optional test assemblies |

**Note**: `PUREDOTS_AUTHORING` may be disabled for scenario builds unless authoring is embedded.

## Documentation Index

- **[BuildProfiles.md](BuildProfiles.md)** - Editor build profiles, CLI methods, scripting symbols
- **[ScenarioBootstrap.md](ScenarioBootstrap.md)** - Scenario bootstrap system (`DemoBootstrap`), DemoOptions, UI controls
- **[Space4X_DemoSpec.md](Space4X_DemoSpec.md)** - Space4X scenario slices, hotkeys, HUD, acceptance criteria
- **[Godgame_DemoSpec.md](Godgame_DemoSpec.md)** - Godgame scenario slices, hotkeys, HUD, acceptance criteria
- **[Scenarios.md](Scenarios.md)** - Scenario JSON schema, known-good scenarios, runner integration
- **[Instrumentation.md](Instrumentation.md)** - Reporter system, metrics, artifact storage
- **[Preflight.md](Preflight.md)** - Preflight validation pipeline, determinism checks
- **[Packaging.md](Packaging.md)** - Demo zip structure, launcher concept, CI artifacts
- **[Coplay_Prompt_DemoScene_Setup.md](Coplay_Prompt_DemoScene_Setup.md)** - Unity scene setup guide for Coplay

## Pre-Scenario Checklist

Before building a scenario build, run the preflight validation:

1. **Prefab Maker**: Run in Minimal set; validate; idempotency JSON written
2. **Scenarios**: Dry-run determinism (short versions) at 30/60/120Hz; log pass/fail
3. **Budgets**: Assert fixed_tick_ms ≤ target; snapshot ring within limits
4. **Bindings**: Swap Minimal↔Fancy once; assert no exceptions

See [Preflight.md](Preflight.md) for detailed validation steps.

## Quick Start

### Space4X Scenario Build
1. Set scripting symbols: `SPACE4X_SCENARIO`
2. Run Prefab Maker in Minimal mode
3. Load scenario via the scenario bootstrap (`DemoBootstrap`)
4. Press Play

### Godgame Scenario Build
1. Set scripting symbols: `GODGAME_SCENARIO`
2. Run Prefab Maker in Minimal mode
3. Load scenario via the scenario bootstrap (`DemoBootstrap`)
4. Press Play

## Scenario Talk Tracks

### Space4X (2-3 minutes)
"Here's the deterministic loop—input→sim→present. Modules are directional; aft volleys tag engines. Compliance fires on safe-zone shots. Crew changes heat, repair, aim. Same duel yields identical damage at different frame rates; Minimal/Fancy doesn't change outcomes."

### Godgame (2-3 minutes)
"Here's the deterministic loop—input→sim→present. Watch as we rewind the last 2 seconds and replay to the exact same counters. Construction consumes tickets, and we can swap visuals without touching code."

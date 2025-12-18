# Scenario Bootstrap System

## Overview

The scenario bootstrap system (`DemoBootstrap`) provides a single entry point for scenario builds. It handles scenario selection, time controls, presentation toggles, and determinism overlays without changing the underlying sim behavior.

## Scenario Options Component (`DemoOptions`)

Core configuration component loaded at boot:

```csharp
public struct DemoOptions : Unity.Entities.IComponentData
{
    public FixedString64Bytes ScenarioPath;
    public byte BindingsSet; // 0=Minimal, 1=Fancy
    public byte Veteran;     // 0/1
}
```

**Usage**:
- Loaded from UI or CLI at boot
- Read-only in Burst code
- Mutated only by small, non-Burst input handler
- ScenarioRunner and binding bootstrap read from it

## System Architecture

### Scenario Bootstrap Mono/ISystem (`DemoBootstrap`)

**Responsibilities**:
1. Load Scenario Select UI (or CLI arg) from `/Assets/Scenarios/<game>/*.json`
2. Expose time controls (Pause/Play/Step, Speed, Rewind)
3. Expose presentation toggles (Minimal/Fancy bindings, debug overlays)
4. Show determinism overlay (tick, rng seed, snapshot ring usage)
5. Start selected ScenarioRunner with seed + options

### Component Structure

```csharp
// Singleton component
public struct DemoBootstrapState : IComponentData
{
    public FixedString64Bytes SelectedScenario;
    public uint RngSeed;
    public byte BindingsSet; // 0=Minimal, 1=Fancy
    public byte VeteranProficiency; // 0/1
    public byte Paused; // 0/1
    public float TimeScale; // 0.5, 1.0, 2.0
    public byte RewindEnabled; // 0/1
}

// Options loaded from UI/CLI
public struct DemoOptions : IComponentData
{
    public FixedString64Bytes ScenarioPath;
    public byte BindingsSet;
    public byte Veteran;
}
```

## UI Controls

### Time Controls

| Control | Action | Hotkey |
|---------|--------|--------|
| Pause/Play | Toggle pause state | `P` |
| Step Back | Rewind one tick | `[` |
| Step Forward | Advance one tick | `]` |
| Speed ×0.5 | Slow motion | `1` |
| Speed ×1 | Normal speed | `2` |
| Speed ×2 | Fast forward | `3` |
| Rewind On/Off | Toggle rewind mode | `R` |

### Presentation Controls

| Control | Action | Hotkey |
|---------|--------|--------|
| Minimal/Fancy | Swap bindings | `B` |
| Debug Overlays | Show/hide debug info | `D` |

### Game-Specific Controls

**Space4X**:
- `J` - Toggle Jump/Flank planner
- `V` - Toggle veteran proficiency

**Godgame**:
- `G` - Spawn construction ghost
- `R` - Trigger rewind sequence (scripted path first, then demo freeform)

## Determinism Overlay

Display real-time information:

**Left HUD** (Game State):
- Game-specific metrics (villagers, damage totals, etc.)
- Active jobs/operations count
- Resource inventories
- Build progress

**Right HUD** (System Metrics):
- Current tick
- FPS
- fixed_tick_ms (fixed step duration)
- Snapshot bytes (ring buffer usage)
- ECB playback ms (if rewinding)
- RNG seed

## Scenario Selection

### UI Flow

1. On scene load, the scenario bootstrap (`DemoBootstrap`) scans `/Assets/Scenarios/<game>/*.json`
2. Displays list of available scenarios
3. User selects scenario (or uses default)
4. A `DemoOptions` component is created with the selected path
5. ScenarioRunner initialized with scenario + seed

### CLI Flow

```bash
-executeMethod Demos.Build.Run --game=Space4X --scenario=combat_duel_weapons.json
```

CLI args override UI selection.

## Binding Bootstrap

### Minimal ↔ Fancy Swap

1. Read `DemoOptions.BindingsSet` (0=Minimal, 1=Fancy)
2. Load corresponding binding asset:
   - Minimal: `Assets/Space4X/Bindings/Minimal.asset`
   - Fancy: `Assets/Space4X/Bindings/Fancy.asset`
3. Apply bindings to presentation system
4. Swap can occur live without rebuild

### Validation

After swap:
- Assert no exceptions
- Verify visuals change
- Verify metrics remain identical (determinism check)

### Render Sanity Ping (Space4X Scenario Build)

- `DemoRenderSanitySystem` emits a single `PlayEffectRequest` with effect id `FX.Demo.Ping` and lifetime `2s` when the Space4X scenario build boots.
- Presentation bridges that consume `PlayEffectRequest` buffers should render a small ping at the origin.
- If you do not see the ping, the presentation bridge/bindings are missing or misconfigured.

## Integration Points

### ScenarioRunner

```csharp
// Scenario bootstrap (DemoBootstrap) starts ScenarioRunner
var scenarioRunner = World.GetOrCreateSystemManaged<ScenarioRunner>();
scenarioRunner.Initialize(
    scenarioPath: demoOptions.ScenarioPath,
    seed: rngSeed,
    options: demoOptions
);
```

### Time Systems

```csharp
// Scenario bootstrap controls time state
var timeState = SystemAPI.GetSingletonRW<TimeState>();
timeState.ValueRW.Paused = demoBootstrapState.Paused;
timeState.ValueRW.TimeScale = demoBootstrapState.TimeScale;
```

### Rewind System

```csharp
// Scenario bootstrap controls rewind
var rewindState = SystemAPI.GetSingletonRW<RewindState>();
rewindState.ValueRW.Mode = demoBootstrapState.RewindEnabled == 1 
    ? RewindMode.Record 
    : RewindMode.Playback;
```

## Implementation Notes

- `DemoOptions` is read-only in Burst systems
- Only non-Burst input handler mutates `DemoOptions`
- ScenarioRunner reads `DemoOptions` once at initialization
- Binding bootstrap reads `DemoOptions` on swap request
- Determinism overlay reads singleton state every frame

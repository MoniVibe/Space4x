# Coplay Prompt: Create Space4X Demo Scene

## Objective
Create a Unity scene (`Assets/Scenes/Demos/Space4XDemo.unity`) that demonstrates the Space4X demo system with scenario selection, time controls, binding swaps, and telemetry reporting. This scene serves as the entry point for all demo builds.

## Context
- **Project**: Unity Space4x (DOTS/ECS-based)
- **Demo System**: New demo bootstrap system with IMGUI controls
- **Existing Systems**: All mining, combat, registry, and telemetry systems are already implemented
- **Reference**: `Space4XMiningDemoAuthoring` shows the pattern for scene setup

## Project Prerequisites (one time)

1. **URP asset available before DOTS bootstraps**
   - Create `Assets/Resources/Rendering/DemoURP.asset` (Assets → Create → Rendering → URP → Pipeline Asset).
   - The shared `EnsureSrpEarly` hook (`Assets/Scripts/Demo/EnsureSrpEarly.cs`) assigns this asset at `RuntimeInitializeLoadType.BeforeSplashScreen`, so Entities Graphics never falls back to “no SRP”.

2. **Use a compute-capable graphics API**
   - Player Settings → Other Settings → Graphics APIs:
     - Windows: keep Direct3D11 or Direct3D12 at the top (remove OpenGLCore for now).
     - macOS: use Metal.
   - Restart the Editor after changing the list so Entities Graphics picks up the new API.

3. **Clean up missing MonoBehaviours**
   - Menu: `Tools → Cleanup → Remove Missing Scripts In Scene/Project` (powered by `Assets/Editor/Tools/RemoveMissingScripts.cs`).
   - This silences the “Unknown script” spam before demo capture.

## Scene Requirements

### 1. Root GameObject Setup

Create a root GameObject (name: "Space4XDemo") with these components:

**Required Components:**
- `PureDotsConfigAuthoring` - Bootstraps PureDOTS runtime (creates TimeState, RewindState, GameplayFixedStep)
- `SpatialPartitionAuthoring` - Sets up spatial grid (use default settings)
- `Space4XDemoUI` - Demo control panel (MonoBehaviour, see below)

**Optional Components (for manual entity spawning):**
- `Space4XMiningDemoAuthoring` - If you want to spawn entities manually instead of via scenarios
- `Space4XCombatDemoAuthoring` - If you want combat entities

### 2. Space4XDemoUI Component Configuration

Add `Space4XDemoUI` component to the root GameObject:

**Component Settings:**
- **Toggle Key**: `F11` (default, can be changed in inspector)
- **No other configuration needed** - Component automatically:
  - Scans `Assets/Scenarios/` for JSON files
  - Connects to ECS world for state queries
  - Displays IMGUI panel when F11 is pressed

**How It Works:**
- Press `F11` to toggle the demo control panel
- Panel shows: Scenario selection, Time controls, Bindings, Metrics overlay
- All hotkeys work independently of the UI panel

### 3. Camera Setup

Ensure the scene has a Main Camera positioned to view the action:
- **Position**: `(0, 50, -100)` or similar elevated angle
- **Rotation**: Looking down at the action (e.g., `(30, 0, 0)`)
- **Field of View**: `60` (or adjust to see the full scene)
- **Tag**: `MainCamera`

**Editor/Demo Controller**:
- Add the `DemoCameraController` MonoBehaviour (from `Assets/Scripts/Space4x/Demo/`) to an empty GameObject in the scene
- Provides `WASD` movement, `QE` yaw, `RF` vertical movement, and scroll-wheel zoom
- Script is wrapped in `#if UNITY_EDITOR`, so it only runs in editor/demo builds

### 4. Lighting

Add a Directional Light (default is fine):
- **Rotation**: `(50, -30, 0)` for good lighting
- **Intensity**: `1.0`
- **Color**: White

### 5. Demo Systems (Automatic)

The following systems will run automatically when the scene loads (no setup needed):

**Bootstrap Systems:**
- `Space4XCoreSingletonGuardSystem` - Creates core singletons
- `Space4XTelemetryBootstrapSystem` - Creates TelemetryStream
- `Space4XDemoBootstrapSystem` (scenario bootstrap) - Creates DemoOptions and DemoBootstrapState singletons
- `Space4XDemoReporterSystem` - Initializes reporter state

**Runtime Systems:**
- `Space4XDemoHotkeySystem` - Handles P/J/B/V/R hotkeys
- All existing Space4X systems (mining, combat, telemetry, etc.)

## Scene Structure

```
Space4XDemo (GameObject)
├── PureDotsConfigAuthoring
├── SpatialPartitionAuthoring
└── Space4XDemoUI (MonoBehaviour)
```

**Separate GameObjects:**
- Main Camera
- Directional Light

## What Should Work

### On Scene Load

1. **ECS World Initialization**:
   - Core singletons created (TimeState, RewindState, TelemetryStream)
   - Scenario singletons created (DemoOptions, DemoBootstrapState, DemoReporterState)
   - All systems enabled and running

2. **Demo UI Available**:
   - Press `F11` to open demo control panel
   - Scenario list populated from `Assets/Scenarios/*.json`
   - Time controls functional
   - Metrics overlay shows system state

3. **Render Sanity Ping**:
   - `DemoRenderSanitySystem` emits an `FX.Demo.Ping` effect request at origin on startup
   - If you do not see the ping, your presentation bridge/bindings are missing or misconfigured

### Scenario Loading

1. **Via UI**:
   - Open demo panel (F11)
   - Click "Scenarios" tab
   - Select a scenario from the list
   - Scenario loads via ScenarioRunner (when integrated)

2. **Via Hotkeys**:
   - All hotkeys work immediately:
     - `P` - Pause/Play
     - `J` - Toggle Jump/Flank planner
     - `B` - Swap Minimal/Fancy bindings
     - `V` - Toggle veteran proficiency
     - `R` - Toggle rewind mode

### Time Controls

- **Pause/Play**: Toggles simulation pause state
- **Speed**: 0.5x, 1x, 2x (via UI buttons or hotkeys 1/2/3)
- **Rewind**: Toggles rewind mode (Record/Playback)
- **State Display**: Shows current tick, time scale, pause state, RNG seed

### Binding Swap

- **Minimal ↔ Fancy**: Swaps presentation bindings live
- **No Rebuild Required**: Bindings swap without scene reload
- **Metrics Preserved**: Visuals change but simulation metrics remain identical

### Telemetry & Reporting

- **Metrics Collection**: TelemetryStream collects metrics from all systems
- **Report Generation**: Reporter writes JSON/CSV to `Reports/Space4X/<scenario>/<timestamp>/`
- **Screenshots**: Captured at scenario start/end (when implemented)

## Validation Checklist

After creating the scene, verify in Play Mode:

- [ ] Scene loads without errors
- [ ] Press F11 opens demo control panel
- [ ] Scenario list shows JSON files from `Assets/Scenarios/`
- [ ] Time controls work (Pause/Play, Speed, Rewind)
- [ ] Hotkeys work (P, J, B, V, R)
- [ ] Binding swap works (B key or UI button)
- [ ] Metrics overlay shows tick, FPS, fixed_tick_ms
- [ ] Telemetry metrics display in UI
- [ ] Render sanity ping (`FX.Demo.Ping`) visible at origin on play
- [ ] No singleton errors in console
- [ ] Camera and lighting visible

## Integration with Scenarios

### Current State

The demo system is ready for scenario integration. Currently:
- `Space4XDemoUI` scans and lists scenarios
- `DemoOptions` component stores selected scenario path
- ScenarioRunner integration is a TODO (will be wired later)

### Future Integration

When ScenarioRunner is integrated:
1. User selects scenario in UI
2. `DemoOptions.ScenarioPath` is set
3. ScenarioRunner reads `DemoOptions` and loads the scenario
4. Entities spawn according to scenario JSON
5. Demo systems track metrics and generate reports

## Known Limitations

1. **Scenario Loading**: Currently placeholder - scenario selection updates `DemoOptions` but doesn't trigger ScenarioRunner yet
2. **Step Controls**: Step forward/back ([ and ]) are UI buttons but not yet implemented
3. **Screenshot Capture**: Reporter system has placeholder for screenshots
4. **Binding Assets**: Requires `Assets/Space4X/Bindings/Minimal.asset` and `Fancy.asset` to exist

## Troubleshooting

**Demo panel doesn't open:**
- Check that `Space4XDemoUI` component is on root GameObject
- Verify F11 key isn't bound elsewhere
- Check console for errors

**No scenarios listed:**
- Verify JSON files exist in `Assets/Scenarios/`
- Check file permissions
- Verify scenario files are valid JSON

**Hotkeys don't work:**
- Ensure scene is in Play Mode
- Check that ECS world is initialized
- Verify `Space4XDemoHotkeySystem` is running

**Metrics don't display:**
- Check that `TelemetryStream` singleton exists
- Verify telemetry systems are publishing metrics
- Check console for telemetry errors

**Binding swap doesn't work:**
- Verify binding assets exist: `Assets/Space4X/Bindings/Minimal.asset` and `Fancy.asset`
- Check that presentation system is running
- Verify `DemoOptions` singleton exists

## Next Steps

After scene setup:
1. Test all hotkeys and UI controls
2. Verify scenario list populates correctly
3. Test binding swap (ensure both Minimal and Fancy assets exist)
4. Integrate ScenarioRunner when ready
5. Add step forward/back implementation
6. Implement screenshot capture in reporter

## Reference Files

- Demo system: `Assets/Scripts/Space4x/Demo/`
- Demo UI: `Assets/Scripts/Space4x/Demo/Space4XDemoUI.cs`
- Scenario bootstrap documentation: `Docs/Archive/Demos/ScenarioBootstrap.md`
- Scenario docs: `Docs/Archive/Demos/Scenarios.md`
- Example authoring: `Assets/Scripts/Space4x/Authoring/Space4XMiningDemoAuthoring.cs`

---

**Ready to create?** Use the Unity Editor to:
1. Create new scene at `Assets/Scenes/Demos/Space4XDemo.unity`
2. Create root GameObject "Space4XDemo"
3. Add required components (PureDotsConfigAuthoring, SpatialPartitionAuthoring, Space4XDemoUI)
4. Add Camera and Light
5. Enter Play Mode and press F11 to test!

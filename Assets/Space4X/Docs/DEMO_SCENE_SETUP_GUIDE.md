# Demo Scene Setup Guide

**Status**: Setup Instructions  
**Target**: Demo_01 and Demo_02 Scene Creation  
**Last Updated**: 2025-12-01

---

## Overview

This guide provides step-by-step instructions for creating Demo_01 and Demo_02 scenes in the Unity Editor. These scenes integrate all Stage 3 presentation systems with proper authoring components and debug tooling.

---

## Demo_01 Scene Setup

### Step 1: Create Main Scene

1. Create new scene: `Assets/Space4X/Demos/Demo_01_Mining.unity`
2. Save scene

### Step 2: Add Camera

1. Ensure scene has Main Camera
2. Select Main Camera
3. Add Component: `Space4XCameraAuthoring` (from `Assets/Scripts/Space4x/Authoring/Space4XCameraAuthoring.cs`)
4. Configure camera authoring:
   - Set initial position/rotation
   - Configure camera profile (if using ScriptableObject)

### Step 3: Add Lighting

1. Ensure scene has Directional Light
2. Configure lighting as needed (default is fine)

### Step 4: Add Demo Configuration

1. Create empty GameObject: "Demo01Config"
2. Add Component: `Demo01Authoring` (from `Assets/Scripts/Space4x/Presentation/Demo01Authoring.cs`)
3. Configure Demo01Authoring:
   - Carrier Count: 4
   - Crafts Per Carrier: 4
   - Asteroid Count: 20
   - Spawn Area Size: 100
   - Faction Colors: Blue, Red, Green, Yellow
   - LOD Thresholds: FullDetail=100, ReducedDetail=500, Impostor=2000

### Step 5: Add Input Bridge

1. Create empty GameObject: "InputBridge"
2. Add Component: `Space4XSelectionInputBridge` (from `Assets/Scripts/Space4x/Presentation/Space4XSelectionInputBridge.cs`)
3. Assign Input Actions Asset: `Assets/InputSystem_Actions.inputactions`
4. Configure action map: "Player" (or as configured in input actions)

### Step 6: Add Debug Panel

1. Create empty GameObject: "DebugPanel"
2. Add Component: `Space4XDebugPanel` (from `Assets/Scripts/Space4x/Presentation/Space4XDebugPanel.cs`)
3. Configure debug panel:
   - Show Panel: true
   - Panel Position: (10, 10)
   - Panel Size: (300, 400)
   - Enable all display options

### Step 7: Add Metrics Logger (Optional)

1. Create empty GameObject: "MetricsLogger"
2. Add Component: `Space4XPresentationMetricsLogger` (from `Assets/Scripts/Space4x/Presentation/Space4XPresentationMetricsLogger.cs`)
3. Configure metrics logger:
   - Enable Logging: false (enable when needed)
   - Log File Path: "Logs/PresentationMetrics.csv"
   - Log Interval Frames: 60

### Step 8: Verify Systems

Ensure these systems are active (they should be automatically):
- `Space4XPresentationLifecycleSystem`
- `Space4XPresentationLODSystem`
- `Space4XRenderDensitySystem`
- `Space4XCarrierPresentationSystem`
- `Space4XCraftPresentationSystem`
- `Space4XAsteroidPresentationSystem`
- `Space4XSelectionSystem`
- `Space4XCommandBridgeSystem`
- All overlay systems

### Step 9: Test Scene

1. Enter Play mode
2. Verify carriers/crafts/asteroids spawn
3. Test camera controls (WASD, scroll)
4. Test selection (left-click)
5. Test commands (right-click)
6. Test overlays (O key)
7. Test debug panel (F1)

---

## Demo_02 Scene Setup

### Step 1: Create Main Scene

1. Create new scene: `Assets/Space4X/Demos/Demo_02_Combat.unity`
2. Save scene

### Step 2: Add Camera

1. Ensure scene has Main Camera
2. Select Main Camera
3. Add Component: `Space4XCameraAuthoring`
4. Configure camera authoring

### Step 3: Add Lighting

1. Ensure scene has Directional Light
2. Configure lighting

### Step 4: Add Scenario Loader

1. Create empty GameObject: "ScenarioLoader"
2. Add Component: `Space4XScenarioLoader` (from `Assets/Scripts/Space4x/Presentation/Space4XScenarioLoader.cs`)
3. Configure scenario loader:
   - Scenario Path: "Scenarios/demo_02_combat.json"
   - Load On Start: true
   - Scenario Name: "Demo_02 Combat"

### Step 5: Add Input Bridge

1. Create empty GameObject: "InputBridge"
2. Add Component: `Space4XSelectionInputBridge`
3. Assign Input Actions Asset
4. Configure action map

### Step 6: Add Debug Panel

1. Create empty GameObject: "DebugPanel"
2. Add Component: `Space4XDebugPanel`
3. Configure debug panel

### Step 7: Add Combat Test Harness (If Needed)

1. Create empty GameObject: "CombatTestHarness"
2. Add Component: `Space4XCombatStateTestHarnessController` (from `Assets/Scripts/Space4x/Presentation/Space4XCombatStateTestHarness.cs`)
3. Configure test harness:
   - Enable Test Harness: true (only if sim doesn't provide CombatState)
   - Damage Interval Seconds: 1.0

**Note**: Test harness only works if `SPACE4X_DEBUG_COMBAT_STATE` is defined in project settings.

### Step 8: Create Scenario JSON

1. Create file: `Assets/Scenarios/demo_02_combat.json`
2. Use schema from `DEMO_02_SCENARIO_SCHEMA.md`
3. Configure fleets, asteroid field, expectations

### Step 9: Verify Systems

Ensure these systems are active:
- All Demo_01 systems
- `Space4XCombatPresentationSystem`
- `Space4XProjectilePresentationSystem`
- `Space4XDamageFeedbackSystem`
- `Space4XCombatStateTestHarness` (if enabled)

### Step 10: Test Scene

1. Enter Play mode
2. Verify scenario loads
3. Verify fleets spawn
4. Wait for combat phase (30s)
5. Verify combat visuals
6. Test selection and commands
7. Check debug panel

---

## Common Issues

### Selection Not Working
- **Check**: Camera has `Space4XCameraAuthoring` component
- **Check**: `Space4XSelectionInputBridge` is present
- **Check**: Input actions asset is assigned
- **Check**: Action map name matches ("Player")

### Entities Not Rendering
- **Check**: Presentation systems are active
- **Check**: Entities have presentation components (added by lifecycle system)
- **Check**: LOD config exists (created by Demo01Authoring)
- **Check**: Camera is positioned correctly

### Combat Visuals Not Showing
- **Check**: `CombatState` component exists on entities
- **Check**: Enable test harness if sim doesn't provide CombatState
- **Check**: `Space4XCombatPresentationSystem` is active
- **Check**: Define `SPACE4X_DEBUG_COMBAT_STATE` for test harness

### Scenario Not Loading
- **Check**: Scenario JSON path is correct
- **Check**: PureDOTS ScenarioRunner is available
- **Check**: Console for loading errors
- **Check**: Scenario JSON schema is valid

### Debug Panel Not Showing
- **Check**: Press F1 to toggle
- **Check**: `Space4XDebugPanel` component is present
- **Check**: Show Panel is enabled
- **Check**: Panel position is on screen

---

## Quick Setup Checklist

**Demo_01:**
- [ ] Scene created
- [ ] Camera with Space4XCameraAuthoring
- [ ] Lighting configured
- [ ] Demo01Authoring component added and configured
- [ ] Space4XSelectionInputBridge added
- [ ] Space4XDebugPanel added
- [ ] Space4XPresentationMetricsLogger added (optional)
- [ ] Test in Play mode

**Demo_02:**
- [ ] Scene created
- [ ] Camera with Space4XCameraAuthoring
- [ ] Lighting configured
- [ ] Space4XScenarioLoader added and configured
- [ ] Scenario JSON file created
- [ ] Space4XSelectionInputBridge added
- [ ] Space4XDebugPanel added
- [ ] Space4XCombatStateTestHarnessController added (if needed)
- [ ] Test in Play mode

---

**End of Setup Guide**


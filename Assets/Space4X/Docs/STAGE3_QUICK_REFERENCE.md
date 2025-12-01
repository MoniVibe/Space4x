# Stage 3 Quick Reference

**Last Updated**: 2025-12-01

---

## Scene Files

- **Demo_01**: `Assets/Space4X/Demos/Demo_01_Mining.unity`
- **Demo_02**: `Assets/Space4X/Demos/Demo_02_Combat.unity`

## Key Controls

| Key | Action |
|-----|--------|
| **WASD** | Pan camera |
| **Mouse Scroll** | Zoom |
| **Left-Click** | Select entity |
| **Shift+Left-Click** | Multi-select |
| **Left-Click+Drag** | Box select |
| **Right-Click** | Issue command (move/mine/attack) |
| **O** | Toggle overlays (Resource → Faction → Routes → Off) |
| **F1** | Toggle debug panel |

## Required Components (Demo_01)

1. **Camera**: `Space4XCameraAuthoring`
2. **Demo Config**: `Demo01Authoring`
3. **Input Bridge**: `Space4XSelectionInputBridge`
4. **Debug Panel**: `Space4XDebugPanel`
5. **Metrics Logger** (optional): `Space4XPresentationMetricsLogger`

## Required Components (Demo_02)

1. **Camera**: `Space4XCameraAuthoring`
2. **Scenario Loader**: `Space4XScenarioLoader`
3. **Input Bridge**: `Space4XSelectionInputBridge`
4. **Debug Panel**: `Space4XDebugPanel`
5. **Combat Test Harness** (if needed): `Space4XCombatStateTestHarnessController`

## Selection Priority

1. Fleet Impostors (LOD = Impostor)
2. Carriers (LOD = FullDetail/ReducedDetail)
3. Crafts (LOD = FullDetail/ReducedDetail)
4. Asteroids (always selectable)

## Debug Panel Features

- **Scenario Name**: Current scenario (if loader present)
- **Entity Counts**: Total, visible, per-type
- **LOD Distribution**: FullDetail/ReducedDetail/Impostor/Hidden counts
- **Performance**: Frame time, render density
- **Selection**: Selected count, selection type
- **Overlays**: Toggle Resource/Faction/Logistics
- **Render Density**: Slider and step buttons
- **Metrics Logging**: Enable/disable CSV logging

## CombatState Contract

**Component**: `CombatState` (must be provided by sim systems)
- `IsInCombat` (bool)
- `TargetEntity` (Entity)
- `HealthRatio` (float 0-1)
- `ShieldRatio` (float 0-1)
- `LastDamageTick` (uint)
- `Phase` (CombatEngagementPhase)

**See**: `COMBAT_STATE_CONTRACT.md` for full details

## Scenario Schema

**Demo_02 JSON Schema**:
- `name`: Scenario identifier
- `fleets`: Array of fleet definitions
- `asteroid_field`: Asteroid field config
- `expectations`: Validation assertions

**See**: `DEMO_02_SCENARIO_SCHEMA.md` for full schema

## Troubleshooting

**Selection not working?**
- Check camera has `Space4XCameraAuthoring`
- Check `Space4XSelectionInputBridge` present
- Check input actions assigned

**Combat visuals not showing?**
- Check `CombatState` exists (or enable test harness)
- Check `Space4XCombatPresentationSystem` active
- Define `SPACE4X_DEBUG_COMBAT_STATE` for test harness

**Scenario not loading?**
- Check scenario JSON path
- Check PureDOTS ScenarioRunner available
- Check console for errors

**Debug panel not showing?**
- Press F1 to toggle
- Check `Space4XDebugPanel` component present
- Check Show Panel enabled

---

**End of Quick Reference**


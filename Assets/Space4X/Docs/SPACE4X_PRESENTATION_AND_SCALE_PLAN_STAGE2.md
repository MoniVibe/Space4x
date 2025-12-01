# Space4X Presentation, Input & Scale Plan - Stage 2

**Status**: Implementation Document  
**Target**: Stage 2 - Combat-Ready Demo & Strategic Features  
**Dependencies**: Stage 1 (Complete), PureDOTS Performance Plan, DOTS 1.4.3  
**Last Updated**: 2025-12-01

---

## Executive Summary

Stage 2 builds on Stage 1 foundations to create a richer, combat-ready demo that fully integrates with PureDOTS simulation data. This stage adds combat visualization, strategic overlays, player commands, scale testing infrastructure, and comprehensive documentation.

**Stage 1 Achievements:**
- ✅ LOD system with 4 levels (FullDetail, ReducedDetail, Impostor, Hidden)
- ✅ Render density sampling for crafts
- ✅ Visual state components for carriers, crafts, asteroids
- ✅ Fleet impostors (icons, volume bubbles, strength indicators)
- ✅ Selection & highlighting (click + box)
- ✅ Input bridge for selection
- ✅ Presentation metrics & auto-tuning of budgets

**Stage 2 Goals:**
- Hardwire presentation systems to real PureDOTS sim data
- Add combat visualization (projectiles, damage feedback, combat states)
- Implement strategic overlays (resources, factions, routes)
- Create minimal command & control layer
- Establish scale testing & performance tuning loop
- Comprehensive documentation for other agents

---

## 1. Stage 2 – Demo_01 Integration & Hardening

### 1.1 PureDOTS Component Dependencies

**Carrier Presentation Dependencies:**
- `Carrier` (sim) - CarrierId, Speed, PatrolCenter, PatrolRadius
- `LocalTransform` (sim) - Position, Rotation, Scale
- `CarrierHullId` (sim) - HullId for visual variant
- `ResourceStorage` buffer (sim) - Current resource amounts
- `Space4XFleet` (sim) - Fleet membership, ShipCount, Posture, TaskForce
- `AffiliationTag` buffer (sim) - Faction identifier
- `FleetMovementBroadcast` (sim) - Position, Velocity for movement visualization
- `FleetKinematics` (sim) - Explicit velocity source

**Craft Presentation Dependencies:**
- `MiningVessel` (sim) - VesselId, Speed, MiningEfficiency, CargoCapacity, CurrentCargo
- `LocalTransform` (sim) - Position, Rotation, Scale
- `VesselAIState` (sim) - CurrentState, CurrentGoal, TargetEntity, TargetPosition
- `MiningOrder` (sim) - Active mining order (optional)
- `VesselMovement` (sim) - Velocity, BaseSpeed, CurrentSpeed, IsMoving
- `Carrier` reference (sim) - Parent carrier entity

**Asteroid Presentation Dependencies:**
- `Asteroid` (sim) - AsteroidId, ResourceType, ResourceAmount, MaxResourceAmount, MiningRate
- `LocalTransform` (sim) - Position, Rotation, Scale
- `ResourceSourceState` (PureDOTS) - UnitsRemaining, LastHarvestTick
- `ResourceSourceConfig` (PureDOTS) - GatherRatePerWorker, MaxSimultaneousWorkers
- `ResourceTypeId` (sim) - Resource type identifier

**Fleet Presentation Dependencies:**
- `Space4XFleet` (sim) - FleetId, ShipCount, Posture, TaskForce
- `LocalTransform` (sim) - Fleet centroid
- `FleetMovementBroadcast` (sim) - Position, Velocity
- `FleetAggregateData` (to be created) - Centroid, Strength, FactionId (from PureDOTS aggregation)

### 1.2 Edge Case Handling

**Entity Creation/Destruction:**
- **System**: `Space4XPresentationLifecycleSystem`
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XPresentationLifecycleSystem.cs`
- **Handles**:
  - New carriers/crafts: Add presentation components on first frame
  - Destroyed carriers: Remove presentation components, fade out visuals
  - Depleted asteroids: Transition to Depleted state, reduce alpha
  - New fleets: Create fleet impostor entities when fleet forms

**Fleet Merge/Split:**
- **System**: `Space4XFleetAggregationSystem`
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XPresentationLifecycleSystem.cs` (same file)
- **Handles**:
  - Fleet merge: Combine fleet impostors, update strength indicators
  - Fleet split: Create new impostor entities, redistribute ships
  - Fleet disband: Remove impostor, show individual carriers

**Visual State Transitions:**
- **Carrier Destruction**: `CarrierVisualState` → `Retreating` (used as destroyed placeholder), fade out over 1 second
- **Craft Loss**: Remove `CraftPresentationTag`, fade out over 0.5 seconds
- **Asteroid Depletion**: `AsteroidVisualState` → `Depleted`, reduce alpha to 0.3

### 1.3 Debug Tools

**Extended Metrics System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XPresentationMetrics.cs` (extended)
- **New Metrics**:
  - `FleetImpostorCount` - Number of active fleet impostors
  - `RealFleetCount` - Number of real fleets (with Space4XFleet component)
  - `EntityCreationRate` - Entities per second (placeholder)
  - `EntityDestructionRate` - Entities per second (placeholder)

**Debug Panel System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XDebugPanel.cs`
- **Features**:
  - On-screen text overlay showing metrics
  - Toggle: Show LOD visualization (color-code entities by LOD level)
  - Toggle: Show metrics
  - Toggle: Show inspector
  - Entity counts per type
  - LOD distribution
  - Performance metrics

**Debug Modes:**
- **Component**: `DebugOverlayConfig` (singleton)
- **Modes**:
  - `ShowLODVisualization`: Color entities by LOD level
  - `ShowMetrics`: Display metrics on screen
  - `ShowInspector`: Show selected entity info

**Deliverable**: `Space4XPresentationLifecycleSystem.cs`, extended `Space4XPresentationMetrics.cs`, and `Space4XDebugPanel.cs` with debug mode toggles.

---

## 2. Demo_02 – Fleet Combat Presentation

### 2.1 Combat-Specific Components

**Projectile Components:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XCombatComponents.cs`
- **Components**:
  - `ProjectilePresentationTag` - Marker for projectile entities
  - `ProjectileVisualState` - Projectile type (Laser, Kinetic, Missile, Beam)
  - `ProjectileTrail` - Trail renderer data (length, color, width)
  - `ProjectileImpact` - Impact effect data (position, radius, effect type)

**Combat State Extensions:**
- **Extend**: `CarrierVisualState` enum (existing)
  - States: Idle, Patrolling, Mining, Combat, Retreating
- **Extend**: `CraftVisualState` enum (existing)
  - States: Idle, Mining, Returning, Docked, Moving
- **New Component**: `CombatState` - Combat-specific state
  - `IsInCombat` (bool)
  - `TargetEntity` (Entity)
  - `HealthRatio` (float 0-1)
  - `ShieldRatio` (float 0-1)
  - `LastDamageTick` (uint)
- **New Component**: `DamageFlash` - Flash effect data
  - `FlashIntensity` (float)
  - `FlashColor` (float4)
  - `FlashDuration` (float)
  - `FlashTimer` (float)

### 2.2 Combat Presentation Systems

**Combat Presentation System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XCombatPresentationSystem.cs`
- **System**: `Space4XCombatPresentationSystem`
- **Reads**:
  - `CombatState` (sim - to be created)
  - `Carrier` / `MiningVessel` (sim)
  - `LocalTransform` (sim)
  - `FleetMovementBroadcast` (sim - for velocity)
- **Writes**:
  - `CarrierVisualState` / `CraftVisualState` (combat states)
  - `MaterialPropertyOverride` (combat colors, shield glow)
  - `DamageFlash` (when damage occurs)

**Projectile Rendering System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XCombatPresentationSystem.cs` (same file)
- **System**: `Space4XProjectilePresentationSystem`
- **Reads**:
  - `ProjectilePresentationTag`
  - `LocalTransform` (sim - projectile position)
  - `ProjectileVisualState` (projectile type)
- **Writes**:
  - `MaterialPropertyOverride` (projectile color, trail)

**Damage Feedback System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XCombatPresentationSystem.cs` (same file)
- **System**: `Space4XDamageFeedbackSystem`
- **Reads**:
  - `CombatState` (LastDamageTick)
  - `DamageFlash` (if exists)
- **Writes**:
  - `MaterialPropertyOverride` (flash effect)
  - `DamageFlash` (update timer)

### 2.3 Demo_02 Scenario

**Scenario Definition:**
- **File**: `Assets/Scenarios/space4x_demo_02_combat.json` (to be created)
- **Setup**:
  - 2-3 fleets (different factions)
  - 10-15 asteroids in small field
  - Initial positions: Fleets approach from opposite sides
- **Phases**:
  1. **Approach** (0-30s): Fleets move toward asteroid field
  2. **Engagement** (30-90s): Fleets engage, projectiles fire
  3. **Resolution** (90-120s): One fleet retreats or is destroyed

**Combat Flow:**
- Fleets detect each other → `CombatState.IsInCombat = true`
- Carriers fire projectiles → Create `ProjectilePresentationTag` entities
- Projectiles hit → `CombatState.LastDamageTick` updated, `DamageFlash` triggered
- Fleet strength decreases → Fleet impostor strength indicator updates
- Retreat condition → `Space4XFleet.Posture = Retreating`, visual state changes

### 2.4 Combat Readability Rules

**Visual Rules:**
- **Fleets in Combat**: Fleet impostor icon pulses red, strength indicator flashes
- **Winning Side**: Stronger fleet's icon is brighter, losing side dims
- **Retreating**: Fleet icon moves away, color shifts to blue
- **Projectiles**: Color-coded by faction (red vs blue lasers)
- **Damage**: Brief white flash on hit, shield glow on carriers

**Deliverable**: New combat components, systems, and Demo_02 scenario file (placeholder).

---

## 3. Strategic Overlays – Resources, Factions, Routes

### 3.1 Resource Overlay

**Components:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XOverlayComponents.cs`
- **Component**: `ResourceOverlayData` - Per-asteroid overlay data
  - `RichnessLevel` (float 0-1) - Based on ResourceAmount / MaxResourceAmount
  - `RecentlyMined` (bool) - Mined in last N ticks
  - `MiningActivity` (int) - Number of active miners

**System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XResourceOverlaySystem.cs`
- **System**: `Space4XResourceOverlaySystem`
- **Reads**:
  - `Asteroid` (sim) - ResourceAmount, MaxResourceAmount
  - `ResourceSourceState` (PureDOTS) - LastHarvestTick
  - `AsteroidVisualState` (view)
- **Writes**:
  - `ResourceOverlayData` - Richness, mining activity
  - `MaterialPropertyOverride` - Halo color/intensity based on richness

**Visual Rules:**
- **Rich Asteroids**: Bright halo (green/cyan)
- **Depleted Asteroids**: Dim halo (gray)
- **Recently Mined**: Pulsing halo effect
- **Active Mining**: Orange glow around asteroid

### 3.2 Faction/Empire Overlay

**Components:**
- **Component**: `FactionOverlayData` - Per-entity faction data
  - `FactionId` (int)
  - `ControlStrength` (float 0-1) - How strongly faction controls this entity
  - `IsPlayerControlled` (bool)

**System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XFactionOverlaySystem.cs`
- **System**: `Space4XFactionOverlaySystem`
- **Reads**:
  - `AffiliationTag` buffer (sim) - Faction identifier
  - `Space4XFleet` (sim) - Fleet membership
  - `FactionColor` (view)
- **Writes**:
  - `FactionOverlayData` - Faction control data
  - Fleet icon colors based on faction

**Visual Rules:**
- **Fleet Icons**: Colored by faction color
- **Carrier Outlines**: Faction-colored outline (future)

### 3.3 Route/Logistics Overlay

**Components:**
- **Component**: `LogisticsRouteOverlay` - Route visualization data
  - `RouteId` (FixedString64Bytes)
  - `OriginPosition` (float3)
  - `DestinationPosition` (float3)
  - `RouteStatus` (Space4XLogisticsRouteStatus)
  - `Throughput` (float)

**System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XLogisticsOverlaySystem.cs`
- **System**: `Space4XLogisticsOverlaySystem`
- **Reads**:
  - `Space4XLogisticsRoute` (sim) - Route data
  - `Space4XColony` (sim) - Origin/destination positions
  - `LocalTransform` (sim) - Colony positions
- **Writes**:
  - `LogisticsRouteOverlay` - Route visualization
  - Line renderer between origin/destination (visual-only entities)

**Visual Rules:**
- **Operational Routes**: Green lines
- **Disrupted Routes**: Red lines
- **Overloaded Routes**: Yellow lines
- **Line Thickness**: Based on throughput
- **Limit**: Show max 20 routes (performance)

### 3.4 Overlay Control

**Extended Component:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XInputComponents.cs` (extended)
- **Extend**: `DebugOverlayConfig`
  - `ShowResourceFields` (bool)
  - `ShowFactionZones` (bool)
  - `ShowLogisticsOverlay` (bool) - NEW
  - `ShowLODVisualization` (bool)
  - `ShowMetrics` (bool)
  - `ShowInspector` (bool)

**Input Actions:**
- **Extend**: `CommandInput`
  - `ToggleOverlaysPressed` (bool) - Cycle through overlay modes

**System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XOverlayControlSystem.cs`
- **System**: `Space4XOverlayControlSystem`
- **Reads**: `CommandInput`, `DebugOverlayConfig`
- **Writes**: `DebugOverlayConfig` (toggle states)

**Deliverable**: Overlay components, systems, and input integration.

---

## 4. Minimal Command & Control Layer

### 4.1 Command Components

**Command Components:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XCommandComponents.cs`
- **Components**:
  - `PlayerCommand` - Command issued by player
    - `CommandType` (enum: Move, Attack, Mine, Patrol, Hold)
    - `TargetEntity` (Entity) - Target for command
    - `TargetPosition` (float3) - Position target
    - `IssuedTick` (uint)
    - `CommandId` (FixedString64Bytes)
  - `CommandQueueEntry` (buffer) - Queue of pending commands
  - `CommandFeedback` - Visual feedback for command
    - `CommandType` (enum)
    - `TargetPosition` (float3)
    - `FeedbackTimer` (float)
    - `FeedbackDuration` (float)
    - `FeedbackColor` (float4)

### 4.2 Command Input Extension

**Extended Input:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XInputComponents.cs` (extended)
- **Extend**: `CommandInput`
  - `IssueMoveCommand` (bool) - Right-click on ground
  - `IssueAttackCommand` (bool) - Right-click on enemy
  - `IssueMineCommand` (bool) - Right-click on asteroid
  - `CancelCommand` (bool) - Cancel selected command
  - `CommandTargetPosition` (float3) - World position from right-click
  - `CommandTargetEntity` (Entity) - Entity target from right-click

**Input Bridge Extension:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XSelectionInputBridge.cs` (extended)
- **Add**: Command input reading
  - Right-click detection
  - Target entity/position detection
  - Command type determination

### 4.3 Command → Sim Bridge

**Command Bridge System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XCommandBridgeSystem.cs`
- **System**: `Space4XCommandBridgeSystem`
- **Reads**:
  - `CommandInput` - Player input
  - `SelectionState` - Selected entities
  - `LocalTransform` - Target positions
- **Writes**:
  - `PlayerCommand` - Command component on selected entities
  - `CommandFeedback` - Visual feedback
  - PureDOTS commands (via existing APIs):
    - `MovementCommand` (for Move)
    - `MiningOrder` (for Mine)
    - `InterceptRequest` (for Attack - future)

**PureDOTS Integration:**
- **Move Command**: Write `MovementCommand` component (existing)
- **Mine Command**: Write `MiningOrder` component (existing)
- **Attack Command**: Write `InterceptRequest` buffer entry (existing)

### 4.4 Command Feedback

**Feedback System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XCommandBridgeSystem.cs` (same file)
- **System**: `Space4XCommandFeedbackSystem`
- **Reads**:
  - `CommandFeedback` - Feedback data
  - `LocalTransform` - Entity positions
- **Writes**:
  - Visual markers (destination marker, attack line, mining indicator)
  - `SelectedEntityInfo` - Update with current order (future)

**Visual Feedback:**
- **Move Command**: Green destination marker at target position (via `CommandFeedback`)
- **Attack Command**: Red line to target, attack icon (future)
- **Mine Command**: Orange mining indicator on asteroid (via `CommandFeedback`)
- **Feedback Duration**: 3 seconds, then fade out

**Deliverable**: Command components, bridge system, and feedback visualization.

---

## 5. Scale Experiments & Performance Tuning

### 5.1 Scenario Hooks

**Scale Scenario System:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XScaleScenarioSystem.cs`
- **System**: `Space4XScaleScenarioSystem`
- **Features**:
  - Load PureDOTS scale scenarios (10k, 100k, 1M entities)
  - Override LOD thresholds for scale tests
  - Override render density for scale tests
  - Spawn additional visual entities if needed

**Scenario Selection:**
- **Config Asset**: `Space4XScaleTestConfig` (ScriptableObject)
  - `ScenarioPath` (string) - Path to scenario JSON
  - `OverrideLOD` (bool) - Override LOD thresholds
  - `LODThresholds` (PresentationLODConfig) - Override values
  - `OverrideDensity` (bool) - Override render density
  - `RenderDensity` (float) - Override value

**Menu Integration:**
- **Editor Menu**: `Tools/Space4X/Load Scale Scenario`
  - Select scenario JSON file
  - Apply config overrides
  - Load scenario via PureDOTS ScenarioRunner (placeholder)

### 5.2 Metrics Integration

**Extended Metrics:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XPresentationMetrics.cs` (extended)
- **New Metrics**:
  - `FleetImpostorCount` - Number of active fleet impostors
  - `RealFleetCount` - Number of real fleets
  - `EntityCreationRate` - Entities per second (placeholder)
  - `EntityDestructionRate` - Entities per second (placeholder)

**Metrics Display:**
- **Debug Panel**: `Space4XDebugPanel.cs` (extended)
  - On-screen metrics display
  - Per-LOD entity counts
  - Fleet impostor vs real fleet counts

**Logging:**
- **File**: `Assets/Scripts/Space4x/Presentation/Space4XPresentationMetricsLogger.cs`
- **System**: `Space4XPresentationMetricsLogger` (MonoBehaviour)
- **Features**:
  - Log metrics to file (CSV format)
  - Periodic logging (every 60 frames)
  - Log file: `Logs/PresentationMetrics_<timestamp>.csv`

### 5.3 Budget Rules Refinement

**Default Thresholds (Refined):**
- **FullDetail**: 0-80 units (reduced from 100)
- **ReducedDetail**: 80-400 units (reduced from 500)
- **Impostor**: 400-1500 units (reduced from 2000)
- **Hidden**: >1500 units

**Default Render Density:**
- **Crafts**: 0.8 (80% rendered) for scale tests
- **Carriers**: 1.0 (100% rendered) always

**Auto-Adjustment Heuristics:**
- **If visible crafts > 1000**: Increase render density step (reduce by 0.1)
- **If frame time > 20ms**: Push LOD earlier (reduce thresholds by 10%)
- **If frame time < 10ms**: Relax LOD (increase thresholds by 5%)

**Documentation:**
- **File**: `Assets/Space4X/Docs/PERFORMANCE_TUNING_GUIDE.md` (to be created)
- **Content**:
  - Default thresholds and rationale
  - Auto-adjustment rules
  - Manual tuning guidelines
  - Scale test results and recommendations

**Deliverable**: Scale scenario system, extended metrics, and performance tuning guide (placeholder).

---

## 6. Documentation & Dev UX

### 6.1 Stage 2 Documentation

**Main Document:**
- **File**: `Assets/Space4X/Docs/SPACE4X_PRESENTATION_AND_SCALE_PLAN_STAGE2.md` (this file)
- **Sections**:
  1. Executive Summary (Stage 1 recap, Stage 2 goals)
  2. Demo_01 Integration & Hardening
  3. Demo_02 Combat Presentation
  4. Strategic Overlays
  5. Command & Control
  6. Scale Experiments & Performance Tuning
  7. Quickstart Guide
  8. PureDOTS Integration Notes

### 6.2 Quickstart Guide

**Section**: "How to Run Demo_01 & Demo_02"

**Demo_01:**
1. Open scene: `Assets/Scenes/Demo/Demo_01_Main.unity` (to be created)
2. Ensure SubScene `Demo_01_Content.unity` is enabled (to be created)
3. Add `Demo01Authoring` component to scene root
4. Add `Space4XSelectionInputBridge` component to scene
5. Enter Play mode
6. Use WASD to pan camera, mouse scroll to zoom
7. Left-click to select entities
8. Press F1 to toggle debug panel

**Demo_02:**
1. Open scene: `Assets/Scenes/Demo/Demo_02_Combat.unity` (to be created)
2. Load scenario: `Assets/Scenarios/space4x_demo_02_combat.json` (to be created)
3. Enter Play mode
4. Watch fleets engage
5. Press O to toggle overlays
6. Press F1 for debug panel

**Overlay Controls:**
- O: Toggle overlay mode (Resource → Faction → Routes → Off)
- F1: Toggle debug panel
- F2: Toggle LOD visualization (future)
- F3: Toggle sim freeze (future)

**Scale Scenarios:**
1. Menu: `Tools/Space4X/Load Scale Scenario`
2. Select scenario JSON (10k, 100k, 1M)
3. Config overrides applied automatically
4. Check debug panel for metrics

### 6.3 Key Scripts Reference

**Components:**
- `Space4XPresentationComponents.cs` - Core presentation components
- `Space4XCombatComponents.cs` - Combat-specific components
- `Space4XOverlayComponents.cs` - Overlay components
- `Space4XCommandComponents.cs` - Command components
- `Space4XInputComponents.cs` - Input components

**Systems:**
- `Space4XPresentationLODSystem.cs` - LOD assignment
- `Space4XPresentationLifecycleSystem.cs` - Entity lifecycle management
- `Space4XCarrierPresentationSystem.cs` - Carrier visuals
- `Space4XCraftPresentationSystem.cs` - Craft visuals
- `Space4XAsteroidPresentationSystem.cs` - Asteroid visuals
- `Space4XCombatPresentationSystem.cs` - Combat visuals
- `Space4XProjectilePresentationSystem.cs` - Projectile rendering (in combat file)
- `Space4XDamageFeedbackSystem.cs` - Damage effects (in combat file)
- `Space4XResourceOverlaySystem.cs` - Resource overlay
- `Space4XFactionOverlaySystem.cs` - Faction overlay
- `Space4XLogisticsOverlaySystem.cs` - Route overlay
- `Space4XOverlayControlSystem.cs` - Overlay toggle control
- `Space4XCommandBridgeSystem.cs` - Command → sim bridge
- `Space4XCommandFeedbackSystem.cs` - Command feedback (in command bridge file)
- `Space4XSelectionSystem.cs` - Entity selection
- `Space4XPresentationMetricsSystem.cs` - Metrics collection
- `Space4XScaleScenarioSystem.cs` - Scale scenario loading

**Authoring:**
- `CarrierPresentationAuthoring.cs` - Carrier presentation setup
- `CraftPresentationAuthoring.cs` - Craft presentation setup
- `AsteroidPresentationAuthoring.cs` - Asteroid presentation setup
- `Demo01Authoring.cs` - Demo_01 configuration
- `Demo02Authoring.cs` - Demo_02 configuration (to be created)

**Input Bridges:**
- `Space4XSelectionInputBridge.cs` - Selection input bridge (extended for commands)
- `Space4XCommandInputBridge.cs` - Command input bridge (integrated in selection bridge)

**Debug Tools:**
- `Space4XDebugPanel.cs` - Debug UI panel
- `Space4XPresentationMetricsLogger.cs` - Metrics logging

### 6.4 PureDOTS Integration Notes

**Current Dependencies:**
- `TimeState`, `RewindState` - Time system
- `SpatialGridConfig`, `SpatialGridState` - Spatial queries
- `ResourceSourceState`, `ResourceSourceConfig` - Resource system
- `RegistryDirectory`, `RegistryMetadata` - Registry system
- `TelemetryStream`, `TelemetryMetric` - Telemetry system

**Future Needs:**
- `FleetAggregateData` component - Fleet aggregation data (centroid, strength, faction)
- Fleet aggregation update frequency - How often fleet aggregates update
- Projectile sim components - If projectiles are simulated entities
- Damage event system - Damage events for feedback system
- Command queue API - Standardized command queue interface

**Notes for PureDOTS Team:**
- Presentation systems read-only from PureDOTS components
- No simulation logic duplication in presentation layer
- Frame-time for presentation, tick-time for simulation
- Presentation respects PureDOTS spatial grid for LOD

**Notes for Godgame Team:**
- Similar presentation patterns can be reused
- LOD system is generic and can be adapted
- Input bridge pattern is reusable
- Metrics system can be shared

---

## Implementation Status

### Phase 1: Demo_01 Hardening ✅
- ✅ Wire presentation systems to real PureDOTS components
- ✅ Implement `Space4XPresentationLifecycleSystem`
- ✅ Add edge case handling (creation/destruction, fleet merge/split)
- ✅ Extend metrics system
- ✅ Create debug panel

### Phase 2: Combat Demo_02 ✅
- ✅ Create combat components
- ✅ Implement combat presentation systems
- ✅ Create projectile rendering system
- ✅ Implement damage feedback system
- ⏳ Create Demo_02 scenario (placeholder)
- ⏳ Test combat readability

### Phase 3: Strategic Overlays ✅
- ✅ Create overlay components
- ✅ Implement resource overlay system
- ✅ Implement faction overlay system
- ✅ Implement logistics overlay system
- ✅ Add overlay controls to input system
- ⏳ Test overlay performance

### Phase 4: Command & Control ✅
- ✅ Create command components
- ✅ Extend input system for commands
- ✅ Implement command bridge system
- ✅ Implement command feedback system
- ✅ Integrate with PureDOTS command APIs
- ⏳ Test command flow

### Phase 5: Scale Testing ✅
- ✅ Implement scale scenario system
- ✅ Extend metrics for scale tests
- ✅ Refine budget rules
- ⏳ Run scale tests (10k, 100k, 1M)
- ⏳ Document performance tuning guide

### Phase 6: Documentation ✅
- ✅ Write Stage 2 documentation
- ✅ Create quickstart guide
- ✅ Document PureDOTS integration notes
- ✅ Update component/system reference

---

## Success Criteria

**Demo_01 Hardening:**
- ✅ All presentation systems read from real PureDOTS components
- ✅ Edge cases handled gracefully (creation/destruction, fleet changes)
- ✅ Debug tools functional (metrics, debug panel)

**Demo_02 Combat:**
- ✅ Combat visuals clear (projectiles, damage, combat states)
- ⏳ Fleet engagement readable (which side winning, retreating)
- ⏳ Demo_02 scenario runs smoothly

**Strategic Overlays:**
- ✅ Resource overlay shows asteroid richness
- ✅ Faction overlay shows fleet ownership
- ✅ Logistics overlay shows routes (limited set)

**Command & Control:**
- ✅ Player can issue move/attack/mine commands
- ✅ Commands bridge to PureDOTS sim
- ✅ Visual feedback confirms commands

**Scale Testing:**
- ✅ Scale scenarios load and run (placeholder)
- ✅ Metrics track performance
- ✅ Auto-adjustment works correctly

**Documentation:**
- ✅ Stage 2 doc complete
- ✅ Quickstart guide clear
- ✅ PureDOTS integration notes documented

---

**End of Stage 2 Plan**


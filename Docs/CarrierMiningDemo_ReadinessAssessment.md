# Carrier Mining Demo Readiness Assessment

**Date**: 2025-01-XX  
**Purpose**: Assess current state of carrier mining/combat demo systems and identify gaps before implementation

---

## 1. Scene + Authoring Baseline

### Current Demo Scenes
- `Assets/Scenes/Demo/DualMiningDemo.unity` - Uses `Space4XMiningDemoAuthoring`
- `Assets/Scenes/Demo/Space4X_MiningDemo_SubScene.unity` - Uses `Space4XMiningDemoAuthoring`
- `Assets/Scenes/Hybrid/Space4XShowcase_SubScene.unity` - Uses `Space4XMiningDemoAuthoring`

### Required Authoring Components
All demo scenes using `Space4XMiningDemoAuthoring` require:
- `PureDotsConfigAuthoring` (required component) - Bootstraps PureDOTS runtime config
- `SpatialPartitionAuthoring` (required component) - Sets up spatial grid for registry queries

### What Gets Baked by Space4XMiningDemoAuthoring
1. **Carriers** (`Carrier` component):
   - `Carrier`, `PatrolBehavior`, `MovementCommand`, `ResourceStorage` buffer
   - `LocalTransform`, `SpatialIndexedTag`
   - `AlignmentTriplet`, `RaceId`, `CultureId`, `EthicAxisValue` buffer, `OutlookEntry` buffer
   - `AffiliationTag` buffer, `AffiliationRelation` entity reference
   - `Space4XPresentationBinding` (if descriptor resolves)

2. **Mining Vessels** (`MiningVessel` component):
   - `MiningVessel`, `MiningOrder`, `MiningState`, `MiningYield`
   - `MiningJob` (legacy), `VesselAIState`, `VesselMovement`
   - `SpawnResourceRequest` buffer
   - `LocalTransform`, `SpatialIndexedTag`
   - Alignment/affiliation components (same as carriers)
   - `Space4XPresentationBinding` (if descriptor resolves)

3. **Asteroids** (`Asteroid` component):
   - `Asteroid`, `ResourceTypeId`, `ResourceSourceConfig`, `ResourceSourceState`
   - `LastRecordedTick`, `RewindableTag`, `HistoryTier`, `ResourceHistorySample` buffer
   - `LocalTransform`, `SpatialIndexedTag`
   - `Space4XPresentationBinding` (if descriptor resolves)

4. **Visual Config Singleton**:
   - `Space4XMiningVisualConfig` singleton entity with visual settings

### Required Bootstrap Singletons (Auto-created by Systems)
- `TimeState` - Created by `Space4XCoreSingletonGuardSystem` or `PureDotsConfigAuthoring`
- `RewindState` - Created by `Space4XCoreSingletonGuardSystem` or `PureDotsConfigAuthoring`
- `GameplayFixedStep` - Created by `Space4XCoreSingletonGuardSystem` or `PureDotsConfigAuthoring`
- `Space4XMiningTimeSpine` - Created by `Space4XMiningTimeSpineBootstrapSystem`
- `Space4XEffectRequestStream` - Created by `Space4XMinerMiningSystem.EnsureEffectStream()`
- `TelemetryStream` - Should be created by PureDOTS config or manually
- `ResourceRegistry` + `ResourceRegistryEntry` buffer - Created by `Space4XResourceRegistryBootstrapSystem`

### Missing Components for Combat Demo
Carriers baked by `Space4XMiningDemoAuthoring` currently **DO NOT** include:
- `Space4XFleet` - Required for registry bridge to see them as fleets
- `FleetMovementBroadcast` - Required for intercept systems to track them
- `InterceptCapability` - Required if carriers should intercept other fleets
- `VesselStanceComponent` - Required for stance-based AI behavior
- `FormationData` - Optional, for formation coordination
- `FleetKinematics` - Optional, for explicit velocity tracking

**Impact**: Carriers exist but are invisible to:
- `Space4xRegistryBridgeSystem` (won't appear in fleet registry)
- `Space4XFleetInterceptSystems` (can't intercept or be intercepted)
- Fleet coordination AI systems

---

## 2. Mining Loop Verification

### Runtime Pipeline Flow
1. **Mining Order Processing** (`Space4XMinerMiningSystem`):
   - Reads `MiningOrder`, `MiningState`, `MiningVessel`, `MiningYield`
   - Requires: `TimeState`, `GameplayFixedStep`, `RewindState`, `MiningOrder` component
   - Creates: `Space4XEffectRequestStream` singleton if missing
   - Outputs: Updates `MiningYield.PendingAmount`, emits `PlayEffectRequest` for mining sparks

2. **Yield Spawn Bridge** (`Space4XMiningYieldSpawnBridgeSystem`):
   - Reads: `MiningYield`, `MiningVessel`, `SpawnResourceRequest` buffer
   - Requires: `TimeState`, `RewindState`
   - Outputs: Adds `SpawnResourceRequest` entries when threshold reached

3. **Resource Spawn** (`MiningResourceSpawnSystem`):
   - Reads: `MiningVessel`, `SpawnResourceRequest` buffer, `MiningYield` (optional)
   - Requires: `TimeState`, `RewindState`
   - Outputs: Creates `SpawnResource` entities with `LocalTransform`

4. **Carrier Pickup** (`CarrierPickupSystem`):
   - Reads: `Carrier`, `ResourceStorage` buffer, `SpawnResource`, `LocalTransform`
   - Requires: `TimeState`, `RewindState`
   - Outputs: Transfers resources to carrier storage, updates `Space4XMiningTelemetry` singleton

5. **Telemetry** (`Space4XMiningTelemetrySystem`):
   - Reads: `Space4XMiningTelemetry` singleton
   - Requires: `TelemetryStream`
   - Outputs: Publishes `TelemetryMetric` entries to telemetry stream

6. **Time Spine Recording** (`Space4XMiningTimeSpineRecordSystem`):
   - Records: `MiningSnapshot`, `MiningTelemetrySnapshot`, `MiningCommandLogEntry`
   - Requires: `Space4XMiningTimeSpine` singleton

### Critical Movement Gap Identified

**Problem**: Vessels with `MiningOrder` component are **excluded** from movement systems:

1. `VesselMovementSystem` (line 69): `[WithNone(typeof(MiningOrder))]` - Skips entities with MiningOrder
2. `VesselTargetingSystem` (line 106): `[WithNone(typeof(MiningOrder))]` - Skips entities with MiningOrder  
3. `VesselAISystem` (line 146): `[WithNone(typeof(MiningOrder))]` - Skips entities with MiningOrder
4. `VesselGatheringSystem` (line 63): `[WithNone(typeof(MiningOrder))]` - Skips entities with MiningOrder
5. `VesselDepositSystem` (line 60): `[WithNone(typeof(MiningOrder))]` - Skips entities with MiningOrder

**Legacy System**: `MiningVesselSystem` in `Space4XDemoSystems.cs` handles movement for vessels with `MiningJob` (legacy component), but `Space4XMiningDemoAuthoring` bakes both `MiningJob` AND `MiningOrder`, creating a conflict.

**Current State**: 
- Vessels baked by `Space4XMiningDemoAuthoring` have `MiningOrder` → excluded from movement
- They also have `MiningJob` → could use legacy `MiningVesselSystem`, but that system queries `WithNone<MiningOrder>` (line 200), so it also skips them
- **Result**: Vessels spawn but never move to asteroids

**Solution Required**: 
- Option A: Remove `MiningOrder` from vessels that should use legacy movement (`MiningVesselSystem`)
- Option B: Add movement support to `Space4XMinerMiningSystem` or create a new movement system for `MiningOrder` vessels
- Option C: Update `VesselMovementSystem` and related systems to handle `MiningOrder` entities

### Test Coverage Status

**Existing Tests**:
- ✅ `Space4XMinerMiningSystemTests` - Covers mining tick, yield accumulation, effect emission, rewind guards
- ✅ `Space4XMiningTimeSpineTests` (PlayMode) - Covers spawn/pickup, telemetry, rewind restoration

**Missing Coverage**:
- ❌ Movement of vessels with `MiningOrder` to asteroids
- ❌ Vessel return to carrier after mining
- ❌ Registry continuity validation (open in `Docs/TODO/Phase2_Demo_TODO.md`)
- ❌ Rewind determinism for full mining→haul loop (partially covered, needs full cycle test)

---

## 3. Combat / Intercept Wiring

### Intercept System Stack

**Bootstrap** (`Space4XFleetInterceptBootstrapSystem`):
- Creates: `Space4XFleetInterceptQueue` singleton with `InterceptRequest` buffer, `FleetInterceptCommandLogEntry` buffer, `Space4XFleetInterceptTelemetry`

**Broadcast** (`FleetBroadcastSystem`):
- Reads: `FleetMovementBroadcast`, `LocalTransform`, `FleetKinematics` (optional), `SpatialGridResidency` (optional)
- Updates: `FleetMovementBroadcast.Position`, `FleetMovementBroadcast.Velocity`, `FleetMovementBroadcast.LastUpdateTick`

**Request Generation** (`FleetInterceptRequestSystem`):
- Reads: `InterceptCapability`, `FleetMovementBroadcast`, `LocalTransform`
- Outputs: Adds `InterceptRequest` entries to queue

**Pathfinding** (`InterceptPathfindingSystem`):
- Reads: `InterceptRequest` buffer, `InterceptCapability`, `FleetMovementBroadcast`
- Outputs: Sets `InterceptCourse` component on requester entities

**Rendezvous Coordination** (`RendezvousCoordinationSystem`):
- Reads: `InterceptCourse`, `FleetMovementBroadcast`
- Updates: `InterceptCourse.InterceptPoint` for rendezvous mode

**Telemetry** (`Space4XFleetInterceptTelemetrySystem`):
- Reads: `Space4XFleetInterceptTelemetry`
- Outputs: Publishes metrics to `TelemetryStream`

### Components Required for Carrier Combat Participation

To make carriers visible to intercept systems and registry bridge:

1. **For Registry Bridge** (`Space4xRegistryBridgeSystem`):
   ```csharp
   Space4XFleet fleet;           // Fleet identity
   LocalTransform transform;     // Position (already present)
   SpatialIndexedTag tag;        // Spatial indexing (already present)
   ```

2. **For Intercept Systems**:
   ```csharp
   FleetMovementBroadcast broadcast;  // Position/velocity broadcast
   FleetKinematics kinematics;        // Optional: explicit velocity
   SpatialGridResidency residency;    // Optional: spatial grid tracking
   ```

3. **For Active Interception** (if carrier should intercept others):
   ```csharp
   InterceptCapability capability;    // Max speed, tech tier, allow intercept flag
   InterceptCourse course;            // Set by intercept pathfinding system
   ```

4. **For Stance-Based AI**:
   ```csharp
   VesselStanceComponent stance;      // Aggressive/Defensive/Neutral/Evasive
   FormationData formation;           // Optional: formation coordination
   ```

### Authoring Hook Available

`Space4XFleetInterceptAuthoring` exists and can add:
- `FleetMovementBroadcast`
- `FleetKinematics` (if initial velocity provided)
- `SpatialIndexedTag`, `SpatialGridResidency`
- `InterceptCapability`, `InterceptCourse` (optional)

**Gap**: `Space4XMiningDemoAuthoring` does not use `Space4XFleetInterceptAuthoring` or add fleet components.

### Demo Setup Requirements

For a combat demo with two opposing carriers:

1. **Carrier 1** (Friendly):
   - Add `Space4XFleet` with `Posture = Space4XFleetPosture.Patrol`
   - Add `FleetMovementBroadcast` with `AllowsInterception = 1`
   - Add `VesselStanceComponent` with `CurrentStance = VesselStance.Neutral`
   - Optionally add `InterceptCapability` if it should intercept enemies

2. **Carrier 2** (Enemy):
   - Add `Space4XFleet` with `Posture = Space4XFleetPosture.Engaging`
   - Add `FleetMovementBroadcast` with `AllowsInterception = 1`
   - Add `VesselStanceComponent` with `CurrentStance = VesselStance.Aggressive`
   - Add `InterceptCapability` to enable interception

3. **Registry Visibility**:
   - Both carriers need `SpatialIndexedTag` (already present from `Space4XMiningDemoAuthoring`)
   - Registry bridge will automatically pick them up once `Space4XFleet` is added

---

## 4. Telemetry, Registry & Test Hooks

### Outstanding Acceptance Items (from Phase2_Demo_TODO.md)

1. **Registry Continuity Validation** (Open):
   - Need: Validation that mining/haul entities appear in registry before PlayMode
   - Current: `Space4XResourceRegistryBootstrapSystem` ensures `ResourceRegistry` exists, but asteroids need to be registered in `ResourceRegistryEntry` buffer
   - Gap: No system currently populates `ResourceRegistryEntry` buffer with asteroid data

2. **Rewind Determinism** (Partially Complete):
   - ✅ Mining tick respects rewind mode (`Space4XMinerMiningSystem` gates on `RewindMode.Record`)
   - ✅ Telemetry snapshot restore works (`Space4XMiningTimeSpinePlaybackSystem`)
   - ❌ Full cycle test: mine → spawn → pickup → rewind → verify state restoration

3. **Presentation-Driven** (Unknown):
   - Need: Verify presentation bindings work without hard UI references
   - Current: `Space4XPresentationBinding` components are baked, `Space4XPresentationAssignmentSystem` exists
   - Gap: Need to verify presentation registry is set up in demo scenes

### Telemetry Stream Setup

**Required**: `TelemetryStream` singleton with `TelemetryMetric` buffer

**Current State**:
- `Space4XMiningTelemetrySystem` requires `TelemetryStream` (line 20)
- `Space4XFleetInterceptTelemetrySystem` requires `TelemetryStream` (line 496)
- No automatic bootstrap found - must be created by `PureDotsConfigAuthoring` or manually

**Gap**: Demo scenes may be missing `TelemetryStream` singleton, causing telemetry systems to skip updates.

### Effect Request Stream Setup

**Status**: ✅ Auto-created by `Space4XMinerMiningSystem.EnsureEffectStream()` (line 389-410)
- Creates `Space4XEffectRequestStream` singleton with `PlayEffectRequest` buffer if missing

### Registry Bridge Requirements

**For Fleet Registry**:
- Entities need: `Space4XFleet`, `LocalTransform`, `SpatialIndexedTag`
- System: `Space4xRegistryBridgeSystem.UpdateFleetRegistry()`
- Output: `Space4XFleetRegistryEntry` buffer on `Space4XFleetRegistry` entity

**For Colony Registry**:
- Entities need: `Space4XColony`, `LocalTransform`, `SpatialIndexedTag`
- Not relevant for mining demo, but `Space4XSampleRegistryAuthoring` can create sample colonies

### Test Gaps to Address

1. **Movement Test**:
   - Create vessel with `MiningOrder`, verify it moves to asteroid
   - Verify vessel returns to carrier after cargo full
   - Test with both legacy `MiningJob` path and new `MiningOrder` path

2. **Combat/Intercept Test**:
   - Create two carriers with `Space4XFleet` and `FleetMovementBroadcast`
   - Verify intercept request generation
   - Verify intercept course calculation
   - Verify registry bridge picks up fleets

3. **Registry Continuity Test**:
   - Verify asteroids appear in `ResourceRegistryEntry` buffer
   - Verify carriers appear in `Space4XFleetRegistryEntry` buffer after adding `Space4XFleet`
   - Verify telemetry metrics are published

4. **Full Cycle Rewind Test**:
   - Mine asteroid → spawn pickup → carrier pickup → rewind → verify all state restored
   - Test with multiple vessels and carriers

---

## 5. Summary & Action Items

### Critical Gaps

1. **Movement Broken**: Vessels with `MiningOrder` cannot move (excluded from all movement systems)
2. **Combat Invisible**: Carriers lack `Space4XFleet` and `FleetMovementBroadcast`, so intercept systems can't see them
3. **Telemetry Stream**: May be missing in demo scenes (needs verification)
4. **Registry Continuity**: Asteroids may not be registered in `ResourceRegistryEntry` buffer

### Recommended Fixes

1. **Fix Movement**:
   - Update `VesselMovementSystem` to handle `MiningOrder` entities, OR
   - Create dedicated movement system for `MiningOrder` vessels, OR
   - Remove `MiningOrder` from vessels that should use legacy `MiningVesselSystem`

2. **Add Fleet Components**:
   - Extend `Space4XMiningDemoAuthoring` to optionally add `Space4XFleet` and `FleetMovementBroadcast`
   - Or create separate authoring component for combat-capable carriers

3. **Verify Telemetry**:
   - Ensure `TelemetryStream` singleton exists in demo scenes
   - Add bootstrap system if missing

4. **Register Asteroids**:
   - Create system to populate `ResourceRegistryEntry` buffer with asteroid data
   - Or extend asteroid authoring to register automatically

5. **Add Tests**:
   - Movement test for `MiningOrder` vessels
   - Combat/intercept integration test
   - Registry continuity validation test
   - Full cycle rewind determinism test

---

## Appendix: System Dependencies

### Mining Systems (FixedStep)
- `Space4XMinerMiningSystem` → Requires: `TimeState`, `GameplayFixedStep`, `RewindState`, `MiningOrder`
- `Space4XMiningYieldSpawnBridgeSystem` → Requires: `TimeState`, `RewindState`
- `MiningResourceSpawnSystem` → Requires: `TimeState`, `RewindState`, `MiningVessel`
- `CarrierPickupSystem` → Requires: `TimeState`, `RewindState`, `Carrier`

### Combat Systems (FixedStep)
- `FleetBroadcastSystem` → Requires: `TimeState`, `RewindState`
- `FleetInterceptRequestSystem` → Requires: `Space4XFleetInterceptQueue`, `TimeState`, `RewindState`
- `InterceptPathfindingSystem` → Requires: `Space4XFleetInterceptQueue`, `TimeState`, `RewindState`
- `RendezvousCoordinationSystem` → Requires: `TimeState`, `RewindState`

### Telemetry Systems (Presentation)
- `Space4XMiningTelemetrySystem` → Requires: `TelemetryStream`, `Space4XMiningTelemetry`
- `Space4XFleetInterceptTelemetrySystem` → Requires: `TelemetryStream`, `Space4XFleetInterceptQueue`

### Bootstrap Systems (Initialization)
- `Space4XCoreSingletonGuardSystem` → Creates: `TimeState`, `RewindState`, `GameplayFixedStep`
- `Space4XMiningTimeSpineBootstrapSystem` → Creates: `Space4XMiningTimeSpine` + buffers
- `Space4XFleetInterceptBootstrapSystem` → Creates: `Space4XFleetInterceptQueue` + buffers
- `Space4XResourceRegistryBootstrapSystem` → Ensures: `ResourceRegistry` + `ResourceRegistryEntry` buffer
- `ModuleCatalogBootstrapSystem` → Creates: Module/Hull catalogs if missing


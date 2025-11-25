# Tests & Telemetry Specification

## Overview

This document specifies the additional edit/play mode tests and telemetry checks needed to satisfy the open Phase 2 acceptance criteria and ensure the carrier mining/combat demo is production-ready.

---

## Outstanding Acceptance Items

### From Phase2_Demo_TODO.md

1. **Registry Continuity Validation** (Open)
   - Status: Not yet implemented
   - Requirement: Validate that mining/haul entities appear in registry before PlayMode
   - Current Gap: No system automatically populates `ResourceRegistryEntry` buffer with asteroid data

2. **Rewind Determinism** (Partially Complete)
   - Status: Basic rewind works, full cycle test missing
   - Requirement: Rewind during transfer; resource counts/state replay identically
   - Current Coverage: ✅ Mining tick respects rewind, ✅ Telemetry snapshot restore
   - Missing: Full cycle test (mine → spawn → pickup → rewind → verify)

3. **Presentation-Driven** (Unknown)
   - Status: Needs verification
   - Requirement: Presentation driven solely by Presentation spine bindings; removing bindings leaves the sim intact
   - Current: `Space4XPresentationBinding` components baked, `Space4XPresentationAssignmentSystem` exists
   - Gap: Need to verify presentation registry is set up in demo scenes

4. **PlayMode Tests** (Partially Complete)
   - Status: Some coverage exists
   - Requirement: PlayMode tests cover mining tick, carrier pickup, telemetry counter, and rewind determinism
   - Current: ✅ Mining tick covered, ✅ Carrier pickup covered, ✅ Rewind restoration covered
   - Missing: Full cycle test, registry continuity assertions

---

## Required Tests

### 1. Movement Integration Tests

#### Test: VesselMovesToAsteroidWithMiningOrder
**Purpose**: Verify vessels with `MiningOrder` can move to asteroids

**Setup**:
- Create asteroid at position (20, 0, 0)
- Create vessel with `MiningOrder` at position (0, 0, 0)
- Set `MiningOrder.TargetEntity` to asteroid
- Add `VesselAIState`, `VesselMovement` components

**Steps**:
1. Run `VesselAISystem` - should set `VesselAIState.TargetEntity`
2. Run `VesselTargetingSystem` - should set `VesselAIState.TargetPosition`
3. Run `VesselMovementSystem` - should move vessel toward asteroid
4. Advance multiple ticks
5. Verify vessel position approaches asteroid

**Expected**:
- Vessel moves toward asteroid
- `VesselAIState.CurrentState` transitions to `Mining` when close
- Vessel stops at asteroid (within gather distance)

**File**: `Assets/Scripts/Space4x/Tests/Space4XVesselMovementTests.cs` (new)

#### Test: VesselReturnsToCarrierAfterCargoFull
**Purpose**: Verify vessels return to carrier when cargo is full

**Setup**:
- Create carrier at position (0, 0, 0)
- Create vessel with `MiningVessel` (CurrentCargo = CargoCapacity * 0.95f)
- Set `VesselAIState.CurrentGoal = Returning`
- Set `VesselAIState.TargetEntity` to carrier

**Steps**:
1. Run `VesselTargetingSystem` - should resolve carrier position
2. Run `VesselMovementSystem` - should move toward carrier
3. Advance ticks until vessel reaches carrier
4. Run `VesselDepositSystem` - should deposit cargo

**Expected**:
- Vessel moves toward carrier
- Cargo deposited when within deposit distance
- `VesselAIState` transitions to `Idle` after deposit

**File**: `Assets/Scripts/Space4x/Tests/Space4XVesselMovementTests.cs` (new)

#### Test: MiningOrderAndLegacyMiningJobCoexist
**Purpose**: Verify both mining systems can coexist

**Setup**:
- Create two vessels: one with `MiningOrder`, one with `MiningJob` only
- Both target same asteroid

**Steps**:
1. Run both mining systems
2. Verify both vessels mine asteroid
3. Verify no conflicts or double-mining

**Expected**:
- Both systems work independently
- Resources correctly deducted from asteroid
- No duplicate mining

**File**: `Assets/Scripts/Space4x/Tests/Space4XMiningSystemIntegrationTests.cs` (new)

---

### 2. Combat/Intercept Integration Tests

#### Test: CarriersAppearInFleetRegistry
**Purpose**: Verify carriers with `Space4XFleet` appear in registry

**Setup**:
- Create carrier with `Space4XFleet`, `LocalTransform`, `SpatialIndexedTag`
- Set `Space4XFleet.Posture = Space4XFleetPosture.Patrol`

**Steps**:
1. Run `Space4xRegistryBridgeSystem`
2. Query `Space4XFleetRegistry` singleton
3. Query `Space4XFleetRegistryEntry` buffer

**Expected**:
- `Space4XFleetRegistry.FleetCount == 1`
- `Space4XFleetRegistryEntry` buffer contains entry for carrier
- Entry has correct `FleetId`, `Posture`, `ShipCount`

**File**: `Assets/Scripts/Space4x/Tests/Space4XFleetRegistryIntegrationTests.cs` (new)

#### Test: InterceptRequestGeneratedForNearbyFleets
**Purpose**: Verify intercept system generates requests for nearby fleets

**Setup**:
- Create carrier1 with `FleetMovementBroadcast` at (0, 0, 0)
- Create carrier2 with `FleetMovementBroadcast` at (10, 0, 0)
- Add `InterceptCapability` to carrier1

**Steps**:
1. Run `FleetBroadcastSystem` - updates positions
2. Run `FleetInterceptRequestSystem` - generates requests
3. Query `InterceptRequest` buffer on queue entity

**Expected**:
- `InterceptRequest` created with carrier1 as requester, carrier2 as target
- Request has correct priority and tick

**File**: `Assets/Scripts/Space4x/Tests/Space4XFleetInterceptIntegrationTests.cs` (new)

#### Test: InterceptCourseCalculatedAndApplied
**Purpose**: Verify intercept pathfinding calculates and applies courses

**Setup**:
- Create target carrier with `FleetMovementBroadcast` moving at velocity (1, 0, 0)
- Create requester carrier with `InterceptCapability` (MaxSpeed = 5) at (-10, 0, 0)
- Add `InterceptRequest` to queue

**Steps**:
1. Run `InterceptPathfindingSystem`
2. Query `InterceptCourse` component on requester

**Expected**:
- `InterceptCourse` component added to requester
- `InterceptCourse.TargetFleet` == target carrier
- `InterceptCourse.UsesInterception == 1` (if tech allows)
- `InterceptCourse.InterceptPoint` calculated correctly
- `InterceptCourse.EstimatedInterceptTick` > 0

**File**: `Assets/Scripts/Space4x/Tests/Space4XFleetInterceptIntegrationTests.cs` (new)

#### Test: FleetTelemetryPublished
**Purpose**: Verify fleet intercept telemetry is published

**Setup**:
- Create `TelemetryStream` singleton
- Create `Space4XFleetInterceptQueue` with telemetry
- Generate intercept requests and process them

**Steps**:
1. Run intercept systems to generate telemetry
2. Run `Space4XFleetInterceptTelemetrySystem`
3. Query `TelemetryMetric` buffer on `TelemetryStream`

**Expected**:
- `TelemetryMetric` entries for:
  - `space4x.intercept.attempts`
  - `space4x.intercept.rendezvous`
  - `space4x.intercept.lastTick`

**File**: `Assets/Scripts/Space4x/Tests/Space4XFleetInterceptIntegrationTests.cs` (new)

---

### 3. Registry Continuity Tests

#### Test: AsteroidsRegisteredInResourceRegistry
**Purpose**: Verify asteroids appear in `ResourceRegistryEntry` buffer

**Setup**:
- Create asteroid with `Asteroid`, `ResourceSourceState`, `ResourceTypeId`, `LocalTransform`
- Ensure `ResourceRegistry` singleton exists with `ResourceRegistryEntry` buffer

**Steps**:
1. Run system that populates `ResourceRegistryEntry` buffer (needs to be created)
2. Query `ResourceRegistryEntry` buffer
3. Verify asteroid entry exists

**Expected**:
- `ResourceRegistryEntry` buffer contains entry for asteroid
- Entry has correct `SourceEntity`, `Position`, `ResourceTypeIndex`, `Tier = Raw`

**File**: `Assets/Scripts/Space4x/Tests/Space4XResourceRegistryIntegrationTests.cs` (new)

**Note**: This test requires a system to populate `ResourceRegistryEntry` buffer. Currently missing.

#### Test: CarriersRegisteredInFleetRegistryAfterAddingFleetComponent
**Purpose**: Verify carriers appear in fleet registry after adding `Space4XFleet`

**Setup**:
- Create carrier with `Carrier`, `LocalTransform`, `SpatialIndexedTag` (no `Space4XFleet`)
- Run registry bridge (should not see carrier)
- Add `Space4XFleet` component

**Steps**:
1. Run `Space4xRegistryBridgeSystem` before adding `Space4XFleet`
2. Verify carrier NOT in registry
3. Add `Space4XFleet` component
4. Run `Space4xRegistryBridgeSystem` again
5. Verify carrier NOW in registry

**Expected**:
- Before: `Space4XFleetRegistry.FleetCount == 0`
- After: `Space4XFleetRegistry.FleetCount == 1`
- Registry entry has correct fleet data

**File**: `Assets/Scripts/Space4x/Tests/Space4XFleetRegistryIntegrationTests.cs` (new)

#### Test: SpatialIndexingWorksForFleets
**Purpose**: Verify spatial indexing correctly maps fleet positions

**Setup**:
- Create carrier with `Space4XFleet`, `LocalTransform`, `SpatialIndexedTag`
- Set position to known coordinates
- Ensure spatial grid is configured

**Steps**:
1. Run `Space4xRegistryBridgeSystem`
2. Query `Space4XFleetRegistryEntry` for carrier
3. Verify `CellId` is set correctly
4. Verify `SpatialVersion` matches grid state

**Expected**:
- `CellId` is valid (>= 0, < grid.CellCount)
- `SpatialVersion` matches `SpatialGridState.Version`
- Position matches `WorldPosition` in registry entry

**File**: `Assets/Scripts/Space4x/Tests/Space4XFleetRegistryIntegrationTests.cs` (new)

---

### 4. Full Cycle Rewind Tests

#### Test: FullMiningCycleRewindDeterminism
**Purpose**: Verify complete mining→haul cycle can be rewound deterministically

**Setup**:
- Create asteroid with 100 units
- Create vessel with `MiningOrder`, `MiningVessel` (CargoCapacity = 50)
- Create carrier with `ResourceStorage` buffer
- Position vessel near asteroid, carrier nearby

**Steps**:
1. Record initial state (tick 0)
2. Advance to tick 10 (vessel mines asteroid, spawns pickup)
3. Advance to tick 20 (carrier picks up spawn)
4. Record state at tick 20 (ore in carrier storage)
5. Rewind to tick 10
6. Verify asteroid state restored
7. Verify vessel cargo restored
8. Verify carrier storage restored
9. Catch up to tick 20
10. Verify final state matches original tick 20 state

**Expected**:
- All state restored correctly at tick 10
- Final state at tick 20 matches original
- No duplicate resources or missing state

**File**: `Assets/Scripts/Space4x/Tests/PlayMode/Space4XMiningFullCycleRewindTests.cs` (new)

#### Test: MultipleVesselsRewindDeterminism
**Purpose**: Verify rewind works with multiple vessels and carriers

**Setup**:
- Create 2 asteroids
- Create 2 vessels (one per asteroid)
- Create 2 carriers
- Set up mining→haul cycle for both

**Steps**:
1. Record state at tick 0
2. Advance to tick 30 (both vessels mine, both carriers pick up)
3. Record state at tick 30
4. Rewind to tick 15
5. Verify all entities restored correctly
6. Catch up to tick 30
7. Verify final state matches original

**Expected**:
- All vessels, carriers, asteroids restored correctly
- No cross-contamination between vessels/carriers
- Final state deterministic

**File**: `Assets/Scripts/Space4x/Tests/PlayMode/Space4XMiningFullCycleRewindTests.cs` (new)

---

### 5. Telemetry Stream Tests

#### Test: TelemetryStreamExistsInDemoScenes
**Purpose**: Verify `TelemetryStream` singleton exists

**Setup**:
- Load demo scene
- Query for `TelemetryStream` singleton

**Steps**:
1. Check if `TelemetryStream` entity exists
2. Verify `TelemetryMetric` buffer exists on entity

**Expected**:
- `TelemetryStream` singleton exists
- Buffer is accessible

**File**: `Assets/Scripts/Space4x/Tests/Space4XTelemetryBootstrapTests.cs` (new)

#### Test: MiningTelemetryMetricsPublished
**Purpose**: Verify mining telemetry metrics are published to stream

**Setup**:
- Create `TelemetryStream` singleton
- Create `Space4XMiningTelemetry` singleton
- Set `OreInHold = 100f`
- Run mining systems to update telemetry

**Steps**:
1. Run `CarrierPickupSystem` (updates telemetry)
2. Run `Space4XMiningTelemetrySystem`
3. Query `TelemetryMetric` buffer

**Expected**:
- `TelemetryMetric` entries for:
  - `space4x.mining.oreInHold` = 100f
  - `space4x.mining.oreInHold.lastTick` = current tick

**File**: `Assets/Scripts/Space4x/Tests/Space4XTelemetryBootstrapTests.cs` (new)

#### Test: InterceptTelemetryMetricsPublished
**Purpose**: Verify intercept telemetry metrics are published

**Setup**:
- Create `TelemetryStream` singleton
- Create `Space4XFleetInterceptQueue` with telemetry
- Generate intercept requests

**Steps**:
1. Run intercept systems
2. Run `Space4XFleetInterceptTelemetrySystem`
3. Query `TelemetryMetric` buffer

**Expected**:
- `TelemetryMetric` entries for:
  - `space4x.intercept.attempts`
  - `space4x.intercept.rendezvous`
  - `space4x.intercept.lastTick`

**File**: `Assets/Scripts/Space4x/Tests/Space4XTelemetryBootstrapTests.cs` (new)

#### Test: TelemetryAccessibleWithoutUI
**Purpose**: Verify telemetry can be read without UI dependencies

**Setup**:
- Create `TelemetryStream` singleton
- Publish various metrics

**Steps**:
1. Query `TelemetryMetric` buffer directly
2. Read metric values without UI code
3. Verify no managed references required

**Expected**:
- Metrics accessible via ECS queries
- No GameObject or UI dependencies
- Metrics readable from pure DOTS code

**File**: `Assets/Scripts/Space4x/Tests/Space4XTelemetryBootstrapTests.cs` (new)

---

### 6. Presentation System Tests

#### Test: PresentationBindingsWorkWithoutUI
**Purpose**: Verify presentation bindings function without UI dependencies

**Setup**:
- Create entity with `Space4XPresentationBinding`
- Ensure `PresentationCommandQueue` exists
- Ensure presentation registry has descriptor

**Steps**:
1. Run `Space4XPresentationAssignmentSystem`
2. Query `PresentationSpawnRequest` buffer
3. Verify spawn request created

**Expected**:
- `PresentationSpawnRequest` created for entity
- Request has correct descriptor, position, scale
- No UI code required

**File**: `Assets/Scripts/Space4x/Tests/Space4XPresentationIntegrationTests.cs` (new)

#### Test: RemovingBindingsLeavesSimIntact
**Purpose**: Verify simulation continues when presentation bindings removed

**Setup**:
- Create mining demo with vessels, carriers, asteroids
- All have `Space4XPresentationBinding` components
- Run simulation for several ticks

**Steps**:
1. Record simulation state
2. Remove all `Space4XPresentationBinding` components
3. Continue simulation
4. Verify mining/haul logic still works

**Expected**:
- Simulation continues normally
- Mining, spawning, pickup all work
- Only visual representation missing

**File**: `Assets/Scripts/Space4x/Tests/Space4XPresentationIntegrationTests.cs` (new)

---

## Test Implementation Checklist

### New Test Files to Create

1. **`Assets/Scripts/Space4x/Tests/Space4XVesselMovementTests.cs`**
   - VesselMovesToAsteroidWithMiningOrder
   - VesselReturnsToCarrierAfterCargoFull
   - VesselStopsAtAsteroidWhenClose

2. **`Assets/Scripts/Space4x/Tests/Space4XMiningSystemIntegrationTests.cs`**
   - MiningOrderAndLegacyMiningJobCoexist
   - MultipleVesselsMineSameAsteroid
   - VesselSwitchesAsteroidWhenDepleted

3. **`Assets/Scripts/Space4x/Tests/Space4XFleetRegistryIntegrationTests.cs`**
   - CarriersAppearInFleetRegistry
   - CarriersRegisteredInFleetRegistryAfterAddingFleetComponent
   - SpatialIndexingWorksForFleets
   - FleetPostureReflectedInRegistryFlags

4. **`Assets/Scripts/Space4x/Tests/Space4XFleetInterceptIntegrationTests.cs`**
   - InterceptRequestGeneratedForNearbyFleets
   - InterceptCourseCalculatedAndApplied
   - FleetTelemetryPublished
   - RendezvousFallbackWhenInterceptDisabled

5. **`Assets/Scripts/Space4x/Tests/Space4XResourceRegistryIntegrationTests.cs`**
   - AsteroidsRegisteredInResourceRegistry
   - ResourceRegistryEntryUpdatedOnMining
   - ResourceRegistryQueriesWorkCorrectly

6. **`Assets/Scripts/Space4x/Tests/PlayMode/Space4XMiningFullCycleRewindTests.cs`**
   - FullMiningCycleRewindDeterminism
   - MultipleVesselsRewindDeterminism
   - RewindPreservesTelemetryState

7. **`Assets/Scripts/Space4x/Tests/Space4XTelemetryBootstrapTests.cs`**
   - TelemetryStreamExistsInDemoScenes
   - MiningTelemetryMetricsPublished
   - InterceptTelemetryMetricsPublished
   - TelemetryAccessibleWithoutUI

8. **`Assets/Scripts/Space4x/Tests/Space4XPresentationIntegrationTests.cs`**
   - PresentationBindingsWorkWithoutUI
   - RemovingBindingsLeavesSimIntact
   - PresentationRegistryResolvesDescriptors

### Test Infrastructure Needed

1. **Bootstrap Helpers**:
   - `EnsureTelemetryStream()` - Creates TelemetryStream singleton
   - `EnsureResourceRegistry()` - Creates and populates ResourceRegistry
   - `EnsurePresentationRegistry()` - Creates presentation registry with descriptors

2. **Test Utilities**:
   - `CreateCarrierWithFleet()` - Creates carrier with all fleet components
   - `CreateAsteroidWithRegistry()` - Creates asteroid and registers it
   - `AdvanceTicks(int count)` - Helper to advance simulation multiple ticks
   - `VerifyTelemetryMetric()` - Helper to verify metric exists and has value

---

## Telemetry Verification Checklist

### Mining Telemetry
- [ ] `TelemetryStream` singleton exists in demo scenes
- [ ] `Space4XMiningTelemetry` singleton created automatically
- [ ] `space4x.mining.oreInHold` metric published
- [ ] `space4x.mining.oreInHold.lastTick` metric published
- [ ] Metrics update when carrier picks up resources
- [ ] Metrics accessible via ECS queries (no UI dependencies)

### Intercept Telemetry
- [ ] `Space4XFleetInterceptQueue` singleton exists
- [ ] `Space4XFleetInterceptTelemetry` component exists
- [ ] `space4x.intercept.attempts` metric published
- [ ] `space4x.intercept.rendezvous` metric published
- [ ] `space4x.intercept.lastTick` metric published
- [ ] Metrics update when intercept requests processed

### Registry Telemetry
- [ ] `Space4XFleetRegistry` singleton exists
- [ ] `Space4XFleetRegistryEntry` buffer populated
- [ ] `Space4XRegistrySnapshot` updated with fleet data
- [ ] Registry metrics accessible via snapshot

---

## Acceptance Criteria Verification

### Phase 2 Acceptance (from Phase2_Demo_TODO.md)

1. **Registry Continuity Validation**:
   - [ ] Test: `AsteroidsRegisteredInResourceRegistry` passes
   - [ ] Test: `CarriersRegisteredInFleetRegistryAfterAddingFleetComponent` passes
   - [ ] System exists to populate `ResourceRegistryEntry` buffer

2. **Rewind Determinism**:
   - [ ] Test: `FullMiningCycleRewindDeterminism` passes
   - [ ] Test: `MultipleVesselsRewindDeterminism` passes
   - [ ] All state restored correctly after rewind

3. **Presentation-Driven**:
   - [ ] Test: `PresentationBindingsWorkWithoutUI` passes
   - [ ] Test: `RemovingBindingsLeavesSimIntact` passes
   - [ ] Presentation registry set up in demo scenes

4. **PlayMode Tests**:
   - [ ] Test: `FullMiningCycleRewindDeterminism` passes
   - [ ] Test: `MultipleVesselsRewindDeterminism` passes
   - [ ] Registry continuity assertions pass

---

## Implementation Priority

### High Priority (Blocking Demo)
1. Fix movement for `MiningOrder` vessels
2. Add `Space4XFleet` to carriers for registry visibility
3. Create `ResourceRegistryEntry` population system
4. Verify `TelemetryStream` bootstrap in demo scenes

### Medium Priority (Demo Enhancement)
1. Add intercept capability to carriers
2. Create full cycle rewind tests
3. Add registry continuity tests
4. Verify presentation system setup

### Low Priority (Polish)
1. Add formation coordination tests
2. Add stance-based AI tests
3. Add telemetry accessibility tests
4. Add presentation binding tests


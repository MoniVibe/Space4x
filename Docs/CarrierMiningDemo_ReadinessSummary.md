# Carrier Mining Demo Readiness Summary

## Executive Summary

This document summarizes the readiness assessment for the carrier mining and combat demo. The assessment identified critical gaps in movement systems, combat integration, and test coverage that must be addressed before the demo can be fully functional.

---

## Key Findings

### ✅ What Works

1. **Mining Core Systems**: 
   - `Space4XMinerMiningSystem` processes mining orders correctly
   - Yield spawn bridge and resource spawn systems work
   - Carrier pickup system functions correctly
   - Telemetry systems publish metrics

2. **Authoring**:
   - `Space4XMiningDemoAuthoring` bakes carriers, vessels, and asteroids correctly
   - Presentation bindings are created automatically
   - Visual config singleton is set up

3. **Time Spine**:
   - Rewind/playback system works for mining state
   - Command log and snapshots are recorded correctly

4. **Combat Systems** (when components present):
   - Intercept pathfinding works correctly
   - Fleet broadcast system updates positions
   - Telemetry publishes intercept metrics

### ❌ Critical Gaps

1. **Movement Broken**:
   - Vessels with `MiningOrder` component are excluded from ALL movement systems
   - Legacy `MiningVesselSystem` also excludes `MiningOrder` entities
   - **Result**: Vessels spawn but never move to asteroids

2. **Combat Invisible**:
   - Carriers lack `Space4XFleet` component, so registry bridge can't see them
   - Carriers lack `FleetMovementBroadcast`, so intercept systems can't track them
   - **Result**: Carriers exist but are invisible to combat/registry systems

3. **Telemetry Stream**:
   - May be missing in demo scenes (needs verification)
   - No automatic bootstrap system found

4. **Registry Continuity**:
   - Asteroids may not be registered in `ResourceRegistryEntry` buffer
   - No system currently populates this buffer automatically

---

## Required Fixes

### Priority 1: Fix Movement (Blocking)

**Problem**: Vessels with `MiningOrder` cannot move.

**Solution Options**:
- **Option A**: Remove `MiningOrder` from demo authoring, use legacy system
- **Option B**: Add movement support to `MiningOrder` path (recommended)
- **Option C**: Hybrid approach

**Recommended**: Option B - Update movement systems to handle `MiningOrder` entities.

**Changes Required**:
1. Remove `[WithNone(typeof(MiningOrder))]` from:
   - `VesselMovementSystem`
   - `VesselTargetingSystem`
   - `VesselAISystem`
   - `VesselGatheringSystem`
   - `VesselDepositSystem`
2. Update `VesselAISystem` to set targets from `MiningOrder.TargetEntity`
3. Ensure systems can handle both `MiningOrder` and legacy paths

### Priority 2: Add Fleet Components (Blocking Combat)

**Problem**: Carriers are invisible to registry and intercept systems.

**Solution**: Add `Space4XFleet` and `FleetMovementBroadcast` components to carriers.

**Implementation Options**:
- **Option A**: Extend `Space4XMiningDemoAuthoring` to optionally add fleet components
- **Option B**: Create separate `Space4XCarrierCombatAuthoring` component
- **Option C**: Enhance `Space4XFleetInterceptAuthoring` to add `Space4XFleet`

**Recommended**: Option B + Option C Enhancement
- Enhance `Space4XFleetInterceptAuthoring` to add `Space4XFleet`
- Create optional `Space4XCarrierStanceAuthoring` for stance/formation

### Priority 3: Verify Telemetry Bootstrap

**Problem**: `TelemetryStream` may be missing in demo scenes.

**Solution**: 
1. Verify `TelemetryStream` exists in demo scenes
2. Add bootstrap system if missing
3. Ensure `PureDotsConfigAuthoring` creates it

### Priority 4: Register Asteroids

**Problem**: Asteroids may not appear in `ResourceRegistryEntry` buffer.

**Solution**: Create system to populate `ResourceRegistryEntry` buffer with asteroid data.

**Implementation**:
- New system: `Space4XResourceRegistryPopulationSystem`
- Runs in `InitializationSystemGroup` after bootstrap
- Queries all asteroids and adds entries to `ResourceRegistryEntry` buffer

---

## Test Coverage Status

### Existing Tests ✅
- `Space4XMinerMiningSystemTests` - Mining tick, yield, effects, rewind guards
- `FleetInterceptSystemsTests` - Broadcast, pathfinding, rendezvous
- `Space4XMiningTimeSpineTests` (PlayMode) - Spawn, pickup, rewind restoration

### Missing Tests ❌
- Movement integration tests (vessel moves to asteroid, returns to carrier)
- Combat integration tests (carriers in registry, intercept generation)
- Registry continuity tests (asteroids registered, carriers registered)
- Full cycle rewind tests (mine → spawn → pickup → rewind)
- Telemetry bootstrap tests (stream exists, metrics published)

**See**: `Docs/CarrierMiningDemo_TestsAndTelemetry.md` for detailed test specifications

---

## Documentation Created

1. **`Docs/CarrierMiningDemo_ReadinessAssessment.md`**
   - Complete system audit
   - Component requirements
   - Bootstrap dependencies
   - Critical gaps identified

2. **`Docs/CarrierMiningDemo_MiningLoopDependencies.md`**
   - System dependency chain
   - Component requirements checklist
   - Movement gap analysis
   - Test checklist

3. **`Docs/CarrierMiningDemo_CombatWiring.md`**
   - Component requirements for combat
   - Implementation options
   - Demo setup examples
   - System integration flow

4. **`Docs/CarrierMiningDemo_TestsAndTelemetry.md`**
   - Required test specifications
   - Telemetry verification checklist
   - Acceptance criteria mapping
   - Implementation priority

---

## Next Steps

### Immediate Actions

1. **Fix Movement** (Priority 1):
   - Remove `MiningOrder` exclusions from movement systems
   - Update `VesselAISystem` to handle `MiningOrder` entities
   - Test vessel movement to asteroids

2. **Add Fleet Components** (Priority 2):
   - Enhance `Space4XFleetInterceptAuthoring` to add `Space4XFleet`
   - Add fleet components to demo scene carriers
   - Verify registry bridge picks them up

3. **Verify Telemetry** (Priority 3):
   - Check demo scenes for `TelemetryStream`
   - Add bootstrap if missing
   - Verify metrics published

4. **Register Asteroids** (Priority 4):
   - Create `Space4XResourceRegistryPopulationSystem`
   - Populate `ResourceRegistryEntry` buffer
   - Verify asteroids appear in registry

### Testing

1. Create movement integration tests
2. Create combat integration tests
3. Create registry continuity tests
4. Create full cycle rewind tests
5. Create telemetry bootstrap tests

### Demo Scene Setup

1. Add `Space4XFleetInterceptAuthoring` to carriers
2. Configure fleet IDs and postures
3. Set up two opposing carriers
4. Verify intercept systems work
5. Verify registry/telemetry updates

---

## Acceptance Criteria Status

### Phase 2 Acceptance (from Phase2_Demo_TODO.md)

- [ ] **Registry Continuity**: Asteroids registered in `ResourceRegistryEntry` buffer
- [ ] **Rewind Determinism**: Full cycle test passes (mine → spawn → pickup → rewind)
- [ ] **Presentation-Driven**: Bindings work without UI, removing bindings leaves sim intact
- [ ] **PlayMode Tests**: Full coverage for mining tick, pickup, telemetry, rewind

**Current Status**: Partially complete - basic rewind works, but full cycle test and registry continuity missing.

---

## Risk Assessment

### High Risk
- **Movement broken**: Demo cannot function without vessel movement
- **Combat invisible**: Combat demo cannot work without fleet components

### Medium Risk
- **Telemetry missing**: Metrics may not be accessible
- **Registry incomplete**: Some systems may not find entities

### Low Risk
- **Test coverage**: Can be added incrementally
- **Presentation**: Already working, just needs verification

---

## Conclusion

The carrier mining demo has a solid foundation with working core systems, but critical gaps in movement and combat integration must be addressed before the demo can be fully functional. The assessment provides clear paths forward for each gap, with recommended solutions and implementation priorities.

**Estimated Effort**:
- Movement fix: 2-4 hours
- Fleet components: 1-2 hours
- Telemetry verification: 1 hour
- Registry population: 2-3 hours
- Test creation: 4-6 hours

**Total**: ~10-16 hours to reach production-ready state


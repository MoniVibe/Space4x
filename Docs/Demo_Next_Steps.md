# Demo Fleshing Out: Next Steps & Priorities

**Date**: 2025-01-XX  
**Status**: Core blockers fixed in code; demo scenes and tests still need verification

---

## Overview

The codebase is error-free and all systems compile. Implementations now cover the previously flagged integration gaps, but the demo still needs end-to-end validation in-scene and supporting tests. This remains primarily Unity/C# work.

---

## ✅ Blockers Resolved (verify in scenes)

- **Movement system gap**: MiningOrder entities now flow through AI/targeting/movement/gather/deposit (`Assets/Scripts/Space4x/Systems/AI/VesselAISystem.cs`, `VesselTargetingSystem.cs`, `VesselMovementSystem.cs`, `VesselGatheringSystem.cs`, `VesselDepositSystem.cs`). Vessels should pick MiningOrder targets and travel to asteroids; needs play-mode validation.
- **Combat/registry visibility**: Carriers auto-gain `Space4XFleet` and `FleetMovementBroadcast` via `Assets/Scripts/Space4x/Registry/Space4XCarrierFleetBootstrapSystem.cs`. `Space4XCarrierCombatAuthoring` remains available for stance/formation controls. Registry bridge and intercept systems should see carriers; verify buffers populate in-scene.
- **Telemetry bootstrap**: `Assets/Scripts/Space4x/Registry/Space4XTelemetryBootstrapSystem.cs` creates the `TelemetryStream` singleton with `TelemetryMetric` buffer, so telemetry systems can publish without scene setup. Confirm singleton appears once scenes load.
- **Resource registry population**: `Assets/Scripts/Space4x/Systems/AI/Space4XResourceRegistryPopulationSystem.cs` registers asteroids into `ResourceRegistryEntry` and tags them to prevent duplicates, covering both baked and spawned asteroids.

## 🔧 Remaining Work (Updated Priorities)

1. **Scene validation loop**: Play the demo scene; confirm miners receive MiningOrder targets, move to asteroids, mine, and return; asteroids appear in `ResourceRegistryEntry`; carriers show up in `Space4XFleetRegistryEntry` and intercept telemetry.
2. **Combat wiring in demo scenes**: Add `Space4XCarrierCombatAuthoring` or `Space4XFleetInterceptAuthoring` to demo carriers if you need explicit posture/formation controls; confirm intercept pathfinding produces courses.
3. **Telemetry/registry checks**: Ensure only one `TelemetryStream` exists and metrics stream during mining/combat; spot-check registry buffers for both fleets and resources.
4. **Tests**: Add the missing integration and playmode coverage (movement, combat visibility, registry continuity, full rewind cycle, telemetry bootstrap) to lock in the fixes above.

---

## ✅ What Works (No Changes Needed)

- **Mining Core**: `Space4XMinerMiningSystem` processes mining orders correctly
- **Yield & Pickup**: Yield spawn bridge and carrier pickup work
- **Authoring**: `Space4XMiningDemoAuthoring` bakes entities correctly
- **Time Spine**: Rewind/playback system works
- **Combat Systems**: Intercept pathfinding works when components present
- **Telemetry**: Systems publish metrics correctly

---

## 📋 Phase 2 Demo Acceptance Criteria

From `Docs/TODO/Phase2_Demo_TODO.md`, these must pass:

- [ ] **Registry Continuity**: Asteroids registered in `ResourceRegistryEntry` buffer
- [ ] **Rewind Determinism**: Full cycle test passes (mine → spawn → pickup → rewind)
- [ ] **Presentation-Driven**: Bindings work without UI, removing bindings leaves sim intact
- [ ] **PlayMode Tests**: Full coverage for mining tick, pickup, telemetry, rewind

**Current Status**: Systems implemented; awaiting play-mode validation and tests for registry continuity and full rewind cycle.

---

## 🎯 Recommended Order of Work

### Step 1: Validate Mining Loop & Movement
**Time**: 1-2 hours

1. Play the demo scene: miners should take MiningOrders, travel to asteroids, gather, and return cargo.
2. Confirm `ResourceRegistryEntry` holds asteroids and `Space4XFleetRegistryEntry` holds carriers (from bootstrap system).
3. Spot-check intercept telemetry entries while miners and carriers move.

**Verification**: Mining loop runs end-to-end in play mode with registry buffers filling.

---

### Step 2: Wire Combat Scenarios
**Time**: 1 hour

1. Add `Space4XCarrierCombatAuthoring` or `Space4XFleetInterceptAuthoring` to demo carriers if posture/formation control is needed.
2. Configure opposing carriers and confirm intercept pathfinding generates courses.
3. Ensure intercept telemetry increments when paths are computed.

**Verification**: Carriers appear in registry, can be targeted/intercepted, and telemetry logs attempts.

---

### Step 3: Telemetry & Registry Sanity Sweep
**Time**: 1 hour

1. Ensure only one `TelemetryStream` exists after bootstrap.
2. Check metrics buffers populate during mining and combat loops.
3. Validate registry snapshot entities update without errors.

**Verification**: No missing singleton errors; metrics and registry entries update while playing.

---

### Step 4: Add Missing Tests
**Time**: 4-6 hours

Create integration tests:
1. Movement integration tests (vessel moves to asteroid, returns to carrier)
2. Combat integration tests (carriers in registry, intercept generation)
3. Registry continuity tests (asteroids registered, carriers registered)
4. Full cycle rewind tests (mine → spawn → pickup → rewind)
5. Telemetry bootstrap tests (stream exists, metrics published)

**Files to Create**:
- `Assets/Scripts/Space4x/Tests/Space4XMiningMovementIntegrationTests.cs`
- `Assets/Scripts/Space4x/Tests/Space4XCombatIntegrationTests.cs`
- `Assets/Scripts/Space4x/Tests/Space4XRegistryContinuityTests.cs` (may already exist)
- `Assets/Scripts/Space4x/Tests/PlayMode/Space4XMiningCycleRewindTests.cs`

---

## 🔧 Is It Only Unity Work?

**Yes, primarily Unity/C# work**, but consider:

### Unity Work (100% in Unity Editor/Code)
- ✅ Movement/registry/telemetry systems implemented (see resolved blockers above)
- Pending: Play-mode validation of mining loop and registry buffers
- Pending: Combat authoring setup for demo carriers where needed
- Pending: Integration + playmode test coverage
- Pending: Scene tuning/verification based on validation results

### Non-Unity Considerations (Minimal)

1. **PureDOTS Package Dependency**:
   - PureDOTS is external package referenced via `file:../../PureDOTS/Packages/com.moni.puredots`
   - **Action**: Ensure PureDOTS is up-to-date and compatible
   - **Verification**: Check `Packages/manifest.json` reference is valid

2. **Documentation Updates**:
   - Update `Docs/Progress.md` with current status
   - Document any new authoring workflows
   - **Action**: Keep docs in sync as you work

3. **Git Commit Strategy**:
   - Commit fixes incrementally (one priority at a time)
   - Include tests with each fix
   - **Action**: Standard git workflow, no special tools needed

---

## 📊 Total Estimated Effort

| Task | Time | Priority |
|------|------|----------|
| Scene validation (mining loop, registry buffers) | 1-2 hours | P1 (Verification) |
| Combat wiring in demo scenes | 1 hour | P2 (Demo readiness) |
| Telemetry/registry sanity sweep | 1 hour | P3 (Visibility) |
| Add integration/playmode tests | 4-6 hours | P4 (Quality) |
| Docs & reporting | 0.5 hour | P5 (Tracking) |
| **Total** | **7.5-10.5 hours** | |

---

## 🚀 After Fixes: Fleshing Out the Demo

Once blockers are fixed, you can:

### 1. **Enhance Demo Scenes**
- Add multiple carriers with different postures
- Create combat scenarios (intercept demo)
- Add visual feedback (mining effects, fleet indicators)

### 2. **Extend Authoring Tools**
- Add more configuration options to `Space4XMiningDemoAuthoring`
- Create `Space4XCombatDemoAuthoring` for combat scenarios
- Build scene setup tools in editor

### 3. **Add HUD/Debug UI**
- Connect telemetry to UI displays
- Show registry counts, fleet status
- Add rewind timeline controls

### 4. **Create More Scenarios**
- Mutiny/desertion demo (Agent A work)
- Module refit demo (Agent B work)
- Economy/trade demo (Agent C work)

### 5. **Improve Presentation**
- Generate prefabs via Prefab Maker (see `space.plan.md` section 7)
- Add visual effects for mining/combat
- Create binding blob assets

---

## 🎯 Decision: Validate Now or Jump to Polish?

### Option A: Validate loops now (Recommended)
**Pros**: Confirms the resolved blockers actually work in-scene, de-risks tests, and keeps telemetry/registry honest  
**Cons**: Less visible progress until validation is done  
**Best For**: Locking demo behavior before presentation polish

### Option B: Polish visuals while validating
**Pros**: Visible progress in parallel (VFX, HUD, bindings)  
**Cons**: Risk of masking issues until validation/tests catch up  
**Best For**: When you need showpieces immediately but can juggle validation

**Recommendation**: Run the validation loop (Steps 1-3 above) first, then layer polish.

---

## 📝 Next Actions

1. **Immediate**: Play demo scene to validate mining loop + registry buffers (Steps 1-3 above)
2. **Then**: Wire combat authoring on carriers for intercept scenarios and confirm telemetry
3. **Verify**: Ensure single `TelemetryStream` and healthy registry snapshots during play
4. **Quality**: Add integration + playmode tests to lock behavior

---

## 📚 Reference Documents

- `Docs/CarrierMiningDemo_ReadinessSummary.md` - Full assessment
- `Docs/CarrierMiningDemo_CombatWiring.md` - Combat integration details
- `Docs/CarrierMiningDemo_MiningLoopDependencies.md` - System dependencies
- `Docs/TODO/Phase2_Demo_TODO.md` - Phase 2 acceptance criteria
- `space.plan.md` - Demo readiness closure plan

---

**Summary**: Core blockers are fixed in code; focus now shifts to validating the mining/combat loops in-scene, ensuring telemetry/registry stay healthy, and adding integration/playmode tests. No external tool dependencies beyond keeping PureDOTS available.


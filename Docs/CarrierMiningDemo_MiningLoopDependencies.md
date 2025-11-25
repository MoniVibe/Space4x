# Mining Loop Dependency & Test Checklist

## System Dependency Chain

### Phase 1: Bootstrap (InitializationSystemGroup)
```
Space4XCoreSingletonGuardSystem
  → Creates: TimeState, RewindState, GameplayFixedStep
  → Dependencies: None (runs first)

Space4XMiningTimeSpineBootstrapSystem  
  → Creates: Space4XMiningTimeSpine + buffers
  → Dependencies: TimeState
  → Buffers: MiningSnapshot, MiningTelemetrySnapshot, MiningCommandLogEntry, SkillChangeLogEntry, SkillSnapshot

Space4XResourceRegistryBootstrapSystem
  → Ensures: ResourceRegistry + ResourceRegistryEntry buffer
  → Dependencies: ResourceRegistryEntry component (from PureDOTS)
  → Note: Only ensures structure, doesn't populate entries

Space4XFleetInterceptBootstrapSystem
  → Creates: Space4XFleetInterceptQueue + buffers
  → Dependencies: TimeState
  → Buffers: InterceptRequest, FleetInterceptCommandLogEntry
```

### Phase 2: Mining Execution (FixedStepSimulationSystemGroup)

**Order of Execution**:
1. `Space4XMinerMiningSystem` (FixedStep, after GameplayFixedStepSyncSystem)
   - **Inputs**: `MiningOrder`, `MiningState`, `MiningVessel`, `MiningYield`, `LocalTransform`
   - **Reads**: `ResourceSourceState`, `ResourceSourceConfig`, `ResourceTypeId`, `CrewSkills`
   - **Creates**: `Space4XEffectRequestStream` singleton (if missing)
   - **Outputs**: 
     - Updates `MiningYield.PendingAmount`
     - Updates `MiningVessel.CurrentCargo`
     - Updates `ResourceSourceState.UnitsRemaining`
     - Emits `PlayEffectRequest` to effect stream
     - Writes `MiningCommandLogEntry` (if command log exists)
   - **Guards**: `RewindMode.Record` only (skips during playback/catch-up)

2. `Space4XMiningYieldSpawnBridgeSystem` (ResourceSystemGroup, before MiningResourceSpawnSystem)
   - **Inputs**: `MiningYield`, `MiningVessel`, `SpawnResourceRequest` buffer, `LocalTransform`
   - **Outputs**: Adds `SpawnResourceRequest` entries when `PendingAmount >= SpawnThreshold`
   - **Guards**: `RewindMode.Record` only

3. `MiningResourceSpawnSystem` (ResourceSystemGroup, after VesselGatheringSystem)
   - **Inputs**: `MiningVessel`, `SpawnResourceRequest` buffer, `MiningYield` (optional), `LocalTransform`
   - **Outputs**: Creates `SpawnResource` entities with `LocalTransform`
   - **Guards**: `RewindMode.Record` only

4. `CarrierPickupSystem` (ResourceSystemGroup, after MiningResourceSpawnSystem)
   - **Inputs**: `Carrier`, `ResourceStorage` buffer, `SpawnResource`, `LocalTransform`, `CrewSkills` (optional)
   - **Outputs**: 
     - Transfers resources to carrier `ResourceStorage` buffer
     - Updates `Space4XMiningTelemetry` singleton
     - Destroys `SpawnResource` entities when amount <= 0
   - **Guards**: `RewindMode.Record` only

### Phase 3: Movement (ResourceSystemGroup / TransportAISystemGroup)

**Current State**: Vessels with `MiningOrder` are EXCLUDED from all movement systems.

**Legacy Path** (`MiningVesselSystem` in Space4XDemoSystems.cs):
- **Inputs**: `MiningVessel`, `MiningJob`, `LocalTransform`
- **Reads**: `Carrier`, `Asteroid`, `ResourceStorage` buffer
- **Outputs**: Updates `LocalTransform` position, updates `MiningJob.State`
- **Note**: System queries `WithNone<MiningOrder>` (line 200), so it also skips vessels with MiningOrder

**New Path** (Intended but broken):
- `VesselAISystem` - Assigns targets (EXCLUDES MiningOrder)
- `VesselTargetingSystem` - Resolves target positions (EXCLUDES MiningOrder)
- `VesselMovementSystem` - Moves vessels (EXCLUDES MiningOrder)
- `VesselGatheringSystem` - Gathers resources (EXCLUDES MiningOrder)
- `VesselDepositSystem` - Deposits to carrier (EXCLUDES MiningOrder)

**Problem**: `Space4XMiningDemoAuthoring` bakes both `MiningOrder` AND `MiningJob`, but:
- Legacy system excludes `MiningOrder` entities
- New systems exclude `MiningOrder` entities
- **Result**: No movement system handles these vessels

### Phase 4: Telemetry (PresentationSystemGroup)

1. `Space4XMiningTelemetrySystem`
   - **Inputs**: `Space4XMiningTelemetry` singleton
   - **Reads**: `TelemetryStream` singleton
   - **Outputs**: Publishes `TelemetryMetric` entries
   - **Metrics**: `space4x.mining.oreInHold`, `space4x.mining.oreInHold.lastTick`

2. `Space4XFleetInterceptTelemetrySystem`
   - **Inputs**: `Space4XFleetInterceptTelemetry` singleton
   - **Reads**: `TelemetryStream` singleton
   - **Outputs**: Publishes intercept metrics
   - **Metrics**: `space4x.intercept.attempts`, `space4x.intercept.rendezvous`, `space4x.intercept.lastTick`

### Phase 5: Time Spine Recording (HistorySystemGroup)

`Space4XMiningTimeSpineRecordSystem`
- **Inputs**: All mining entities (asteroids, vessels, carriers, spawns)
- **Outputs**: `MiningSnapshot`, `MiningTelemetrySnapshot`, `MiningCommandLogEntry`, `SkillSnapshot` buffers
- **Guards**: `RewindMode.Record` only

---

## Component Requirements Checklist

### For Mining Vessel to Function

**Required Components**:
- ✅ `MiningVessel` - Vessel identity and cargo state
- ✅ `MiningOrder` - Resource to mine and target
- ✅ `MiningState` - Current mining phase and timer
- ✅ `MiningYield` - Accumulated yield and spawn threshold
- ✅ `LocalTransform` - Position in world
- ✅ `SpawnResourceRequest` buffer - For spawn bridge system

**Optional but Recommended**:
- `CrewSkills` - Mining skill multiplier (up to +50% output)
- `VesselAIState` - For movement systems (if not using MiningOrder path)
- `VesselMovement` - For movement systems (if not using MiningOrder path)
- `MiningJob` - Legacy component (conflicts with MiningOrder path)

**Movement Path Decision Required**:
- **Option A**: Remove `MiningOrder`, use legacy `MiningJob` + `MiningVesselSystem`
- **Option B**: Keep `MiningOrder`, fix movement systems to handle it
- **Option C**: Hybrid - use `MiningOrder` for mining logic, but add movement support

### For Carrier to Function

**Required Components**:
- ✅ `Carrier` - Carrier identity
- ✅ `ResourceStorage` buffer - Storage slots for resources
- ✅ `LocalTransform` - Position in world
- ✅ `PatrolBehavior` - For patrol movement (if using CarrierPatrolSystem)
- ✅ `MovementCommand` - For patrol movement (if using CarrierPatrolSystem)

**Optional**:
- `CrewSkills` - Hauling skill extends pickup radius (+25% per skill point)
- `Space4XMiningTelemetry` - Auto-created by CarrierPickupSystem if missing

### For Asteroid to Function

**Required Components**:
- ✅ `Asteroid` - Asteroid identity and resource type
- ✅ `ResourceSourceState` - Current resource amount
- ✅ `ResourceSourceConfig` - Mining rate and worker limits
- ✅ `ResourceTypeId` - Registry resource identifier
- ✅ `LocalTransform` - Position in world
- ✅ `LastRecordedTick` - For rewind support
- ✅ `RewindableTag` - For rewind support
- ✅ `HistoryTier` - For history recording
- ✅ `ResourceHistorySample` buffer - For history recording

**Registry Integration**:
- ⚠️ `ResourceRegistryEntry` - Should be populated but no system currently does this automatically

### For Combat/Intercept Participation

**Required Components**:
- `Space4XFleet` - Fleet identity for registry bridge
- `FleetMovementBroadcast` - Position/velocity broadcast for intercept systems
- `LocalTransform` - Position (already present)
- `SpatialIndexedTag` - Spatial indexing (already present)

**Optional**:
- `FleetKinematics` - Explicit velocity tracking
- `SpatialGridResidency` - Spatial grid tracking
- `InterceptCapability` - If carrier should intercept others
- `InterceptCourse` - Set by intercept pathfinding
- `VesselStanceComponent` - For stance-based AI
- `FormationData` - For formation coordination

---

## Test Checklist

### Unit Tests (EditMode)

**Mining System Tests** (`Space4XMinerMiningSystemTests`):
- ✅ Miner processes scripted order and emits effect
- ✅ Miner skips ticks when rewind is not recording
- ✅ Miner logs command and queues spawn request
- ✅ Spawn system uses mining yield threshold and resource ID
- ✅ Mining skill amplifies mining tick
- ❌ **MISSING**: Movement to asteroid (vessel never moves in tests)
- ❌ **MISSING**: Return to carrier after cargo full

**Fleet Intercept Tests** (`FleetInterceptSystemsTests`):
- ✅ Fleet broadcast updates tick velocity and residency
- ✅ Intercept pathfinding computes predictive course when tech allows
- ✅ Intercept pathfinding falls back to rendezvous when intercept disabled
- ✅ Fleet intercept request system selects nearest fleet and creates course
- ❌ **MISSING**: Integration with carriers (tests use generic entities)

**Time Spine Tests** (`Space4XMiningTimeSpineTests` - PlayMode):
- ✅ Mining tick records spawn and commands
- ✅ Carrier pickup updates telemetry and storage
- ✅ Hauling skill extends pickup radius
- ✅ Rewind restores mining state from spine
- ❌ **MISSING**: Full cycle test (mine → spawn → pickup → rewind)

### Integration Tests Needed

1. **Movement Integration Test**:
   - Create vessel with `MiningOrder` at distance from asteroid
   - Verify vessel moves toward asteroid
   - Verify vessel stops at asteroid and begins mining
   - Verify vessel returns to carrier when cargo full
   - Verify vessel deposits cargo to carrier

2. **Combat Integration Test**:
   - Create two carriers with `Space4XFleet` and `FleetMovementBroadcast`
   - Verify intercept request generation
   - Verify intercept course calculation
   - Verify registry bridge picks up fleets
   - Verify telemetry metrics published

3. **Registry Continuity Test**:
   - Verify asteroids appear in `ResourceRegistryEntry` buffer
   - Verify carriers appear in `Space4XFleetRegistryEntry` buffer after adding `Space4XFleet`
   - Verify spatial indexing works correctly

4. **Full Cycle Rewind Test**:
   - Mine asteroid → spawn pickup → carrier pickup
   - Record state at tick N
   - Rewind to tick N-10
   - Verify all state restored correctly
   - Catch up to tick N
   - Verify state matches original

5. **Telemetry Stream Test**:
   - Verify `TelemetryStream` singleton exists
   - Verify mining telemetry metrics published
   - Verify intercept telemetry metrics published (if combat enabled)
   - Verify metrics accessible without UI dependencies

---

## Movement Fix Recommendations

### Option A: Remove MiningOrder from Demo Authoring
**Pros**: Quick fix, uses existing legacy system
**Cons**: Can't use new mining order system features
**Implementation**: Modify `Space4XMiningDemoAuthoring.BakeMiningVessels()` to not add `MiningOrder` component

### Option B: Add Movement Support to MiningOrder Path
**Pros**: Uses modern system, more flexible
**Cons**: Requires updating multiple systems
**Implementation**: 
- Remove `[WithNone(typeof(MiningOrder))]` from `VesselMovementSystem`, `VesselTargetingSystem`, `VesselAISystem`, `VesselGatheringSystem`, `VesselDepositSystem`
- Add logic to handle `MiningOrder` entities in these systems
- Ensure `MiningOrder.TargetEntity` is set correctly for movement

### Option C: Hybrid Approach
**Pros**: Best of both worlds
**Cons**: More complex, two code paths
**Implementation**:
- Keep `MiningOrder` for mining logic (handled by `Space4XMinerMiningSystem`)
- Add `VesselAIState` and `VesselMovement` for movement (handled by existing systems)
- Ensure systems can coexist (remove exclusions, add proper state management)

### Recommended: Option B
Update movement systems to handle `MiningOrder` entities. This aligns with the modern architecture and allows future expansion.

**Changes Required**:
1. Remove `[WithNone(typeof(MiningOrder))]` from movement-related systems
2. Update `VesselAISystem` to set `VesselAIState.TargetEntity` from `MiningOrder.TargetEntity`
3. Update `VesselGatheringSystem` to work with `MiningOrder` entities
4. Update `VesselDepositSystem` to work with `MiningOrder` entities
5. Ensure `MiningOrder` and `VesselAIState` stay in sync


# Combat/Intercept Wiring Specification

## Overview

This document outlines the concrete components and systems required to make carriers participate in fleet intercept systems and appear in the registry/telemetry.

---

## Component Requirements

### 1. Registry Bridge Visibility

**System**: `Space4xRegistryBridgeSystem.UpdateFleetRegistry()`

**Required Components**:
```csharp
Space4XFleet fleet;           // Fleet identity component
LocalTransform transform;     // Position (already present from Space4XMiningDemoAuthoring)
SpatialIndexedTag tag;        // Spatial indexing (already present from Space4XMiningDemoAuthoring)
```

**Component Details**:
- `Space4XFleet`:
  - `FleetId`: Unique identifier (FixedString64Bytes)
  - `ShipCount`: Number of ships in fleet (int)
  - `Posture`: Current posture enum (Space4XFleetPosture: Idle, Patrol, Engaging, Retreating, Docked)
  - `TaskForce`: Optional task force assignment (int)

**Registry Output**:
- Creates entry in `Space4XFleetRegistryEntry` buffer on `Space4XFleetRegistry` entity
- Updates `Space4XFleetRegistry` summary (FleetCount, TotalShips, ActiveEngagementCount)
- Updates `Space4XRegistrySnapshot` with fleet metrics

### 2. Intercept System Participation

**Systems**: `FleetBroadcastSystem`, `FleetInterceptRequestSystem`, `InterceptPathfindingSystem`

**Required Components**:
```csharp
FleetMovementBroadcast broadcast;  // Position/velocity broadcast
LocalTransform transform;          // Position (already present)
```

**Optional but Recommended**:
```csharp
FleetKinematics kinematics;       // Explicit velocity source
SpatialGridResidency residency;    // Spatial grid tracking
```

**Component Details**:
- `FleetMovementBroadcast`:
  - `Position`: Current position (float3) - Updated by FleetBroadcastSystem
  - `Velocity`: Current velocity (float3) - From FleetKinematics or zero
  - `LastUpdateTick`: Last update tick (uint)
  - `AllowsInterception`: Whether this fleet can be intercepted (byte: 0/1)
  - `TechTier`: Tech tier for intercept calculations (byte)

- `FleetKinematics` (optional):
  - `Velocity`: Explicit velocity vector (float3)
  - Used if present, otherwise velocity is zero

- `SpatialGridResidency` (optional):
  - `CellId`: Current spatial grid cell (int)
  - `LastPosition`: Last recorded position (float3)
  - `Version`: Grid version (uint)

### 3. Active Interception Capability

**System**: `FleetInterceptRequestSystem`, `InterceptPathfindingSystem`

**Required Components** (if carrier should intercept other fleets):
```csharp
InterceptCapability capability;    // Interception capability
InterceptCourse course;            // Set by intercept pathfinding (output)
LocalTransform transform;          // Position (already present)
```

**Component Details**:
- `InterceptCapability`:
  - `MaxSpeed`: Maximum speed for intercept calculations (float)
  - `TechTier`: Tech tier (byte)
  - `AllowIntercept`: Whether interception is allowed (byte: 0/1)

- `InterceptCourse` (output, set by system):
  - `TargetFleet`: Target fleet entity (Entity)
  - `InterceptPoint`: Calculated intercept point (float3)
  - `EstimatedInterceptTick`: Estimated tick for intercept (uint)
  - `UsesInterception`: Whether using interception vs rendezvous (byte: 0/1)

### 4. Stance-Based AI

**Systems**: `Space4XVesselMovementAISystem`, `Space4XFleetCoordinationAISystem`, `Space4XStrikeCraftBehaviorSystem`

**Required Components**:
```csharp
VesselStanceComponent stance;      // Current and desired stance
```

**Optional**:
```csharp
FormationData formation;           // Formation coordination
ChildVesselTether tether;         // For strike craft coordination
```

**Component Details**:
- `VesselStanceComponent`:
  - `CurrentStance`: Current stance (VesselStance: Defensive, Aggressive, Neutral, Evasive)
  - `DesiredStance`: Desired stance (VesselStance)
  - `StanceChangeTick`: Tick when stance changed (uint)

- `FormationData` (optional):
  - `FormationTightness`: Tightness based on alignment (half: 0-1)
  - `FormationRadius`: Formation radius (float)
  - `FormationLeader`: Leader entity (Entity.Null if this is leader)
  - `FormationUpdateTick`: Last update tick (uint)

---

## Implementation Options

### Option A: Extend Space4XMiningDemoAuthoring

**Pros**: Single authoring component, consistent setup
**Cons**: Mixes mining and combat concerns

**Changes Required**:
1. Add optional combat fields to `CarrierDefinition`:
   ```csharp
   public bool enableCombat = false;
   public Space4XFleetPosture posture = Space4XFleetPosture.Patrol;
   public VesselStance stance = VesselStance.Neutral;
   public bool allowInterception = true;
   public byte techTier = 1;
   public bool canInterceptOthers = false;
   public float interceptSpeed = 10f;
   ```

2. Update `BakeCarriers()` to conditionally add:
   - `Space4XFleet` if `enableCombat == true`
   - `FleetMovementBroadcast` if `enableCombat == true`
   - `VesselStanceComponent` if `enableCombat == true`
   - `InterceptCapability` if `canInterceptOthers == true`

### Option B: Create Separate Combat Authoring Component

**Pros**: Separation of concerns, reusable
**Cons**: Requires adding component to each carrier GameObject

**New Component**: `Space4XCarrierCombatAuthoring`
- Adds `Space4XFleet`, `FleetMovementBroadcast`, `VesselStanceComponent`
- Optionally adds `InterceptCapability`, `FormationData`
- Can be added alongside `Space4XMiningDemoAuthoring`

### Option C: Use Existing Space4XFleetInterceptAuthoring

**Pros**: Already exists, tested
**Cons**: Doesn't add `Space4XFleet` for registry bridge

**Current Capabilities**:
- Adds `FleetMovementBroadcast`
- Adds `SpatialIndexedTag`, `SpatialGridResidency`
- Optionally adds `InterceptCapability`, `InterceptCourse`

**Gap**: Missing `Space4XFleet` component for registry bridge

**Fix**: Extend `Space4XFleetInterceptAuthoring` to also add `Space4XFleet` component

### Recommended: Option B + Option C Enhancement

1. Enhance `Space4XFleetInterceptAuthoring` to add `Space4XFleet` component
2. Create `Space4XCarrierCombatAuthoring` for stance/formation (optional)
3. Allow combining authoring components on same GameObject

---

## Demo Setup Example

### Two Opposing Carriers

**Carrier 1 (Friendly/Defensive)**:
```csharp
// From Space4XMiningDemoAuthoring
Carrier carrier;
ResourceStorage buffer;
PatrolBehavior patrol;
MovementCommand movement;

// Add for combat:
Space4XFleet fleet = new Space4XFleet {
    FleetId = "FRIENDLY-FLEET-1",
    ShipCount = 1,
    Posture = Space4XFleetPosture.Patrol,
    TaskForce = 101
};

FleetMovementBroadcast broadcast = new FleetMovementBroadcast {
    Position = transform.Position,
    Velocity = float3.zero,
    LastUpdateTick = 0,
    AllowsInterception = 1,
    TechTier = 1
};

VesselStanceComponent stance = new VesselStanceComponent {
    CurrentStance = VesselStance.Neutral,
    DesiredStance = VesselStance.Neutral,
    StanceChangeTick = 0
};
```

**Carrier 2 (Enemy/Aggressive)**:
```csharp
// Same base components as Carrier 1

Space4XFleet fleet = new Space4XFleet {
    FleetId = "ENEMY-FLEET-1",
    ShipCount = 1,
    Posture = Space4XFleetPosture.Engaging,  // Different posture
    TaskForce = 201
};

FleetMovementBroadcast broadcast = new FleetMovementBroadcast {
    Position = transform.Position,
    Velocity = float3.zero,
    LastUpdateTick = 0,
    AllowsInterception = 1,
    TechTier = 1
};

VesselStanceComponent stance = new VesselStanceComponent {
    CurrentStance = VesselStance.Aggressive,  // Aggressive stance
    DesiredStance = VesselStance.Aggressive,
    StanceChangeTick = 0
};

InterceptCapability capability = new InterceptCapability {
    MaxSpeed = 15f,
    TechTier = 1,
    AllowIntercept = 1
};
```

---

## System Integration Flow

### 1. Registry Bridge Flow
```
Carrier with Space4XFleet + LocalTransform + SpatialIndexedTag
  → Space4xRegistryBridgeSystem.UpdateFleetRegistry()
  → Creates Space4XFleetRegistryEntry
  → Updates Space4XFleetRegistry summary
  → Updates Space4XRegistrySnapshot
```

### 2. Intercept Flow
```
Carrier with FleetMovementBroadcast
  → FleetBroadcastSystem (updates Position/Velocity every tick)
  → FleetInterceptRequestSystem (finds nearby fleets, creates InterceptRequest)
  → InterceptPathfindingSystem (calculates intercept course)
  → Sets InterceptCourse component on requester
  → RendezvousCoordinationSystem (updates rendezvous courses)
```

### 3. Telemetry Flow
```
Space4XFleetInterceptTelemetry (singleton)
  → Space4XFleetInterceptTelemetrySystem
  → Publishes to TelemetryStream
  → Metrics: space4x.intercept.attempts, space4x.intercept.rendezvous
```

---

## Authoring Component Implementation

### Enhanced Space4XFleetInterceptAuthoring

**Current State**: Adds `FleetMovementBroadcast`, `SpatialIndexedTag`, `SpatialGridResidency`, optionally `InterceptCapability`

**Enhancement Needed**: Also add `Space4XFleet` component

**Proposed Changes**:
```csharp
[Header("Fleet Registry")]
public bool addFleetComponent = true;
public string fleetId = "FLEET-1";
public int shipCount = 1;
public Space4XFleetPosture posture = Space4XFleetPosture.Patrol;
public int taskForce = 0;

// In Baker:
if (authoring.addFleetComponent)
{
    AddComponent(entity, new Space4XFleet
    {
        FleetId = new FixedString64Bytes(authoring.fleetId),
        ShipCount = authoring.shipCount,
        Posture = authoring.posture,
        TaskForce = authoring.taskForce
    });
}
```

### New Space4XCarrierStanceAuthoring

**Purpose**: Add stance and formation components

**Fields**:
```csharp
[Header("Stance")]
public VesselStance initialStance = VesselStance.Neutral;
public VesselStance desiredStance = VesselStance.Neutral;

[Header("Formation (Optional)")]
public bool addFormationData = false;
public Entity formationLeader;  // Set in inspector
public float formationRadius = 50f;
```

**Baker**:
```csharp
AddComponent(entity, new VesselStanceComponent
{
    CurrentStance = authoring.initialStance,
    DesiredStance = authoring.desiredStance,
    StanceChangeTick = 0
});

if (authoring.addFormationData)
{
    AddComponent(entity, new FormationData
    {
        FormationTightness = (half)0.7f,
        FormationRadius = authoring.formationRadius,
        FormationLeader = GetEntity(authoring.formationLeader, TransformUsageFlags.None),
        FormationUpdateTick = 0
    });
}
```

---

## Verification Checklist

### Registry Bridge Verification
- [ ] Carrier appears in `Space4XFleetRegistryEntry` buffer
- [ ] `Space4XFleetRegistry.FleetCount` increments
- [ ] `Space4XRegistrySnapshot.FleetCount` updates
- [ ] Fleet posture reflected in registry flags

### Intercept System Verification
- [ ] `FleetMovementBroadcast` updates position every tick
- [ ] `FleetInterceptRequestSystem` creates intercept requests
- [ ] `InterceptPathfindingSystem` calculates intercept courses
- [ ] `InterceptCourse` component set on requester entities
- [ ] Telemetry metrics published (`space4x.intercept.attempts`, etc.)

### Combat Behavior Verification
- [ ] `VesselStanceComponent` affects formation behavior
- [ ] Aggressive stance triggers strike craft launch (if strike craft present)
- [ ] Formation coordination works with multiple carriers
- [ ] Stance changes propagate to subordinates

---

## Testing Requirements

### Unit Tests
- [ ] Registry bridge picks up carriers with `Space4XFleet`
- [ ] Intercept request generation works with `FleetMovementBroadcast`
- [ ] Intercept course calculation works correctly
- [ ] Telemetry metrics published correctly

### Integration Tests
- [ ] Two carriers with different postures appear in registry
- [ ] Intercept request created when carriers are nearby
- [ ] Intercept course calculated and applied
- [ ] Registry snapshot updates reflect fleet state
- [ ] Telemetry stream contains intercept metrics

### Demo Scene Tests
- [ ] Carriers visible in registry after adding `Space4XFleet`
- [ ] Intercept systems can find and target carriers
- [ ] Telemetry metrics accessible without UI
- [ ] Multiple carriers can coexist with different postures

---

## Migration Path

### For Existing Demo Scenes

1. **Add Fleet Components**:
   - Option A: Manually add `Space4XFleetInterceptAuthoring` to carrier GameObjects
   - Option B: Extend `Space4XMiningDemoAuthoring` to optionally add fleet components
   - Option C: Create new authoring component that adds both mining and combat components

2. **Verify Setup**:
   - Check that `Space4XFleet` component exists on carriers
   - Check that `FleetMovementBroadcast` updates every tick
   - Check that registry bridge picks up carriers
   - Check that telemetry metrics are published

3. **Test Combat**:
   - Create two opposing carriers
   - Verify intercept request generation
   - Verify intercept course calculation
   - Verify registry/telemetry updates

### Backward Compatibility

- Existing carriers without `Space4XFleet` continue to work for mining
- Adding `Space4XFleet` is optional and doesn't break mining functionality
- Combat systems gracefully handle missing components (queries filter appropriately)


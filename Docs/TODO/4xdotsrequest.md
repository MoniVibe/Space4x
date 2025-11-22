# Space4X DOTS Framework Request

This note captures the DOTS-side work needed to support Space4X's core mechanics. Items are organized by framework domain and prioritized for implementation.

---

## Core Mechanics Support (High Priority)

## Components & Data Shapes
- **Wire up alignment data**: Ensure every crew member prefab gains `AlignmentTriplet`, `RaceId`, `CultureId`, `DynamicBuffer<EthicAxisValue>`, and `DynamicBuffer<OutlookEntry>`. Aggregated crews should maintain `DynamicBuffer<TopOutlook>`, `RacePresence`, and `CulturePresence`.
- **Affiliations**: Populate `DynamicBuffer<AffiliationTag>` for crews, fleets, colonies, and factions. Loyalty should be derived from morale/contract state on spawn.
- **DoctrineAuthoring**: Expose a monobehaviour/baker that maps to `DoctrineProfile`, `DynamicBuffer<DoctrineAxisExpectation>`, and `DynamicBuffer<DoctrineOutlookExpectation>` so designers can define expectations per empire/faction/fleet template.

## Systems
- **Aggregation pass**: Implement a `CrewAggregationSystem` that recalculates weighted alignments/outlooks when membership changes and writes filtered results into the buffers consumed by compliance.
- **Compliance**: Integrate `Space4XAffiliationComplianceSystem` into the Simulation group ordering, making sure it runs after aggregation and before command systems. Hook breaches to AI/planning pipelines (e.g., convert `ComplianceBreach` into mutiny/desertion command buffers).
- **Suspicion decay routing**: Feed `SuspicionScore` deltas into intel/alert systems so UI/telemetry can surface looming spy exposure.

## Authoring & Tooling
- **Enum registry**: Generate/maintain shared enums for `EthicAxisId`, `OutlookId`, and `AffiliationType` to keep authoring, narrative, and DOTS code aligned.
- **Inspector helpers**: Add custom inspectors or baker validation to guard against doctrine ranges that conflict (e.g., min > max) or crews with more than two fanatic convictions.
- **Sample scenes**: Create a micro test scene with a captain, crew, and faction doctrine to validate mutiny/desertion flows.

## Testing
- **Edit mode**: Add NUnit coverage that feeds synthetic alignments/ethics into `Space4XAffiliationComplianceSystem` and asserts breach type, severity scaling with loyalty, and spy suspicion behavior.
- **Runtime assertions**: Instrument aggregation/compliance with Unity assertions (enabled in dev builds) to catch missing doctrine data or empty affiliation buffers at runtime.

## Integration Hooks
- **AI planner**: Consume `ComplianceBreach` output to spawn behavioral tickets (mutiny state machine, desertion escape paths, independence colony creation).
- **Telemetry**: Extend the registry bridge snapshot to include counts of current breaches and mean suspicion so ops dashboards reflect moral stability.
- **Narrative triggers**: Forward breach events into narrative/quest systems for bark generation and incident scripts.

---

## Module System Framework

### Requirements
- **Physical Slot System**: Carriers need modular component slots (mining rigs, cargo holds, weapons, shields, etc.).
- **Refit Mechanics**: Time-based module swapping with transition states (removing → empty slot → installing → active).
- **Archetype Transitions**: Support smooth archetype changes during module swap (e.g., carrier+miningRig archetype → carrier+weaponMount archetype).
- **Stat Modification**: Modules affect carrier stats (speed, cargo capacity, energy consumption, etc.).
- **Tech/Crew Scaling**: Refit time scales with tech level and crew experience.

### Components & Data
```csharp
// Suggested component structure
struct CarrierModuleSlot : IBufferElementData
{
    public int SlotIndex;
    public ModuleTypeId CurrentModule;
    public ModuleTypeId TargetModule; // During refit
    public float RefitProgress; // 0-1
    public byte SlotSize; // Small/Medium/Large
}

struct ModuleStatModifier : IComponentData
{
    public float SpeedMultiplier;
    public float CargoCapacityMultiplier;
    public float EnergyConsumptionMultiplier;
    // ... etc
}
```

### Systems
- `CarrierModuleRefitSystem`: Processes refit progress, handles archetype transitions.
- `ModuleStatAggregationSystem`: Recalculates carrier stats based on active modules.
- `ModuleBakingSystem`: Converts authoring module definitions to runtime blobs.

### Implementation Details
- **Module as Entity**: Each module instance is a separate entity with parent-child relationship to carrier.
  - Enables per-module health tracking, damage visualization, and repair prioritization.
  - Carrier queries child entities for stat aggregation.
- **Refit Gating**: Refit operations check for `RefitFacility` component on nearby entities (stations, carriers with shipyard modules).
  - Tech level determines which modules can be swapped in field vs requiring station.

### Testing
- Unit tests for module swap sequences (remove → install → activate).
- Archetype transition determinism (same result on rewind).
- Stat aggregation with multiple modules.

---

## Component Degradation Framework

### Requirements
- **Universal Health Tracking**: All components (mining rigs, hull, engines, shields, weapons) track health/durability.
- **Degradation Sources**: Combat damage, hazard exposure, wear-and-tear from usage.
- **Field Repairs**: Limited repairs possible outside stations (capped at 80% health, requires resources).
- **Station Overhauls**: Full repair/refurbishment at stations/colonies.
- **Failure States**: Components become inoperable at 0% health, reducing carrier effectiveness.

### Components & Data
```csharp
struct ComponentHealth : IBufferElementData
{
    public ComponentTypeId ComponentType;
    public float CurrentHealth; // 0-1
    public float MaxFieldRepairHealth; // e.g., 0.8
    public float DegradationRate; // Per tick
    public byte RepairPriority;
}

struct FieldRepairCapability : IComponentData
{
    public float RepairRatePerTick;
    public int ResourceCostPerPoint;
    public bool CanRepairCritical;
}
```

### Systems
- `ComponentDegradationSystem`: Applies degradation based on usage/hazards.
- `FieldRepairSystem`: Processes repair actions outside stations.
- `StationRepairSystem`: Full overhaul at stations/colonies.
- `ComponentFailureSystem`: Disables components at 0% health.

### Implementation Details
- **Per-Instance Health**: Each module entity tracks its own health independently.
- **Repair Queue**: Players can prioritize repair order; repair systems process modules by priority value.
  - Critical systems (life support, propulsion) can be auto-prioritized.
  - Manual override allows players to delay non-critical repairs to conserve resources.

---

## Crew Experience & Skills Framework

### Requirements
- **Skill Levels**: Crews gain experience in mining, combat, hauling, exploration.
- **Hazard Resistance**: Experienced crews resist specific hazard types (radiation, fauna, anomalies).
- **Skill Modifiers**: Crew skills modify extraction rates, refit times, repair effectiveness, combat performance.
- **Passive Growth**: Skills grow through activity (mining missions → mining skill).

### Components & Data
```csharp
struct CrewSkills : IComponentData
{
    public float MiningSkill; // 0-1
    public float CombatSkill;
    public float HaulingSkill;
    public float RepairSkill;
    public float ExplorationSkill;
}

struct HazardResistance : IBufferElementData
{
    public HazardTypeId HazardType;
    public float ResistanceMultiplier; // 0-1, reduces damage/morale penalty
}

struct SkillExperienceGain : IComponentData
{
    public float MiningXP;
    public float CombatXP;
    // Accumulates, then levels up
}
```

### Systems
- `CrewExperienceSystem`: Accumulates XP from activities, triggers level-ups.
- `SkillModifierSystem`: Applies skill multipliers to relevant operations.
- `HazardResistanceSystem`: Reduces hazard impact based on crew experience.

### 2025-02-04 progress (Agent 2)
- Added `CrewSkills`, `SkillExperienceGain`, `HazardResistance`, and `SkillChangeLogEntry` components (buffers live on the mining time spine for replay/telemetry).
- `Space4XCrewExperienceSystem` reads `MiningCommandLogEntry` (gather/pickup) to award XP, updates skill multipliers via a deterministic curve, and logs deltas for rewind.
- `Space4XMinerMiningSystem` now multiplies mining rate by the crew’s `MiningSkill` (up to +50%); `Space4XCrewSkillTelemetrySystem` publishes average skill metrics under `space4x.skills.*`.
- Tests: `Space4XCrewExperienceSystemTests` (XP/skill update + log) and `Space4XMinerMiningSystemTests.MiningSkillAmplifiesMiningTick`.
- Authoring: `Space4XCrewSkillsAuthoring` seeds skills/XP/hazard resistance with clamping; mining time spine records skill snapshots and reapplies skill logs during rewind to keep XP deterministic.
- Hazard: `Space4XHazardMitigationSystem` reduces `HazardDamageEvent` amounts using `HazardResistance` and reports mitigated totals to telemetry; covered by `Space4XHazardMitigationSystemTests`.

---

## Waypoint & Infrastructure Framework

### Requirements
- **Persistent Waypoints**: Spatial entities that mark navigation points, reusable by all vessels.
- **Hyper Highways**: Constructed infrastructure that modifies pathfinding costs (fast travel corridors).
- **Gateways**: Jump gates that enable instant/rapid transit between connected points.
- **Spatial Integration**: Waypoints registered in spatial grid for fast queries.

### Components & Data
```csharp
struct Waypoint : IComponentData
{
    public float3 Position;
    public WaypointTypeId Type; // Navigation, Highway Node, Gateway
    public Entity OwnerFaction;
    public bool IsPublic;
}

struct HyperHighway : IComponentData
{
    public Entity StartWaypoint;
    public Entity EndWaypoint;
    public float SpeedMultiplier; // 2x, 5x, 10x travel speed
    public float MaintenanceCost;
}

struct Gateway : IComponentData
{
    public Entity DestinationGateway;
    public float JumpCost; // Energy/resource cost
    public float CooldownTime;
}
```

### Systems
- `WaypointRegistrationSystem`: Registers/unregisters waypoints in spatial grid.
- `PathfindingSystem`: Routes vessels through highways/gateways when beneficial.
- `InfrastructureMaintenanceSystem`: Degrades highways/gates without upkeep.

### Implementation Details
- **Destructible Infrastructure**: Players can destroy enemy highways and gateways via combat/sabotage.
- **Reconfiguration**: Captured infrastructure can be reconfigured to new ownership (change OwnerFaction).
- **Maintenance Requirements**: Highways and gates require periodic resource deliveries to remain operational.
  - Maintenance entity tracks required resources (e.g., polymers, power cells) and consumption rate.
  - Unmaintained infrastructure degrades and eventually becomes inoperable.
  - Resource types and quantities stored in blob data per infrastructure tier.

---

## Supply & Demand Economy Framework

### Requirements
- **Dynamic Pricing**: Station prices vary based on local supply/demand.
- **Flow Tracking**: Stations track inflow/outflow rates per resource type.
- **Price Recalculation**: Burst jobs update prices per tick based on inventory changes.
- **Trade Opportunities**: Price differentials drive automated trade routes.

### Components & Data
```csharp
struct StationInventory : IBufferElementData
{
    public ResourceTypeId ResourceType;
    public float CurrentStock;
    public float InflowRate; // Per tick
    public float OutflowRate; // Per tick
    public float BasePrice;
    public float CurrentPrice; // Adjusted by supply/demand
}

struct SupplyDemandModifier : IComponentData
{
    public float PriceElasticity; // How much prices change per stock level
    public float MinPriceMultiplier; // 0.1x (glut)
    public float MaxPriceMultiplier; // 10x (scarcity)
}
```

### Systems
- `InventoryFlowTrackingSystem`: Updates inflow/outflow rates.
- `DynamicPricingSystem`: Recalculates prices based on stock levels.
- `TradeOpportunitySystem`: Identifies profitable trade routes for AI haulers.

---

## Resource Spoilage Framework

### Requirements
- **FIFO Inventory**: First-in-first-out consumption to prioritize old stock.
- **Degradation Rates**: 2% per tick for consumables (food, fuel, organics).
- **Resource Type Filtering**: Only consumables degrade; ore/metals/electronics are durable.
- **Timestamp Tracking**: Each resource batch has creation tick for age calculation.

### Components & Data
```csharp
struct InventoryBatch : IBufferElementData
{
    public ResourceTypeId ResourceType;
    public float Quantity;
    public uint CreationTick;
    public float SpoilageRate; // 0 for durables, 0.02 for consumables
}

struct SpoilageSettings : IComponentData
{
    public float ConsumableSpoilageRate; // 0.02 default
    public bool EnableSpoilage;
}
```

### Systems
- `ResourceSpoilageSystem`: Applies degradation to consumables based on age.
- `FIFOConsumptionSystem`: Prioritizes oldest batches for consumption.

---

## Fleet Interception & Rendezvous Framework

**Status:** Broadcast/queue/systems and edit-mode tests landed (FleetInterceptSystemsTests); telemetry keys/logs available for rewind bindings.

### Requirements
- **Position Broadcasting**: Moving fleets broadcast position/velocity for interception.
- **Predictive Pathfinding**: Haulers calculate intercept courses for moving targets.
- **Tech-Gated**: High-tech enables dynamic interception; low-tech uses static waypoint rendezvous.
- **Spatial Queries**: Use spatial grid to find fleets in range for resupply.

### Components & Data
```csharp
struct FleetMovementBroadcast : IComponentData
{
    public float3 Position;
    public float3 Velocity;
    public uint LastUpdateTick;
    public bool AllowsInterception; // Tech-gated
}

struct InterceptCourse : IComponentData
{
    public Entity TargetFleet;
    public float3 InterceptPoint;
    public uint EstimatedInterceptTick;
}
```

### Systems
- `FleetBroadcastSystem`: Updates fleet position/velocity broadcasts.
- `InterceptPathfindingSystem`: Calculates intercept courses for haulers.
- `RendezvousCoordinationSystem`: Manages static waypoint meetups (low-tech).

### Status / Notes (2025-02-05)
- Authoring: `Space4XFleetInterceptAuthoring` seeds `FleetMovementBroadcast` + optional `InterceptCourse/InterceptCapability` and now adds `SpatialGridResidency` for spatial queries.
- Runtime: `FleetBroadcastSystem` (FixedStep) stamps `LastUpdateTick` from `TimeState`, respects `RewindState` playback, carries velocity from `FleetKinematics`, and mirrors positions into `SpatialGridResidency.LastPosition`.
- Intercept queue: `InterceptPathfindingSystem` sorts requests by `Priority`, `RequestTick`, `Requester.Index` and writes `FleetInterceptCommandLogEntry` + telemetry counters (`space4x.intercept.*`). Rendezvous system reapplies live positions when interception is disabled or gated by tech.
- Spatial query path: `FleetInterceptRequestSystem` (FixedStep) selects nearest broadcast fleets within ~250u for entities with `InterceptCapability`, writing `InterceptRequest` (priority = tech tier, request tick) into the queue. This keeps request ordering deterministic before path calculation.
- Tests: `FleetInterceptSystemsTests` now cover broadcast tick/velocity/residency updates and request → path → command-log flow, ensuring nearest selection and command log entries are written.
- `Space4XFleetInterceptQueue` holds `InterceptRequest` (sorted by Priority, RequestTick, EntityIndex) and logs `FleetInterceptCommandLogEntry` for rewind; telemetry keys: `space4x.intercept.attempts`, `space4x.intercept.rendezvous`, `space4x.intercept.lastTick`.

---

## Tech Diffusion Framework

### Requirements
- **Gradual Propagation**: Tech upgrades take time to reach all entities in faction.
- **Diffusion Waves**: Tech spreads from core worlds to frontier in waves.
- **Upgrade Gating**: Entities require facility upgrades to adopt new tech.
- **Tech Levels**: Entities track current tech level per domain (mining, combat, hauling).

### Components & Data
```csharp
struct TechLevel : IComponentData
{
    public byte MiningTech; // 0-10
    public byte CombatTech;
    public byte HaulingTech;
    public byte ProcessingTech;
    public uint LastUpgradeTick;
}

struct TechDiffusionState : IComponentData
{
    public Entity SourceEntity; // Core world that researched it
    public float DiffusionProgress; // 0-1
    public uint DiffusionStartTick;
}
```

### Systems
- `TechDiffusionSystem`: Propagates tech upgrades across faction over time.
- `TechUpgradeApplicationSystem`: Applies new recipes/capabilities when tech levels up.

### Implementation Details
- **Hybrid Diffusion**: Tech spreads using both distance and time:
  - **Distance Component**: Spatial proximity to core world reduces diffusion delay.
  - **Time Component**: Base diffusion delay per tech tier (higher tiers take longer).
  - **Tech Level Offset**: Higher faction tech level reduces overall diffusion time.
  - **Formula**: `DiffusionTime = BaseTier * (Distance / MaxDistance) / TechLevelMultiplier`
- **Spatial Queries**: Use spatial grid to calculate distance from core worlds to frontier entities.
- **Acceleration**: Resource investment or dedicated "research vessels" can reduce diffusion time for specific entities.

---

## Time Control Framework

### Requirements
- **Time Scaling**: Players control simulation speed (pause, 1x, 2x, 5x, 10x).
- **Real-Time Sync**: Gameplay timers (5s extraction tick) match both real-time and sim-time.
- **Deterministic Scaling**: Time controls don't break determinism or rewind.

### Integration
- Already supported by PureDOTS `TimeState` singleton.
- Ensure Space4X systems read `TimeState.TimeScale` for speed adjustments.
- **Time-Independent Systems**: UI updates and camera controls ignore time scaling, always run at real-time.
  - Mark systems with `[TimeIndependent]` attribute or check for `PresentationSystemGroup` membership.
  - Input sampling, camera movement, and HUD updates use `UnityEngine.Time.deltaTime` instead of `TimeState.DeltaTime`.

---

## Crew Breeding/Cloning Framework (Deferred)

**Status:** Data + authoring + gated stubs landed (CrewGrowthSettings/State/Telemetry, Space4XCrewGrowthAuthoring, Space4XCrewGrowthSystem). Defaults remain disabled; stub logs telemetry only.

### Requirements
- **Passive Growth**: Crews expand over time without player intervention (if policies allow).
- **Breeding**: Organic crew reproduction (slow, policy-dependent).
- **Cloning**: Synthetic crew creation (fast, resource-cost, tech-gated).
- **Policy Integration**: Faction doctrines enable/disable breeding/cloning.

### Components & Data
```csharp
struct CrewGrowthSettings : IComponentData
{
    public bool BreedingEnabled;
    public bool CloningEnabled;
    public float BreedingRate; // Crew per tick
    public float CloningCost; // Resources per clone
}
```

### Status
- **Deferred**: Implement after core loops stabilize.

---

## Mining Deposit & Harvest Nodes Framework

### Requirements
- **Deposit Entities**: Serializable deposit entities with richness, type, regeneration rate, hazard level.
- **Harvest Nodes**: Buffer of attachment points on each deposit for mining vessels to dock.
- **Node Queuing**: Deterministic queue system when more carriers than available nodes.
- **Regeneration**: Type-specific richness regeneration for certain deposit types (gas clouds, renewable resources).

### Components & Data
```csharp
struct MiningDeposit : IComponentData
{
    public float Richness; // 0-5.0
    public float RegenerationRate; // 0-0.5 per tick
    public DepositTypeId DepositType; // Ore, Gas, Ice, etc.
    public byte HazardLevel; // 0-10
    public byte MaxHarvestNodes;
}

struct HarvestNode : IBufferElementData
{
    public float3 LocalPosition; // Offset from deposit center
    public Entity AttachedCarrier; // Entity.Null if free
    public ExtractionState State; // Idle, Extracting, Depleted
    public uint AttachmentTick;
}

struct HarvestNodeRequest : IComponentData
{
    public Entity TargetDeposit;
    public uint RequestTick;
    public byte Priority;
}
```

### Systems
- `DepositRegenerationSystem`: Accumulates richness for regenerating deposits over time.
- `HarvestNodeAssignmentSystem`: Processes carrier requests, assigns to free nodes deterministically.
- `HarvestNodeQueueSystem`: Manages waiting carriers when all nodes occupied.
- `DepositDepletionSystem`: Marks depleted deposits, triggers carrier suspend/reassignment.

### Technical Notes
- **Deterministic Assignment**: Sort requests by (Priority, RequestTick, EntityIndex) before processing.
- **Spatial Registration**: Deposits registered in spatial grid for carrier proximity queries.

---

## Implementation Priorities

### Phase 1 (Foundational)
1. Module System Framework
2. Component Degradation Framework
3. Mining Deposit & Harvest Nodes Framework
4. Waypoint & Infrastructure Framework

### Phase 2 (Economy)
5. Supply & Demand Economy Framework
6. Resource Spoilage Framework
7. Tech Diffusion Framework

### Phase 3 (Advanced)
8. Fleet Interception Framework
9. Crew Experience & Skills Framework
10. Crew Breeding/Cloning (deferred)

---

## Existing Alignment Systems (Original Request)

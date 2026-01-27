# System Integration & Neutrality Guidelines

## Overview

PureDOTS core systems must remain theme-neutral and reusable across multiple games (Godgame, Space4x, and future projects). This document outlines integration contracts, naming conventions, and neutrality enforcement.

## Content Neutrality

### Naming Conventions

- **Avoid theme-specific terms**: Use generic names like `ResourceNode`, `StorageBuilding`, `WorkerUnit` instead of `Tree`, `Storehouse`, `Villager`
- **Prefer behavior-based names**: `ResourceGatherer`, `ResourceConsumer`, `MobileUnit` instead of domain-specific nouns
- **Namespace organization**: Keep shared systems in `PureDOTS.Runtime.*`, game-specific code in game assemblies

### Component Design

- **Data-driven configuration**: Use blob assets and ScriptableObjects for game-specific behavior
- **Marker components**: Use tags (e.g., `FlowFieldAgentTag`) instead of type hierarchies
- **Extension points**: Provide interfaces/events for games to hook into shared systems

### Example Violations

❌ **Bad**: `VillagerJobSystem`, `TreeGrowthSystem`, `StorehouseInventoryComponent`  
✅ **Good**: `WorkerJobSystem`, `GrowthNodeSystem`, `StorageInventoryComponent`

## Integration Contracts

### Registry Pattern

All game entities should register themselves via registry systems:

```csharp
// Game-specific code adds component
entityManager.AddComponent<ResourceNodeTag>(entity);

// Registry system automatically picks it up
// No game-specific code in registry system
```

### Spatial Grid Integration

- Entities with `SpatialIndexedTag` are automatically indexed
- Games query via `SpatialGridQuerySystem` using generic position queries
- No game-specific logic in spatial systems

### Flow Field Navigation

- Agents add `FlowFieldAgentTag` to participate
- Goals add `FlowFieldGoalTag` with layer ID
- Games configure layers via bootstrap profiles
- Steering blends with flow fields automatically

## Bootstrap Pattern

### Profile-Based Initialization

1. Create `BootstrapProfile` ScriptableObject asset
2. Configure world bounds, spawn counts, initial entities
3. Profile consumed by `BootstrapSystem` during scene load
4. All configuration data-driven, no hardcoded game logic

### Example

```csharp
[CreateAssetMenu]
public class VillageBootstrapProfile : BootstrapProfile
{
    public int InitialVillagerCount = 10;
    public int StorehouseCount = 2;
    
    public override void ApplyBootstrap(EntityManager em)
    {
        // Seed entities using generic spawners
        // No game-specific systems called
    }
}
```

## Validation

### Lint Checks

- Scan `PureDOTS.Runtime.*` namespaces for theme-specific terms
- Flag components that reference game-specific types
- Enforce namespace boundaries (games can't modify PureDOTS.Runtime)

### Testing

- Shared systems must work with mock/test entities
- Games provide test fixtures using `RegistryMocks`
- Integration tests validate neutrality contracts

## Spatial Query Integration

### Query Helpers

Use `SpatialQueryHelper` for position-based queries (radius, nearest, AABB):

```csharp
// Get spatial grid buffers
var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
var cellRanges = SystemAPI.GetBuffer<SpatialGridCellRange>(gridEntity);
var gridEntries = SystemAPI.GetBuffer<SpatialGridEntry>(gridEntity);
var spatialConfig = SystemAPI.GetSingleton<SpatialGridConfig>();

// Query nearby entities
var results = new NativeList<Entity>(Allocator.Temp);
SpatialQueryHelper.GetEntitiesWithinRadius(
    position, radius, spatialConfig, cellRanges, gridEntries, ref results);
```

### When to Use Spatial vs. Registry Queries

- **Spatial queries**: Position/radius-based searches (AI sensors, targeting, selection)
- **Registry queries**: Type/category-based searches (job assignment, resource gathering)
- **Combined**: Spatial query to narrow candidates, then filter by registry/component

See `Docs/Guides/SpatialQueryUsage.md` for detailed examples.

## Reusable AI Modules

PureDOTS provides a modular AI framework in `AISystemGroup` that can be consumed by any entity type (villagers, ships, drones, NPCs) via configuration components.

### Module Pipeline

The AI pipeline consists of four stages executed in order:

1. **Sensors** (`AISensorUpdateSystem`): Samples spatial grid around agents, categorizes entities
2. **Scoring** (`AIUtilityScoringSystem`): Evaluates actions using utility curves and sensor readings
3. **Steering** (`AISteeringSystem`): Computes movement direction toward selected targets
4. **Task Resolution** (`AITaskResolutionSystem`): Emits commands to domain-specific systems

### Component Contracts

**Sensor Module**:
- `AISensorConfig` - Update interval, range, max results, categories to detect
- `AISensorState` - Tracks elapsed time and last sample tick
- `AISensorReading` (buffer) - Cached sensor results with normalized scores

**Scoring Module**:
- `AIBehaviourArchetype` - Blob reference containing utility curve definitions
- `AIUtilityState` - Best action index and score from last evaluation
- `AIActionState` (buffer, optional) - Per-action scores for debugging

**Steering Module**:
- `AISteeringConfig` - Max speed, acceleration, responsiveness, degrees of freedom
- `AISteeringState` - Desired direction, linear velocity, last sampled target
- `AITargetState` - Current target entity/position from scoring stage

**Task Resolution**:
- `AICommand` (buffer on singleton) - Commands emitted for domain systems to consume

### Integration Pattern

Entities opt into AI modules by adding configuration components:

```csharp
// Add sensor config
entityManager.AddComponent(entity, new AISensorConfig
{
    UpdateInterval = 0.5f,
    Range = 10f,
    MaxResults = 8,
    PrimaryCategory = AISensorCategory.ResourceNode,
    SecondaryCategory = AISensorCategory.Storehouse
});

// Add sensor state and buffer
entityManager.AddComponent(entity, new AISensorState());
entityManager.AddBuffer<AISensorReading>(entity);

// Add behaviour archetype (blob baked from ScriptableObject)
entityManager.AddComponent(entity, new AIBehaviourArchetype
{
    UtilityBlob = utilityBlobReference
});

// Add utility state
entityManager.AddComponent(entity, new AIUtilityState());

// Add steering config and state
entityManager.AddComponent(entity, new AISteeringConfig
{
    MaxSpeed = 5f,
    Acceleration = 10f,
    Responsiveness = 0.1f,
    DegreesOfFreedom = 2 // 2D planar movement
});
entityManager.AddComponent(entity, new AISteeringState());
entityManager.AddComponent(entity, new AITargetState());
```

Domain systems consume `AICommand` buffer to execute actions:

```csharp
var commands = SystemAPI.GetBuffer<AICommand>(queueEntity);
for (int i = 0; i < commands.Length; i++)
{
    var cmd = commands[i];
    // Execute action based on cmd.ActionIndex
    // Use cmd.TargetEntity and cmd.TargetPosition
}
```

### Customization Points

- **Sensor Categories**: Add new categories to `AISensorCategory` enum (reserve 240-255 for custom)
- **Utility Curves**: Define in `AIUtilityArchetypeBlob` blob assets (baked from ScriptableObjects)
- **Steering Behavior**: Adjust weights and responsiveness via `AISteeringConfig`
- **Action Execution**: Domain systems interpret `AICommand.ActionIndex` based on their needs

### Migration Notes

- Existing villager systems (`VillagerAISystem`) can coexist with shared modules
- Gradual migration: Add AI module components alongside existing logic, compare results
- Once validated, remove custom AI logic and consume `AICommand` buffer instead

## Slice Ownership & Governance

PureDOTS is organized into meta-system slices to clarify ownership and maintain clear boundaries. Each slice has designated responsibilities and contact cadence.

### Slice Definitions

#### [Runtime Core]
**Owners**: Core DOTS systems team  
**Responsibilities**:
- Core DOTS systems (time, rewind, resource gathering, villager jobs)
- Registry infrastructure and directory system
- Spatial services and query helpers
- Navigation and pathfinding systems
- Environment grid systems (moisture, temperature, wind, sunlight)
- Input routing and hand interaction systems
- Presentation bridge core systems

**Contact Cadence**: Weekly async reviews, checkpoint at end of each phase

#### [Data Authoring]
**Owners**: Authoring/Editor tooling team  
**Responsibilities**:
- ScriptableObject definitions and validation
- Baker implementations
- Editor inspectors and gizmos
- Asset migration scripts
- Authoring workflow documentation

**Contact Cadence**: Monthly review, ad-hoc for breaking changes

#### [Tooling/Telemetry]
**Owners**: Observability/QA team  
**Responsibilities**:
- Debug overlays and HUD systems
- Telemetry hooks and instrumentation
- Console commands and debugging utilities
- Performance profiling harness
- Frame timing and allocation diagnostics

**Contact Cadence**: Weekly sync with Runtime Core, monthly review

#### [QA/Validation]
**Owners**: Testing/QA team  
**Responsibilities**:
- Test suite maintenance (unit, integration, playmode)
- Deterministic replay harness
- Performance benchmarks and stress tests
- CI automation and test coverage reporting
- Integration test checklist and validation scenarios

**Contact Cadence**: Weekly sync, checkpoint reviews before releases

### Governance Process

**Monthly Review Per Slice**:
- Review interfaces for stability and neutrality
- Identify cross-slice dependencies and potential conflicts
- Update documentation and truth sources
- Surface blockers and integration risks

**Onboarding Notes**:
- New features should be tagged with `[Runtime Core]`, `[Data Authoring]`, `[Tooling]`, or `[QA]` in TODO items
- Clarify which slice new features belong to before implementation
- Request new shared utilities through slice owners to avoid violating neutrality

**CI/Testing Responsibilities**:
- **Runtime Core**: Maintains stress tests for core systems
- **Tooling**: Maintains telemetry pipelines and profiling automation
- **QA**: Maintains integration test suites and CI configuration
- **Data Authoring**: Maintains asset validation and editor tooling tests

## Hand & Miracle Integration

### Centralized Event Stream

Hand interaction events are centralized through `DivineHandEvent` buffer and `DivineHandEventBridge` MonoBehaviour:
- **DOTS side**: `DivineHandSystem` emits events to `DivineHandEvent` buffer
- **Presentation side**: `DivineHandEventBridge` reads buffer and dispatches Unity Events
- **VFX/Audio**: Subscribe to `DivineHandEventBridge` events (single stream, no duplication)

### Shared State Contracts

- `HandInteractionState`: Synchronized state consumed by resource and miracle systems
- `ResourceSiphonState`: Aggregated siphon state for resource and miracle chains
- `HandInputRouterSystem`: Single priority table for all hand interactions (resources, miracles, UI)

### Manual Test Scenarios

See `Docs/QA/IntegrationTestChecklist.md` for detailed manual test scenarios covering:
- Hand holding resource + miracle token (deterministic resolver)
- Hand dumps to storehouse after miracle charge
- Centralized feedback events (VFX/audio)

## Cross-References

- `Docs/TODO/SystemIntegration_TODO.md` - Integration task tracking
- `Docs/DesignNotes/PresentationBridgeContracts.md` - Visual representation contracts
- `Docs/DesignNotes/RegistryHotColdSplits.md` - Registry data layout guidelines
- `Docs/DesignNotes/SoA_Expectations.md` - SoA layout guidelines for all high-volume systems
- `Docs/DesignNotes/ThreadingAndScheduling.md` - Threading policy and hot/cold execution strategy
- `Docs/DesignNotes/HistoryBufferPatterns.md` - Double-buffering and history capture patterns
- `Docs/Guides/SpatialQueryUsage.md` - Spatial query helper usage guide
- `Docs/QA/IntegrationTestChecklist.md` - Manual integration test scenarios

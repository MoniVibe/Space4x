# Threading & Scheduling Strategy

## Overview

This document defines PureDOTS threading policies, job worker configuration, Burst compilation rules, and hot vs. cold execution path organization. It complements `PlatformPerformance_TruthSource.md` with actionable scheduling guidance.

## Job Worker Policy

### Default Configuration

- **Default Worker Count**: `JobsUtility.JobWorkerCountHint` (Unity's recommended count based on CPU cores)
- **Bootstrap Override**: Can be configured via `PureDotsWorldBootstrap` during world initialization
- **Tuning Guidelines**: Adjust based on workload characteristics (see scenarios below)

### Worker Count Scenarios

#### High AI Density (Villagers, Pathfinding)
- **Configuration**: Increase worker count by 2 (but keep ≤ logical cores)
- **Rationale**: Villager pathfinding and AI decision-making benefit from parallel execution
- **Example**: 8-core CPU → 6 workers (leave 2 cores for main thread + OS)

#### Physics-Heavy Scenes
- **Configuration**: Worker count = physical cores - 1
- **Rationale**: Leave headroom for Burst-compiled physics jobs and main thread
- **Example**: 8-core CPU → 7 workers

#### Low-End Hardware / Mobile
- **Configuration**: Expose setting in runtime config to reduce workers
- **Rationale**: Prevents contention and thermal throttling
- **Default**: Physical cores / 2 (e.g., 4-core → 2 workers)
- **Override**: Allow user/designer configuration via `PureDotsRuntimeConfig`

#### Balanced Workload (Default)
- **Configuration**: Use Unity's default (`JobWorkerCountHint`)
- **Rationale**: Balanced performance across all system types
- **No manual override needed**

### Configuration Example

```csharp
// In PureDotsWorldBootstrap or runtime config system
var workerCount = JobsUtility.JobWorkerCountHint;
if (config.OverrideWorkerCount > 0)
{
    workerCount = math.min(config.OverrideWorkerCount, SystemInfo.processorCount);
}
JobsUtility.JobWorkerCount = workerCount;
```

## Burst Compilation Rules

### Enforcement Policy

- **Development**: `BurstCompilerOptions.CompileSynchronously = true`
  - Catches Burst compile errors immediately
  - Slows iteration but prevents runtime failures
  - Enabled by default in development builds

- **Release**: `BurstCompilerOptions.CompileSynchronously = false`
  - Faster startup, background compilation
  - Suitable for final builds where compile errors should be caught earlier

### Burst Compatibility Requirements

All hot-path systems must be Burst-compatible:

1. **No Managed Types**: Avoid `string`, `object`, delegates, managed generics
2. **Blittable Structs**: All component/struct fields must be primitive or blittable
3. **No Reflection**: Avoid `GetType()`, `Activator`, dynamic dispatch in jobs
4. **Native Containers**: Use `NativeArray`, `NativeList`, `BlobAssetReference` instead of managed collections
5. **Fixed Strings**: Use `FixedString64Bytes`, `FixedString128Bytes` instead of `string`

### Burst Compile Checklist

When adding new systems/jobs:

- [ ] All job structs are `[BurstCompile]`
- [ ] No managed types in job signatures
- [ ] Component structs are blittable (no `string`, `object`, delegates)
- [ ] `NativeArray`/`NativeList` use `Allocator.TempJob` or `Allocator.Persistent`
- [ ] Blob assets used for large read-only data
- [ ] `[BurstCompile]` attribute on hot-path `ISystem` implementations
- [ ] Test with Burst enabled (not just Editor-only builds)

### Burst Troubleshooting

- **Compile Errors**: Check `Library/Bee/tmp/il2cppOutput/BurstDebugInformation_DoNotShip/`
- **Generic Instantiation Errors**: Add explicit static constructors or dummy usage
- **Missing Types**: Ensure types are in Burst-compatible assemblies (no editor references)
- **Debugging**: Temporarily disable Burst (`BurstCompilerOptions.EnableBurstCompilation = false`) to isolate issues

## Hot vs. Cold Execution Paths

### System Group Classification

#### Hot Path Groups (Critical, Run Every Frame)

**FixedStepSimulationSystemGroup**:
- Time tick system
- Physics simulation
- Core gameplay loops (villager AI, resource gathering)

**SpatialSystemGroup**:
- Spatial grid rebuild (`OrderFirst`)
- Spatial queries and updates

**GameplaySystemGroup** (and sub-groups):
- `VillagerSystemGroup` - Villager AI, movement, jobs
- `ResourceSystemGroup` - Resource gathering, deposit
- `CombatSystemGroup` - Combat logic
- `ConstructionSystemGroup` - Building logic

**EnvironmentSystemGroup**:
- Environment grid updates (moisture, temperature, wind)

**Priority**: These groups must complete within frame budget (< 16ms for 60 FPS)

#### Cold Path Groups (Background, Can Throttle)

**LateSimulationSystemGroup**:
- History snapshot capture
- Registry instrumentation
- Telemetry logging
- Registry directory updates

**PresentationSystemGroup**:
- Visual updates (can skip frames)
- UI updates
- Debug overlays

**InitializationSystemGroup**:
- Bootstrap systems (run once, then disabled)
- One-time setup

**Priority**: These groups can be throttled or skipped during heavy load

### Throttling Strategy

**Throttle Mechanisms**:
1. **Frame Skip**: Skip cold systems every N frames (e.g., history snapshots every 3 frames)
2. **Time Budget**: Limit cold system execution time (e.g., max 2ms per frame)
3. **Conditional Execution**: Only run when needed (e.g., telemetry only when enabled)

**Example Throttling**:
```csharp
// In LateSimulationSystemGroup system
if (timeState.Tick % 3 != 0)  // Skip every 2 out of 3 frames
{
    return;
}

// Or limit execution time
var startTime = Time.realtimeSinceStartup;
// ... do work ...
if (Time.realtimeSinceStartup - startTime > 0.002f)  // 2ms budget
{
    break;  // Exit early
}
```

### Hot/Cold System Catalog

#### Hot Systems (Must Complete Every Frame)

| System Group | Key Systems | Update Frequency |
|-------------|-------------|------------------|
| `TimeSystemGroup` | `TimeTickSystem`, `RewindCoordinatorSystem` | Every frame |
| `SpatialSystemGroup` | `SpatialGridBuildSystem` | Every frame (OrderFirst) |
| `VillagerSystemGroup` | `VillagerAISystem`, `VillagerMovementSystem` | Every frame |
| `ResourceSystemGroup` | `ResourceGatheringSystem`, `ResourceDepositSystem` | Every frame |
| `EnvironmentSystemGroup` | `MoistureEvaporationSystem`, `TemperatureUpdateSystem` | Every frame (or cadence) |

#### Cold Systems (Can Throttle)

| System Group | Key Systems | Throttle Strategy |
|-------------|-------------|-------------------|
| `LateSimulationSystemGroup` | `HistorySnapshotSystem` | Skip every 2-3 frames |
| `LateSimulationSystemGroup` | `RegistryInstrumentationSystem` | Skip every 5 frames |
| `LateSimulationSystemGroup` | `TelemetryStreamSystem` | Conditional (when enabled) |
| `PresentationSystemGroup` | `DebugDisplaySystem` | Skip when disabled |
| `PresentationSystemGroup` | Visual sync systems | Skip if no changes |

## Job Scheduling Best Practices

### Dependency Management

- **Explicit Dependencies**: Use `state.Dependency` to chain jobs
- **Avoid Full Sync Points**: Minimize `Complete()` calls on main thread
- **Parallel Execution**: Use `ScheduleParallel()` when possible (no shared writes)

### Allocator Policy

- **`Allocator.Temp`**: Frame-scoped allocations (disposed at end of frame)
- **`Allocator.TempJob`**: Job-scoped allocations (disposed when job completes)
- **`Allocator.Persistent`**: Long-lived allocations (explicit disposal required)
- **Pooled Containers**: Use `NativeContainerPools` for frequent allocations

### Job Granularity

- **Chunk-Based Jobs**: Prefer `IJobEntity` for entity processing (automatic chunking)
- **Batch Jobs**: Group related work into single jobs to reduce scheduling overhead
- **Avoid Micro-Jobs**: Don't create jobs for trivial work (< 100 entities)

### Example: Proper Job Scheduling

```csharp
[BurstCompile]
public partial struct VillagerMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var timeState = SystemAPI.GetSingleton<TimeState>();
        
        // Schedule parallel job (no shared writes)
        var job = new UpdateMovementJob
        {
            DeltaTime = timeState.FixedDeltaTime
        };
        
        // Chain dependency (if previous job exists)
        state.Dependency = job.ScheduleParallel(state.Dependency);
        
        // Don't Complete() here - let Unity schedule next system
    }
}
```

## Thread Affinity

### Main Thread Requirements

**Must Run on Main Thread**:
- World bootstrap and system creation
- EntityCommandBuffer playback (from jobs)
- Presentation updates (rendering, UI)
- Editor-only systems

**Can Run on Worker Threads**:
- All Burst-compiled jobs
- Spatial grid rebuilds
- Registry updates
- AI decision-making
- Pathfinding calculations

### Worker Thread Safety

- **Read-Only Access**: Multiple workers can read same data concurrently
- **Write Access**: Only one worker writes to a chunk/entity at a time
- **Buffer Access**: Use `NativeArray` for shared read-only data, `DynamicBuffer` for per-entity writes
- **Singleton Access**: Read-only from jobs (`ComponentLookup<T>` with `true` flag)

## Performance Monitoring

### Instrumentation

- **Frame Timing**: `FrameTimingStream` captures per-system-group timings
- **Job Scheduling**: Optional logs for worker utilization (see `PlatformPerformance_TruthSource.md`)
- **Telemetry**: `TelemetryStream` aggregates metrics across systems

### Profiling Guidelines

1. **Unity Profiler**: Use CPU Usage view to identify hot systems
2. **Burst Inspector**: Check Burst compilation status and warnings
3. **Job Debugger**: Verify job dependencies and worker utilization
4. **Frame Timing**: Monitor `FrameTimingStream` for per-group budgets

### Budgets

**Target Frame Times** (60 FPS):
- Hot path total: < 12ms (leave 4ms headroom)
- Cold path total: < 4ms (can skip/throttle)
- Presentation: < 4ms (can skip frames)

**Per-System Budgets** (from `PerformanceProfiles.md`):
- Spatial grid rebuild: < 1ms
- Flow field build: < 3ms
- Villager AI/Movement: < 2ms (for 10k villagers)
- Registry updates: < 0.5ms (all registries)

## Configuration & Overrides

### Runtime Configuration

Expose worker count and throttling via `PureDotsRuntimeConfig`:

```csharp
[Serializable]
public class ThreadingSettings
{
    public int OverrideWorkerCount = 0;  // 0 = use default
    public bool EnableColdThrottling = true;
    public int HistorySnapshotCadence = 3;  // Every N frames
    public float ColdPathTimeBudget = 0.002f;  // 2ms
}
```

### Editor Configuration

- **Burst Settings**: `Project Settings → Burst AOT Settings → Compile Synchronously`
- **Job Settings**: `Project Settings → Jobs → Worker Thread Count` (overridden at runtime)
- **Profiling**: Enable `Development Build` + `Autoconnect Profiler` for analysis

## Cross-References

- `Docs/TruthSources/PlatformPerformance_TruthSource.md` - IL2CPP/AOT and Burst guidelines
- `Docs/TruthSources/RuntimeLifecycle_TruthSource.md` - System group ordering
- `Docs/DesignNotes/SystemExecutionOrder.md` - Detailed system ordering
- `Docs/QA/PerformanceProfiles.md` - Performance budgets and targets
- `Docs/DesignNotes/SoA_Expectations.md` - Memory layout guidelines

## Checklist for New Systems

When adding a new system:

- [ ] Determine hot vs. cold classification
- [ ] Place in appropriate system group
- [ ] Add `[BurstCompile]` if hot-path
- [ ] Verify Burst compatibility (no managed types)
- [ ] Use appropriate allocator (`TempJob` for short-lived, `Persistent` for long-lived)
- [ ] Chain dependencies via `state.Dependency`
- [ ] Add throttling if cold-path (skip frames or time budget)
- [ ] Update this document if new group/throttling strategy needed



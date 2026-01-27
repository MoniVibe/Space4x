# Foundational Settings Sandbox (Engine-Level Tweaking)

## Expanded Concept

**Beyond Entity Tweaking**: While entity/component inspection lets you tweak **what exists**, foundational settings let you tweak **how the engine works**.

**Core Idea**: Every fundamental rule, constant, and configuration in PureDOTS should be adjustable at runtime to observe behavioral changes at the deepest level.

**Philosophy**: "If you can't tweak it live, you don't truly understand it."

---

## Foundational Categories

### 1. Spatial Partitioning & Grid Configuration

#### Spatial Hash Settings
```
Purpose: Controls how entities are organized in space for efficient queries

Tweakable Parameters:

CellSize (float, 1-1000 meters):
  - Size of spatial hash grid cells
  - Smaller = more precise, more cells, more memory
  - Larger = less precise, fewer cells, less memory
  - Default: Godgame = 10m, Space4X = 1000m

  Live Effect:
    Increase CellSize 10m → 100m:
      → Fewer cells, faster insertion
      → Broader queries (less precise neighbor finding)
      → Less memory usage
      → Pathfinding may be coarser

MaxEntitiesPerCell (int, 10-10000):
  - Expected entity density per cell
  - Used for capacity pre-allocation
  - Too low = frequent reallocations
  - Too high = wasted memory

  Live Effect:
    Increase 100 → 1000:
      → More memory allocated upfront
      → Fewer reallocations during swarms
      → Better performance in dense areas

QueryRadius (float, 1-5000 meters):
  - Default search radius for "find nearby" queries
  - Affects patrol detection range, collision checks

  Live Effect:
    Increase 50m → 500m:
      → Patrols detect enemies from farther away
      → More entities returned per query (slower)
      → Different tactical gameplay

RebuildFrequency (int, 1-60 ticks):
  - How often to rebuild spatial hash
  - Every tick = accurate but expensive
  - Every 10 ticks = faster but stale data

  Live Effect:
    Increase 1 → 10 ticks:
      → 10x faster (amortized cost)
      → Queries may return outdated positions
      → Fast-moving entities "lag" in hash
```

#### Octree/BVH Settings (3D Space4X)
```
MaxDepth (int, 4-16):
  - Maximum tree depth
  - Deeper = more precise subdivision
  - Shallower = coarser, but faster

  Live Effect:
    Increase depth 8 → 12:
      → More precise culling (fewer false positives)
      → Slower construction (more nodes)
      → Better for sparse, large-scale fleets

LeafCapacity (int, 8-256):
  - Max entities per leaf node before split
  - Higher = fewer nodes, coarser queries
  - Lower = more nodes, finer queries

  Live Effect:
    Decrease 64 → 16:
      → More splits, deeper tree
      → Faster queries (fewer entities per leaf)
      → Slower insertions (more split operations)
```

---

### 2. Physics Engine Configuration

#### Core Physics Parameters
```
Gravity (float3, -100 to +100 m/s²):
  - Godgame default: (0, -9.81, 0)
  - Space4X default: (0, 0, 0)
  - Can be non-uniform (e.g., tidal forces)

  Live Effect:
    Change (0, -9.81, 0) → (0, -50, 0):
      → 5x stronger gravity
      → Projectiles drop faster
      → Jump heights reduced
      → Falling damage increased

FixedDeltaTime (float, 0.001-0.1 seconds):
  - Physics simulation step size
  - Smaller = more accurate, slower
  - Larger = faster, less stable

  Live Effect:
    Decrease 0.016 (60Hz) → 0.008 (120Hz):
      → 2x more physics steps per second
      → More accurate collision detection
      → Smoother rigid body motion
      → 2x slower simulation (obvious trade-off)

MaxVelocity (float, 1-10000 m/s):
  - Clamp to prevent tunneling (objects moving through walls)
  - Higher = allows faster entities
  - Lower = more stable but limits speed

  Live Effect:
    Increase 100 → 10000:
      → Allows hypersonic projectiles
      → Risk of tunneling if FixedDeltaTime too large
      → May reveal collision bugs

SolverIterations (int, 1-50):
  - Constraint solver iterations per step
  - More = more accurate but slower
  - Fewer = faster but "springy" collisions

  Live Effect:
    Increase 4 → 20:
      → More rigid collisions (less penetration)
      → Slower physics (5x iterations)
      → Better stacking stability (crates don't jitter)
```

#### Collision Settings
```
CollisionLayers (bitmask):
  - Which layers collide with which
  - Modify at runtime to test "ghost mode"

  Live Effect:
    Disable player-enemy collision:
      → Players phase through enemies (debug mode)
      → Useful for testing pathfinding without combat

CollisionMargin (float, 0.001-0.1 meters):
  - "Skin" around colliders
  - Prevents jitter from micro-penetrations

  Live Effect:
    Increase 0.01 → 0.05:
      → More forgiving collisions (less jitter)
      → Objects appear slightly separated
      → Better performance (fewer tiny contacts)

ContinuousCollisionDetection (bool):
  - Expensive but prevents tunneling
  - Toggle to see impact on fast projectiles

  Live Effect:
    Disable CCD:
      → Fast projectiles tunnel through walls
      → 30% faster physics (no swept tests)
      → Reveals need for CCD in current design
```

---

### 3. Job System & Scheduler Tuning

#### Job Batching
```
BatchSize (int, 1-10000):
  - Entities processed per job
  - Smaller = more parallelism, more overhead
  - Larger = less overhead, less parallelism

  Live Effect:
    Decrease 1000 → 100:
      → 10x more jobs spawned
      → Better CPU utilization (more threads busy)
      → More overhead (job scheduling cost)
      → Observe: Does frame time improve or worsen?

MaxThreads (int, 1-128):
  - Worker thread count
  - Usually = CPU core count
  - Tweak to test scaling

  Live Effect:
    Decrease from 8 cores → 2 cores:
      → Simulate low-end CPU
      → Observe which systems bottleneck
      → Reveals parallelization effectiveness
```

#### Update Frequencies
```
Per-System Update Rate:
  - Some systems don't need 60Hz

  Examples:

  PatrolPlannerSystem:
    UpdateFrequency: Every N ticks (default: 60)

    Live Effect:
      Change 60 → 1:
        → Patrols planned every tick (expensive!)
        → More responsive to threats
        → 60x slower (obvious)

      Change 60 → 300:
        → Patrols planned every 5 seconds
        → Much faster
        → Slower response to border changes
        → Observe: Is 5s acceptable?

  ShadowCastingSystem:
    UpdateFrequency: Every N ticks (default: 10)

    Live Effect:
      Change 10 → 1:
        → Shadows update every tick
        → Smooth day/night transitions
        → 10x slower

      Change 10 → 60:
        → Shadows update once per second
        → "Popping" as plants switch light/dark
        → Faster but less smooth
```

#### Job Dependencies
```
EnableParallelism (bool per system):
  - Force system to run serially (debug mode)

  Live Effect:
    Disable parallelism for MovementSystem:
      → All movement processed on single thread
      → Frame time increases (measure by how much)
      → Proves parallelism benefit quantitatively
```

---

### 4. Memory Management Settings

#### Pool Sizes
```
EntityCapacity (int, 100-1000000):
  - Max entities in world
  - Pre-allocated at startup

  Live Effect:
    Decrease 100000 → 10000:
      → Less memory used
      → Spawn failure if exceeded
      → Useful for testing low-memory devices

ComponentPoolSizes (int per type, 10-100000):
  - Pre-allocated component pools
  - Prevents allocations during gameplay

  Live Effect:
    Set MovementModelSpec pool = 100:
      → Only 100 moving entities allowed
      → Spawn 101st → allocation or failure
      → Stress test: Does pool grow gracefully?
```

#### Buffer Capacities
```
DynamicBufferCapacity (int, 4-1024 elements):
  - Initial size for IBufferElementData

  Live Effect:
    Increase default 8 → 64:
      → Fewer reallocations (better performance)
      → More memory per entity (trade-off)
      → Observe: Does GC improve (fewer allocs)?

NativeContainerCapacity (int, 16-65536):
  - Initial size for NativeList, NativeHashMap

  Live Effect:
    Decrease 256 → 16:
      → More frequent resizing
      → Worse performance (reallocations)
      → Useful for stress testing resize logic
```

---

### 5. Rendering & Presentation

#### Level of Detail (LOD)
```
LODDistances (float array, meters):
  - Distance thresholds for LOD switches
  - [50, 200, 1000] = LOD0 < 50m, LOD1 < 200m, LOD2 < 1000m

  Live Effect:
    Change [50, 200, 1000] → [10, 20, 30]:
      → Higher quality meshes at all distances
      → Much slower rendering (more triangles)
      → GPU bottleneck visible

    Change [50, 200, 1000] → [5, 5, 5]:
      → Everything uses lowest LOD
      → Very fast rendering (low poly)
      → Ugly but playable on low-end GPU

LODBias (float, 0.1-10.0):
  - Multiplier for LOD distances
  - <1.0 = bias toward higher quality
  - >1.0 = bias toward lower quality

  Live Effect:
    Set to 2.0:
      → All LOD distances doubled
      → Higher quality at same distance
      → Slower rendering
```

#### Culling Settings
```
FrustumCulling (bool):
  - Disable to render everything (debug)

  Live Effect:
    Disable:
      → All entities rendered, even off-screen
      → Much slower GPU
      → Proves culling effectiveness

OcclusionCulling (bool):
  - Disable to render occluded objects

  Live Effect:
    Disable:
      → Objects behind walls still rendered
      → Slower but easier to debug visibility

CullingDistance (float, 10-10000 meters):
  - Max render distance

  Live Effect:
    Decrease 1000 → 100:
      → Only nearby entities rendered
      → Much faster (fewer draw calls)
      → "Fog of war" effect visually
```

#### Batch Sizes
```
InstanceBatchSize (int, 1-1023):
  - Entities per GPU instanced draw call
  - Unity max = 1023

  Live Effect:
    Decrease 1023 → 100:
      → More draw calls (slower)
      → Observe GPU bottleneck
      → Reveals batching importance

ShadowCascades (int, 0-4):
  - Number of shadow map cascades
  - More = better quality, slower

  Live Effect:
    Increase 2 → 4:
      → Better shadow quality at distance
      → 2x slower shadow rendering
      → GPU stress test
```

---

### 6. AI & Pathfinding Configuration

#### Pathfinding Costs
```
TerrainCostMultipliers (float per terrain type):
  - Grass: 1.0 (default)
  - Sand: 1.2 (slower)
  - Mud: 2.0 (very slow)
  - Road: 0.5 (faster)

  Live Effect:
    Set Mud = 10.0:
      → Units avoid mud strongly
      → Longer paths around swamps
      → Different tactical gameplay

DiagonalMovementCost (float, 1.0-2.0):
  - Cost of diagonal vs straight movement
  - 1.0 = treat as same (Manhattan)
  - 1.414 = Euclidean distance (realistic)
  - 2.0 = heavily penalize diagonals

  Live Effect:
    Change 1.414 → 1.0:
      → Units cut corners more aggressively
      → Shorter paths but less realistic
      → Faster pathfinding (simpler heuristic)
```

#### A* Tuning
```
MaxSearchNodes (int, 100-100000):
  - Max nodes explored before giving up
  - Higher = can solve harder paths
  - Lower = faster but may fail

  Live Effect:
    Decrease 10000 → 1000:
      → Complex paths fail ("no path found")
      → 10x faster pathfinding
      → Units get stuck more often

HeuristicWeight (float, 0.5-2.0):
  - Weight of heuristic vs actual cost
  - 1.0 = optimal (A*)
  - <1.0 = explores more (slower, optimal)
  - >1.0 = greedy (faster, suboptimal)

  Live Effect:
    Increase 1.0 → 2.0:
      → Paths found faster (less exploration)
      → Paths suboptimal (longer than necessary)
      → Trade-off: Speed vs quality
```

#### Behavior Tree Parameters
```
DecisionThresholds (float, 0-1):
  - Thresholds for behavior transitions

  Example: FleeThreshold (HP ratio)
    Default: 0.3 (flee when HP < 30%)

    Live Effect:
      Change 0.3 → 0.7:
        → Units flee earlier (more cautious)
        → Combat disengages faster
        → Different tactical feel

UpdateFrequency (int, ticks):
  - How often AI re-evaluates decisions

  Live Effect:
    Decrease 30 → 1:
      → AI reacts every tick (very responsive)
      → Much slower (30x more decisions)
      → Observe: Is 1-tick response worth cost?
```

---

### 7. Gameplay Rules & Balancing Constants

#### Damage Formulas
```
DamageFormula (expression):
  - Base formula for damage calculation

  Current: Damage = Attack × (1.0 - Defense / (Defense + 100))

  Live Tweaks:

  DefenseExponent (float, 0.5-2.0):
    Default: 1.0 (linear)

    Live Effect:
      Change 1.0 → 0.5:
        → Square root scaling (defense diminishes faster)
        → High defense less effective
        → Offensive meta favored

      Change 1.0 → 2.0:
        → Quadratic scaling (defense super effective)
        → High defense extremely tanky
        → Defensive meta favored

  CriticalMultiplier (float, 1.5-5.0):
    Default: 2.0 (double damage)

    Live Effect:
      Change 2.0 → 5.0:
        → Crits deal 5x damage (massive)
        → More swingy combat (luck-based)
        → Reveals balance issues
```

#### Resource Rates
```
ResourceGatherRates (float per resource type):
  - Wood: 1.0 per tick
  - Stone: 0.5 per tick
  - Gold: 0.1 per tick

  Live Effect:
    Multiply all by 10:
      → Resources gathered 10x faster
      → Economy accelerates
      → Reveals endgame faster (testing)

ResourceCaps (int per resource):
  - Max storage capacity

  Live Effect:
    Set all caps = 100:
      → Frequent cap-hitting
      → Forces spending (different gameplay)
      → Tests cap-handling logic
```

#### Time Constants
```
DayNightCycleDuration (float, seconds):
  - How long is one in-game day?
  - Default: 600 seconds (10 minutes real-time)

  Live Effect:
    Decrease 600 → 60:
      → 1 minute days (rapid cycle)
      → Day/night effects happen faster
      - Plants grow/dormant quickly
      → Good for testing circadian mechanics

BuildTimes (float per building type):
  - Seconds to construct

  Live Effect:
    Divide all by 100:
      → Instant construction (nearly)
      → Skip waiting (content testing)
      → Reveals late-game faster
```

---

## Foundational Settings UI

### Categorized Inspector

```
[Foundational Settings Inspector]

  ▼ Spatial Partitioning
    Grid Cell Size:    [=====|===] 50 m (1-1000)
    Max Per Cell:      [===|=====] 500 (10-10000)
    Query Radius:      [==|======] 150 m (1-5000)
    Rebuild Freq:      [=|=======] 10 ticks (1-60)
    [Apply] [Reset to Defaults]

  ▼ Physics Engine
    Gravity Y:         [===|=====] -9.81 m/s² (-100 to +100)
    Fixed Delta Time:  [====|====] 0.016 s (0.001-0.1)
    Max Velocity:      [==|======] 500 m/s (1-10000)
    Solver Iterations: [=|=======] 8 (1-50)
    [Apply] [Reset]

  ▼ Job System
    Batch Size:        [====|====] 1000 (1-10000)
    Max Threads:       [====|====] 8 (1-128)
    [Apply] [Reset]

  ▼ Memory Management
    Entity Capacity:   [===|=====] 50000 (100-1000000)
    Buffer Capacity:   [=|=======] 16 (4-1024)
    [Apply] [Reset]

  ▼ Rendering
    LOD Distances:     [50, 200, 1000] [Edit Array]
    LOD Bias:          [====|====] 1.0 (0.1-10)
    Culling Distance:  [==|======] 500 m (10-10000)
    [Apply] [Reset]

  ▼ AI & Pathfinding
    Max Search Nodes:  [===|=====] 5000 (100-100000)
    Heuristic Weight:  [====|====] 1.0 (0.5-2.0)
    Flee Threshold:    [=|=======] 0.3 (0-1)
    [Apply] [Reset]

  ▼ Gameplay Rules
    Damage Exponent:   [====|====] 1.0 (0.5-2.0)
    Crit Multiplier:   [===|=====] 2.0 (1.5-5.0)
    Day Length:        [===|=====] 600 s (10-3600)
    [Apply] [Reset]

  [Save Profile] [Load Profile] [Export to Config]
```

---

## Foundational Stress Tests

### Test 1: Extreme Spatial Density
```
Goal: Test spatial hash with extreme cell sizes

Setup:
  1. CellSize = 1 meter (very small)
  2. Spawn 10000 entities in 100×100m area
  3. Run "find nearby" queries constantly

Expected Behavior:
  - Millions of cells created
  - High memory usage
  - Frequent hash collisions
  - Slower insertion

Metrics:
  - Memory usage (MB)
  - Query time per entity (ms)
  - Hash rebuild time (ms)

Reveals:
  - Optimal cell size for dense scenarios
  - Memory scaling limits
```

### Test 2: Physics Chaos
```
Goal: Test physics stability at extreme settings

Setup:
  1. Gravity = -100 m/s² (10x Earth)
  2. FixedDeltaTime = 0.05 (20Hz, low)
  3. MaxVelocity = 10000 (very high)
  4. SolverIterations = 1 (minimal)

Expected Behavior:
  - Objects fall extremely fast
  - Penetrations and tunneling
  - Jittery collisions
  - Unstable stacks

Metrics:
  - Penetration depth (cm)
  - Tunneling events (count)
  - Frame time (physics cost)

Reveals:
  - Minimum SolverIterations for stability
  - Relationship between FixedDeltaTime and MaxVelocity
```

### Test 3: Job Parallelism Scaling
```
Goal: Measure parallelism efficiency

Setup:
  1. Spawn 10000 entities with movement
  2. Vary MaxThreads: 1, 2, 4, 8, 16
  3. Vary BatchSize: 10, 100, 1000, 10000
  4. Measure frame time

Expected Behavior:
  - 1 thread: Slowest (baseline)
  - 8 threads: ~8x faster (ideal)
  - 16 threads: <16x (diminishing returns)
  - Batch size affects overhead

Metrics:
  - Frame time vs thread count (chart)
  - Speedup factor (actual vs ideal)
  - Optimal batch size per thread count

Reveals:
  - Actual parallelism efficiency
  - Overhead of job scheduling
  - Sweet spot for batch sizes
```

### Test 4: Memory Pool Stress
```
Goal: Test pool growth and allocation behavior

Setup:
  1. EntityCapacity = 1000 (low)
  2. ComponentPoolSizes = 500 (low)
  3. Spawn entities until pools exhausted
  4. Observe behavior

Expected Behavior:
  - Pools fill up
  - Either: Graceful growth (allocations)
  - Or: Hard failure (entity spawn rejected)

Metrics:
  - Allocation count
  - Memory usage over time
  - Frame time impact of reallocations

Reveals:
  - Whether pools auto-grow
  - Cost of dynamic growth
  - Need for pre-allocation tuning
```

### Test 5: LOD Performance Impact
```
Goal: Quantify rendering cost vs LOD settings

Setup:
  1. Spawn 1000 entities (visible)
  2. Vary LODDistances: [10,20,30] vs [100,500,2000]
  3. Measure GPU time

Expected Behavior:
  - Tight LOD distances: High poly, slow
  - Loose LOD distances: Low poly, fast

Metrics:
  - GPU frame time (ms)
  - Triangle count per frame
  - Draw call count

Reveals:
  - Rendering bottleneck (GPU-bound?)
  - Optimal LOD distances for target FPS
```

### Test 6: Pathfinding Trade-offs
```
Goal: Balance pathfinding quality vs speed

Setup:
  1. Complex maze (many obstacles)
  2. Vary MaxSearchNodes: 100, 1000, 10000
  3. Vary HeuristicWeight: 0.5, 1.0, 2.0
  4. 100 units pathfind simultaneously

Expected Behavior:
  - Low MaxSearchNodes: Fast but fails on complex paths
  - High HeuristicWeight: Fast but suboptimal paths
  - Optimal: Balance of speed and quality

Metrics:
  - Pathfinding time per query (ms)
  - Path length (meters)
  - Failure rate (%)

Reveals:
  - Acceptable trade-offs for game feel
  - Minimum node budget for level complexity
```

---

## Configuration Profiles (Presets)

### Performance Profiles
```
Low-End Device Profile:
  - CellSize: 100 (coarse)
  - FixedDeltaTime: 0.033 (30Hz physics)
  - MaxThreads: 2
  - LODDistances: [5, 10, 20] (aggressive)
  - CullingDistance: 100
  - MaxSearchNodes: 500 (fast pathfinding)

  Use Case: Mobile, low-spec PC

High-End Profile:
  - CellSize: 10 (fine)
  - FixedDeltaTime: 0.008 (120Hz physics)
  - MaxThreads: 16
  - LODDistances: [200, 1000, 5000] (quality)
  - CullingDistance: 2000
  - MaxSearchNodes: 50000 (perfect paths)

  Use Case: High-end PC, next-gen console
```

### Gameplay Profiles
```
Casual Profile:
  - DamageMultiplier: 0.5 (less lethal)
  - ResourceGatherRate: 3.0 (faster)
  - BuildTimes: ×0.5 (faster)
  - FleeThreshold: 0.5 (cautious AI)

  Use Case: Relaxed, accessible gameplay

Hardcore Profile:
  - DamageMultiplier: 2.0 (very lethal)
  - ResourceGatherRate: 0.5 (scarce)
  - BuildTimes: ×2.0 (slower)
  - FleeThreshold: 0.1 (aggressive AI)

  Use Case: Challenging, strategic gameplay
```

### Debug Profiles
```
Instant Feedback Profile:
  - TimeScale: 10.0 (fast forward)
  - BuildTimes: ×0.01 (instant)
  - ResourceGatherRate: ×100 (abundant)
  - DayNightCycle: 10 seconds

  Use Case: Rapid testing, skip waiting

Physics Stress Profile:
  - Gravity: (0, -100, 0)
  - MaxVelocity: 10000
  - FixedDeltaTime: 0.005 (200Hz)
  - SolverIterations: 20

  Use Case: Test physics edge cases

Pathfinding Stress Profile:
  - MaxSearchNodes: 100 (force failures)
  - HeuristicWeight: 3.0 (greedy)
  - TerrainCostMultipliers: All ×10 (harsh)

  Use Case: Test pathfinding robustness
```

---

## Export & Integration

### Export to Config Files
```
Save foundational settings as JSON config:

{
  "profile": "HighEnd_60FPS",
  "spatial": {
    "cellSize": 10,
    "maxPerCell": 1000,
    "queryRadius": 150,
    "rebuildFrequency": 10
  },
  "physics": {
    "gravity": [0, -9.81, 0],
    "fixedDeltaTime": 0.016,
    "maxVelocity": 500,
    "solverIterations": 8
  },
  "jobSystem": {
    "batchSize": 1000,
    "maxThreads": 8
  },
  ...
}

Use Cases:
  - Load at startup (different profiles per platform)
  - Version control (track config changes)
  - A/B testing (compare profiles)
```

### Integration with ScenarioRunner
```
CLI Commands:

foundation.load <profile>
  → Load preset profile (LowEnd, HighEnd, Debug)

foundation.set spatial.cellSize 50
  → Modify specific setting

foundation.physics.gravity 0 -50 0
  → Override gravity

foundation.export <path>
  → Save current settings to JSON

foundation.compare <profile1> <profile2>
  → Diff two profiles, show differences

foundation.reset
  → Reset all to defaults

foundation.stress <test>
  → Run foundational stress test
  Examples:
    foundation.stress extreme_density
    foundation.stress physics_chaos
    foundation.stress pathfinding_trade_offs
```

---

## Live Feedback During Tweaks

### Performance Impact Indicators
```
While adjusting foundational settings, show real-time impact:

[Spatial Cell Size: 50m → 100m]
  Impact:
    ✓ Memory: -45% (12 MB → 6.6 MB)
    ✓ Query Time: -20% (0.5ms → 0.4ms)
    ⚠ Precision: Reduced (broader queries)

[Physics Fixed Delta: 0.016s → 0.008s]
  Impact:
    ✗ Frame Time: +95% (12ms → 23ms)
    ✓ Collision Accuracy: +50% (fewer misses)
    ✗ Physics Cost: Doubled (expected)

[Pathfinding Max Nodes: 10000 → 1000]
  Impact:
    ✓ Pathfinding Time: -80% (5ms → 1ms)
    ✗ Path Failures: +25% (complex paths fail)
    ⚠ Path Quality: Reduced (shorter search)
```

### Warnings & Suggestions
```
User sets CellSize = 1 meter:

  [!] Warning: Very small cell size detected
      - Expected cell count: 5,000,000
      - Memory estimate: 200 MB
      - Suggestion: Consider CellSize ≥ 10m for this world size

User sets FixedDeltaTime = 0.001:

  [!] Warning: Extremely high physics rate (1000Hz)
      - Expected frame time: >100ms (physics-bound)
      - Will NOT run at 60 FPS
      - Suggestion: 0.016s (60Hz) or lower for playability

User sets MaxSearchNodes = 100:

  [!] Warning: Very low pathfinding budget
      - Complex paths may fail frequently
      - Units may get stuck or give up
      - Suggestion: ≥1000 for typical game levels
```

---

## Observability (What Changed?)

### Change Log
```
Track all foundational setting changes:

[Foundational Change Log]
  Tick 1200: spatial.cellSize changed 10 → 50 (user: designer)
  Tick 1250: physics.gravity.y changed -9.81 → -20 (user: sandbox)
  Tick 3000: foundation.load "HighEnd" (profile applied)
  Tick 4500: jobSystem.maxThreads changed 8 → 4 (test: thread scaling)

Export log:
  - JSON for analysis
  - Replay: Re-apply changes deterministically
  - Compare: Before/after metrics
```

### Diff Visualization
```
Compare two configurations:

[Config Diff: Default vs HighEnd]

  Spatial:
    cellSize:       10 → 10 (no change)
    queryRadius:    150 → 200 (+33%)

  Physics:
    fixedDeltaTime: 0.016 → 0.008 (-50%, 2x more steps)
    solverIterations: 8 → 16 (+100%, more accurate)

  Job System:
    maxThreads:     8 → 16 (+100%, more parallelism)

  Rendering:
    lodDistances:   [50,200,1000] → [200,1000,5000]
                    (+300%, higher quality)

Net Impact:
  ✗ Performance: ~40% slower (physics + rendering)
  ✓ Quality: Significantly better
  Use Case: High-end hardware only
```

---

## Summary

**Expanded Vision**: Not just tweak entities, but **tweak the engine itself**.

**Categories Exposed**:
1. **Spatial Partitioning**: Grid sizes, rebuild frequency, query ranges
2. **Physics**: Gravity, time steps, solver quality, collision settings
3. **Job System**: Batch sizes, thread counts, parallelism toggles
4. **Memory**: Pool sizes, capacities, allocation strategies
5. **Rendering**: LOD, culling, batching, quality settings
6. **AI**: Pathfinding budgets, behavior thresholds, decision frequencies
7. **Gameplay**: Damage formulas, resource rates, time constants

**Stress Tests**:
- Extreme density (spatial hash limits)
- Physics chaos (stability testing)
- Job scaling (parallelism efficiency)
- Memory pressure (pool exhaustion)
- LOD impact (rendering cost)
- Pathfinding trade-offs (quality vs speed)

**Integration**:
- CLI commands (foundation.*)
- JSON profiles (save/load configs)
- Live feedback (impact indicators)
- Change logging (track tweaks)
- Diff visualization (compare configs)

**Philosophy**: "True understanding comes from being able to break it, then fix it, then optimize it—all while it's running."

This extends the runtime sandbox from **gameplay tweaking** to **engine mastery**.

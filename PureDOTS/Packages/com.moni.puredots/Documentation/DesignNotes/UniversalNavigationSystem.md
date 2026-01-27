# Universal Navigation System Concepts

## Goals
- Provide a shared navigation framework covering 2D terrain (with height/slope) and 3D space environments.
- Incorporate danger weights, terrain modifiers, and traversal modes so villagers, fleets, and AI agents choose safest/fastest paths.
- Remain deterministic, Burst-friendly, and tightly integrated with the spatial services and scheduler infrastructure.

## Data Sources
- **Spatial Grid**: base occupancy and obstacle data (hashed grid, cell metadata).
- **Height/Slope Field**: sampled from terrain or authored heightmaps; stored per cell as `float Height`, `float Slope`.
- **Danger Layers**: dynamic heatmaps (raids, storms, plague). Multiple layers combine with weights per agent type.
- **Traversal Modes**: enum identifying movement profile (`OnFoot`, `Mounted`, `Waterborne`, `Airborne`, `Spacecraft`). Each mode has penalty tables and allowed surfaces.
- **3D Nav Volumes**: culling obstacles in space scenes (asteroids, debris fields) stored as convex cells or voxel occupancy grids.

## Cost Function
For neighbouring nodes `a` → `b`:
```
float distance = math.distance(posA, posB);
float slopePenalty = math.max(0f, slope(b) - slopeLimit) * slopeWeight;
float terrainCost = terrainTypeCost[b];
float dangerCost = dot(agentDangerWeights, dangerLayers[b]);
float traversalPenalty = traversalModePenalty[mode][terrainType];
float totalCost = distance * terrainCost * traversalPenalty
                + slopePenalty
                + dangerCost
                + hazardEventsCost(b);
```
- Space navigation replaces slope term with velocity-change penalties (course corrections, gravity wells).
- Hazard events (storms, traps) add discrete costs or hard blocks depending on severity.

## 2D Terrain Navigation
- **Graph Construction**: derived from spatial grid; each cell becomes node with neighbours (4/8 connectivity). Height and danger metadata stored in SoA arrays for Burst iteration.
- **Hierarchical Levels**: coarse macro grid (e.g., 32×32) summarises terrain; used for long-distance planning. Micro grid handles final approach.
- **Algorithm**: A* with consistent heuristic (octile distance) using deterministic open set (bucketed priority queue). Supports:
  - `HPA*` updates for dynamic obstacles.
  - `D* Lite` fallback for fast replanning when terrain changes.
- **Query Flow**:
  1. Systems enqueue `NavigationQueryRequest` with start, goal, mode, danger weights.
  2. `NavigationSchedulerSystem` batches requests by mode/heuristic.
  3. Burst job `TerrainPathfindJob` executes A*/HPA* per request, writing `NavigationPath` buffers (list of waypoints) and summary metrics (cost, danger exposure).
  4. Optional smoothing (string pulling, funnel) runs in vectorized job to reduce zig-zag.

## 3D Space Navigation
- **Nav Volume Representation**: 3D grid of cells or nav polyhedra with adjacency lists; stores gravity vectors, hazard fields, legal velocities.
- **Algorithm**: `A*` or `Theta*` with Euclidean heuristic; can upgrade to `Any-angle` or `Jump Point Extension` for open space.
- **Danger & Terrain**: distance penalties incorporate radiation, minefields, patrol zones.
- **Course Planning**:
  - Step 1: coarse route through macro nodes (trade lanes, corridors).
  - Step 2: fine route to docking targets using local avoidance flow fields.
- **Dynamic Obstacles**: maintain `SpaceObstacleBuffer` (ships, debris). Use `VelocityObstacle` or `Reciprocal Velocity Obstacles` for localized adjustments after base path generation.

## Danger Weight Handling
- Danger layers stored as `NativeArray<float>` per cell/volume; updated by `DangerFieldUpdateSystem`.
- Agents carry `DangerPreference` weights (e.g., villagers avoid raids, raiders ignore). Cost function multiplies layer values by weights.
- Provide quick queries (`SampleDanger`, `ComputeSafeCorridor`) for AI heuristics.

## Query API
```csharp
public struct NavigationQueryRequest : IBufferElementData
{
    public Entity Agent;
    public float3 Start;
    public float3 Goal;
    public TraversalMode Mode;
    public DangerPreference Weights;
    public PathRequestFlags Flags; // allow partial, require safe corridor, etc.
}

public struct NavigationPath : IBufferElementData
{
    public float3 Waypoint;
}

public struct NavigationSummary : IComponentData
{
    public float TotalCost;
    public float DangerExposure;
    public bool Success;
}
```
- Requests stored in SoA buffers; path results pooled to avoid allocations.
- `NavigationResultSystem` writes completions back to agents (villager job, fleet AI). On failure, optional fallback (flow field, direct steering).

## Technical Considerations
- **SoA Storage**: For grids, store positions, costs, danger, slope in separate `NativeArray`s to leverage Burst.
- **Parallelization**: Batch multiple path jobs; schedule per traversal mode to reduce divergent branches.
- **Determinism**: Use fixed-size buckets for open set, consistent tie-breakers (lexicographic by cell index). All randomization replaced with hashed seeds.
- **Rewind**: Recompute paths deterministically on playback; minimal state storage (requests + config). Optionally cache results with versioned keys (start/goal strongly hashed) for catch-up.
- **Caching**: Maintain path cache keyed by `(startCell, goalCell, mode, dangerProfileVersion)` with TTL; helps repeated queries.
- **Upkeep**: Danger and terrain modifiers flagged by version numbers. Navigation systems check for mismatch and queue rebuilds automatically.
- **Integration**: Works with `FlowFieldPathfinding` by letting flow fields consume precomputed safe corridors or fallback to local steering.

## Authoring & Config
- `NavigationConfig` ScriptableObject:
  - cell size, macro/micro ratios, slope limits, default danger weights.
  - heuristics settings (max iterations per tick, partial path rules).
- `DangerLayerProfile`: defines blend modes and base weights (raids, storms, corruption).
- `TraversalModeProfile`: per mode penalties, allowed terrain masks, slope thresholds.
- Bakers emit blobs consumed by runtime singletons (`NavigationConfigData`, `DangerProfileBlob`).

## Testing
- Unit tests for cost function, heuristic admissibility, A* correctness under weighted terrains.
- Integration tests combining danger updates, obstacle injections, and path queries.
- Stress tests (10k+ simultaneous requests) measuring throughput.
- Determinism tests for identical inputs under catch-up/rewind.
- Visualization tools (heatmaps, path overlays) to debug routes and danger influence.

## Roadmap
1. Implement common data structures (grid/volume adjacency, request buffers).
2. Terrain pathfinding MVP (2D) with danger weights.
3. Space navigation prototype using nav volumes.
4. Integrate with flow fields/local steering; add caching and smoothing.
5. Expand tooling & analytics (exposure reports, safe corridor diagnostics).

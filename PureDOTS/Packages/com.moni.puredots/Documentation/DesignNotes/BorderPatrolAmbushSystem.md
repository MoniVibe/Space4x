# Border Patrol, Ambush & Rapid Response System (Game-Agnostic)

## Core Concept

**Unified Problem**: How do entities patrol borders, set ambushes, and respond to intrusions in a deterministic, data-driven way that works in both 2D (Godgame terrain) and 3D (Space4X void)?

**Solution**: Field-based decision making using signed distance fields, sensor coverage maps, and ownership boundaries to drive autonomous patrol behavior.

**Key Insight**: Borders are mathematical surfaces (zero-crossings in ownership fields). Patrols move along these surfaces, ambushes hide in sensor shadows, and intercepts predict enemy trajectories.

## Design Principles

### 1. Deterministic & Data-First
```
Required:
  - FixedStep simulation only (no async/coroutines)
  - Seeded RNG for all decisions (same seed = same outcome)
  - No GameObject/MonoBehaviour references
  - All data in components/blobs
  - Presentation via request buffers (never structural changes in gameplay)

Result:
  - Perfect replay (record input → deterministic output)
  - Rewind support (restore state, replay)
  - Network determinism (lockstep multiplayer)
```

### 2. Game-Agnostic Core, Thin Adapters
```
PureDOTS (core):
  - Field builders (borders, sensors, needs)
  - Patrol planner (ticket emission)
  - Patrol behavior state machine
  - Ambush reveal logic
  - Intercept prediction

Space4X (adapter):
  - Jump planner (micro-jumps behind enemies)
  - 3D sensor fields (spherical propagation)
  - Orbital mechanics integration

Godgame (adapter):
  - Flank path routing (around mountains/rivers)
  - 2D sensor fields (line-of-sight on terrain)
  - Terrain integration
```

### 3. 2D/3D Unification via 3D Fields
```
Strategy:
  - All fields are 3D (NativeArray3D or voxel grid)
  - Godgame: Set Size.z = 1 (single layer)
  - Space4X: Full 3D (Size.z = orbital height layers)

Benefits:
  - Same algorithms work for both
  - No branching on "is2D" flags
  - Trivial to add vertical layers to Godgame later (underground caves, sky islands)
```

---

## Architecture Overview

### Data Flow

```
Input:
  OwnershipGrid (which faction owns each cell)
    ↓
BorderFieldBuilder (compute signed distance to borders)
    ↓
SensorFieldBuilder (visibility/radar pressure)
    ↓
PatrolNeedField (combine border strength + gaps + threats)
    ↓
PatrolPlanner (emit tickets for needed patrols)
    ↓
PatrolSpawner (instantiate units from tickets)
    ↓
PatrolBehavior (state machine: patrol → shadow → ambush → intercept)
    ↓
Output:
  - Patrol units moving along borders
  - Ambush units hidden in sensor shadows
  - Intercept units responding to intrusions
```

### Field Types (The Foundation)

#### 1. OwnershipGrid
```
Purpose: Track which faction owns each spatial cell

Data:
  - NativeArray3D<uint> (cell → OwnerId)
  - Updated by gameplay (conquest, capture, construction)
  - 0 = neutral, 1-255 = faction IDs

Resolution:
  - Godgame: 10x10 meter cells (terrain-aligned)
  - Space4X: 1000x1000 meter cells (sector-based)

Example:
  Cell (50, 100, 0) → OwnerId = 3 (Faction Red)
  Cell (51, 100, 0) → OwnerId = 5 (Faction Blue)
  → Border between (50,100) and (51,100)
```

#### 2. BorderField (Signed Distance Field)
```
Purpose: For each cell, store distance (meters) to nearest ownership boundary

Signed Distance:
  - Positive: Inside friendly territory (distance inward from border)
  - Zero: On border
  - Negative: Inside enemy territory (distance inward from enemy side)

Calculation:
  - Scan OwnershipGrid for ownership transitions
  - Mark border cells (where neighbor has different OwnerId)
  - Flood-fill from border cells, accumulating distance
  - Sign based on whether moving into friendly or hostile

Example:
  Friendly cells: +10, +20, +30 (10m, 20m, 30m from border)
  Border cell: 0
  Enemy cells: -10, -20, -30 (10m, 20m, 30m into enemy territory)

Use:
  - Patrol band selection: patrol cells where 0 ≤ distance ≤ BorderDepth
  - Risk assessment: negative distance = deep in enemy territory = high risk
```

#### 3. SensorField (Detection Pressure)
```
Purpose: Normalized (0-1) probability of being detected per cell

Sources:
  - Radar stations (Space4X)
  - Watchtowers (Godgame)
  - Active sensor sweeps
  - Enemy unit vision cones

Calculation:
  - For each sensor source, propagate detection strength
  - Attenuate by distance, terrain occlusion (Godgame), or asteroid fields (Space4X)
  - Accumulate all sources: Total = 1.0 - ∏(1 - source_i)
  - Clamp to [0, 1]

Example:
  - Open plains near watchtower: SensorField = 0.9 (high detection)
  - Cave behind mountain: SensorField = 0.1 (low detection, good for ambush)
  - Deep space near asteroid field: SensorField = 0.2 (sensor shadow)

Use:
  - Ambush placement: prefer cells with SensorField < 0.3
  - Patrol routing: avoid over-exposed routes (SensorField > 0.8)
```

#### 4. PatrolNeedField (Composite Risk/Gap Map)
```
Purpose: Combined metric of "how much do we need patrols here?"

Inputs:
  1. Border strength: abs(∇OwnershipGrid) (high gradient = active border)
  2. Coverage gaps: few friendly units in area
  3. Recent sightings: enemy activity (decayed over time)
  4. Sensor shadows: low SensorField = blind spot

Formula (conceptual):
  Need = BorderStrength × 0.4 +
         CoverageGap × 0.3 +
         RecentThreat × 0.2 +
         SensorShadow × 0.1

Result:
  - High Need → emit more patrol tickets
  - Low Need → reduce patrols (shift elsewhere)

Dynamic:
  - Recalculated every N ticks (e.g., every 60 ticks = 1 second at 60Hz)
  - Adapts to changing threats and deployments
```

---

## Component Design

### A. Policy & Configuration

#### PatrolPolicy
```
Purpose: Faction-wide settings for patrol behavior

Fields:
  - BorderDepthMeters (float): How far inward to patrol (e.g., 500m band)
  - DensityPerKm (float): Desired patrols per km of border (e.g., 2.5)
  - AmbushProb (float): Chance [0-1] to seed an ambush slot at corridor point
  - InterceptLookahead (float): Seconds to predict enemy movement for intercept
  - ReshuffleCooldown (float): Minimum seconds before re-planning patrol routes

Authoring:
  - ScriptableObject → baked to blob
  - Per-faction (aggressive factions have higher density, ambush prob)

Usage:
  - PatrolPlannerSystem reads policy to determine ticket emission rate
```

#### PatrolUnitSpecRef
```
Purpose: Maps patrol roles to unit templates/prefabs per game

Fields:
  - Blob: BlobAssetReference<PatrolUnitCatalog>

Catalog Structure:
  - Role → UnitTemplateId mapping
  - Scout: Fast, low combat (Space4X: corvette, Godgame: light cavalry)
  - Interceptor: Balanced speed/firepower (Space4X: destroyer, Godgame: archer)
  - Ambusher: High damage, low mobility (Space4X: cloaked bomber, Godgame: hidden pike)

Authoring:
  - Per-game ScriptableObject
  - Designer assigns unit types to roles
  - Baked to blob at startup
```

### B. Patrol Tickets & Slots

#### PatrolTicket (Buffer)
```
Purpose: Request for a patrol unit at a specific location

Fields:
  - Seed (uint): RNG seed for deterministic unit selection
  - Pos (float3): Where to spawn patrol (jittered within cell)
  - Role (byte): Scout=0, Interceptor=1, Ambusher=2

Lifecycle:
  1. PatrolPlannerSystem emits tickets to faction's ticket buffer
  2. PatrolSpawnerSystem consumes tickets, spawns units
  3. Tickets removed after spawn (one-shot)

Burst-Friendly:
  - All value types, no Entity references (entity created on spawn)
  - Stored in DynamicBuffer on faction aggregate entity
```

#### AmbushSlot
```
Purpose: Pre-designated ambush position (not yet occupied)

Fields:
  - Pos (float3): Center of ambush zone
  - Radius (float): Trigger radius (enemy enters → reveal)
  - Seed (uint): For selecting ambusher type deterministically

Lifecycle:
  1. PatrolPlannerSystem creates AmbushSlot entities in low-sensor cells
  2. PatrolSpawnerSystem assigns ambusher to slot (adds SlotOccupied tag)
  3. RevealRulesSystem monitors slot; removes HiddenTag when triggered
  4. Slot remains for future re-use (or expires after time)

Why Entity?
  - Slots persist across multiple ambushers
  - Can have components (SlotOccupied, LastRevealTick, etc.)
  - Easier to query spatially ("find nearest available ambush slot")
```

### C. Patrol State & Behavior

#### PatrolState (Component)
```
Purpose: Current behavior mode of patrol unit

Enum Values:
  - Patrolling: Move along corridor, maintain spacing
  - Shadowing: Follow enemy at distance without revealing
  - AmbushReady: Occupy slot, hold fire, hidden
  - Intercept: Move to predicted intercept point
  - RTB (Return To Base): Low fuel/HP, heading home

Transitions (State Machine):
  Patrolling → Shadowing (enemy sighted within lookahead)
  Shadowing → Intercept (enemy trajectory clear, commit)
  Patrolling → AmbushReady (assigned to ambush slot)
  AmbushReady → Intercept (enemy in slot radius, reveal + attack)
  Any → RTB (fuel < 20% OR HP < 30%)
  RTB → Patrolling (refueled/repaired at base)

Deterministic:
  - State changes based purely on data (enemy positions, timers)
  - No random state transitions (use Seed for weighted choices if needed)
```

#### PatrolCorridorRef
```
Purpose: Reference to patrol route (sequence of waypoints along border)

Fields:
  - Blob: BlobAssetReference<CorridorPath>
  - CurrentWaypointIndex (int)
  - LoopBehavior: PingPong, Loop, OneWay

CorridorPath Blob:
  - NativeArray<float3> waypoints
  - Generated by PatrolPlannerSystem along BorderField band
  - Spacing: every N meters (e.g., 100m waypoints)

Movement:
  - Patrol moves from waypoint[i] to waypoint[i+1]
  - On arrival, increment index
  - LoopBehavior determines what happens at end
```

### D. Ambush & Special Abilities

#### HiddenTag
```
Purpose: Unit is not targetable until reveal condition met

Mechanics:
  - Unit has HiddenTag → not included in enemy target acquisition queries
  - RevealRulesSystem checks conditions:
      1. Enemy within AmbushSlot.Radius
      2. Unit fires weapon
      3. Enemy sensor ping hits unit (active sensor sweep)
      4. Max patience timer expires (can't stay hidden forever)
  - On reveal: remove HiddenTag, unit becomes targetable

Godgame Example:
  - Hidden pike units in forest
  - Enemy cavalry charges through
  - Cavalry enters slot radius → pikes reveal, counter-charge

Space4X Example:
  - Cloaked bombers near asteroid
  - Enemy fleet passes within 5km
  - Bombers decloak, launch torpedoes
```

#### JumpCapability (Space4X)
```
Purpose: Unit can micro-jump (short-range teleport) to flanking position

Fields:
  - Range (float): Max jump distance (e.g., 10km)
  - Cooldown (float): Seconds before next jump
  - EnergyCost (float): Energy drained per jump
  - LastJumpTick (uint): When last jump occurred

Usage:
  - Space4X_JumpPlannerSystem analyzes intercept path
  - If direct path crosses high-risk cells (enemy fire lanes):
      Calculate jump point behind enemy (outside sensor cone)
      If within Range && Cooldown elapsed && Energy sufficient:
          Schedule jump (add JumpCommand component)
  - MovementSystem executes jump: instant position change, consume energy

Tactical Depth:
  - Flank enemies instead of frontal assault
  - Dodge into asteroid fields to break sensor lock
  - Escape when ambushed (emergency jump)
```

#### FlankPathRef (Godgame)
```
Purpose: Prebaked path around terrain obstacles for flanking

Fields:
  - Blob: BlobAssetReference<FlankPath>
  - DirectRisk (float): Risk of direct path
  - FlankRisk (float): Risk of flank path

FlankPath Blob:
  - NativeArray<float3> waypoints (around mountain, through forest)
  - Calculated during map initialization or dynamically via pathfinding
  - Stored in blob for reuse across multiple units

Usage:
  - Godgame_FlankPathSystem compares paths:
      If FlankRisk < DirectRisk × 0.7:  // 30% safer
          Switch to flank path
      Else:
          Use direct path
  - Movement system follows current path

Example:
  - Direct path to enemy village: crosses open field (high archer risk)
  - Flank path: goes through forest (low visibility, safer)
  - System chooses flank despite longer distance
```

---

## System Architecture

### Initialization Phase

#### BorderFieldBuildSystem
```
Purpose: Compute signed distance field from ownership boundaries

Input:
  - OwnershipGrid (NativeArray3D<uint>)

Output:
  - BorderField (NativeArray3D<float>)

Algorithm (Conceptual):
  1. Scan OwnershipGrid, identify border cells:
      Cell is border if any neighbor has different OwnerId
  2. Initialize BorderField:
      Border cells = 0.0
      All others = float.MaxValue
  3. Flood-fill (multi-pass):
      For each cell:
          Distance = min(Distance, NeighborDistance + CellSpacing)
      Repeat until convergence
  4. Sign correction:
      If cell's OwnerId matches "friendly", distance is positive
      If cell's OwnerId is hostile, distance is negative

Burst-Friendly:
  - Pure data transformation (no entity queries)
  - IJob or IJobParallelFor over grid cells
  - No allocations (reuse NativeArray across frames)

Frequency:
  - Rebuild when OwnershipGrid changes significantly
  - Incremental updates for small changes (single cell capture)
  - Typically every 10-60 seconds of game time
```

#### PatrolNeedFieldBuildSystem
```
Purpose: Combine multiple factors into patrol need metric

Input:
  - BorderField (border strength)
  - SensorField (sensor coverage)
  - FriendlyUnitDensity (coverage gaps)
  - EnemySightingHistory (recent threats, decayed)

Output:
  - PatrolNeedField (NativeArray3D<float>, range 0-1)

Algorithm:
  For each cell:
      BorderStrength = abs(gradient(OwnershipGrid))  // sharp ownership change = active border
      CoverageGap = 1.0 - saturate(FriendlyUnitsInRadius / DesiredDensity)
      ThreatLevel = sum(EnemySightings within 1km, decayed by time)
      SensorShadow = 1.0 - SensorField[cell]

      Need = weighted_sum([BorderStrength, CoverageGap, ThreatLevel, SensorShadow])
      Clamp to [0, 1]

Burst-Friendly:
  - IJobParallelFor over cells
  - Read-only access to multiple field arrays
  - Write to output array

Frequency:
  - Every 60 ticks (1 second at 60Hz)
  - Responsive to changing battlefield conditions
```

### FixedStep Simulation Phase

#### PatrolPlannerSystem
```
Purpose: Emit patrol tickets to fill coverage gaps

Input:
  - PatrolPolicy (per faction)
  - BorderField (identify patrol band)
  - PatrolNeedField (where patrols needed)
  - ActivePatrolCount (current deployed patrols)

Output:
  - PatrolTicket buffer (requests for new patrols)
  - AmbushSlot entities (potential ambush positions)

Algorithm:
  1. Sample BorderField along patrol band (0 ≤ distance ≤ BorderDepthMeters)
  2. Place corridor points every N meters (e.g., 100m spacing)
  3. For each corridor point:
      Calculate desired patrol count = DensityPerKm × BorderLengthKm
      Current count = ActivePatrols in area
      Gap = desired - current
      If Gap > 0:
          Emit PatrolTicket(Seed, Pos, Role)
          Role = weighted choice (Scout 60%, Interceptor 30%, Ambusher 10%)
  4. With probability AmbushProb, convert corridor point to AmbushSlot:
      If SensorField[point] < 0.3:  // low detection area
          Create AmbushSlot entity
          Emit PatrolTicket with Role=Ambusher assigned to slot

Deterministic:
  - Use Seed = Hash(FactionId, CorridorIndex, FrameCount)
  - Same inputs → same tickets every time

Burst-Friendly:
  - Main logic in IJob (sequential corridor sampling)
  - Ticket emission via EndSimulationECB (structural changes)
```

#### PatrolSpawnerSystem
```
Purpose: Instantiate patrol units from tickets

Input:
  - PatrolTicket buffer
  - PatrolUnitSpecRef (role → unit template mapping)

Output:
  - Spawned patrol entities
  - Tickets consumed (buffer cleared)

Algorithm:
  For each ticket in buffer:
      1. Use Seed to select unit from PatrolUnitCatalog[Role]
      2. Instantiate entity from template (via EntityPrefab)
      3. Set position = Ticket.Pos + jitter (deterministic from Seed)
      4. Add components:
          PatrolState = Patrolling (or AmbushReady if Role=Ambusher)
          PatrolCorridorRef (assign nearest corridor)
          If Ambusher: assign to nearest AmbushSlot, add HiddenTag
      5. Record in faction's ActivePatrolRegistry
      6. Remove ticket from buffer

Budget Enforcement:
  - Max patrols per km (prevent spam)
  - Max patrols per faction (resource limit)
  - If over budget, defer ticket to next frame

Burst-Friendly:
  - Parallel processing of tickets (each ticket independent)
  - Entity instantiation via EntityCommandBuffer (deferred)
```

#### PatrolBehaviorSystem
```
Purpose: State machine for patrol unit AI

Input:
  - PatrolState (current mode)
  - Enemy sightings (spatial queries)
  - Fuel/HP levels
  - AmbushSlot assignment (if ambusher)

Output:
  - Updated PatrolState
  - Movement commands
  - Reveal triggers

State Logic:

Patrolling:
  - Move toward next waypoint in PatrolCorridorRef
  - Maintain separation from allied patrols (avoid clustering)
  - Sample SensorField, avoid over-exposed routes if possible
  - If enemy sighted within InterceptLookahead:
      Transition to Shadowing

Shadowing:
  - Follow enemy at offset (behind or to side, outside detection range)
  - Predict enemy trajectory (linear extrapolation)
  - If trajectory clear for intercept:
      Transition to Intercept
  - If enemy enters high-value area (e.g., friendly base):
      Transition to Intercept (force engagement)
  - If lose track of enemy:
      Return to Patrolling

AmbushReady:
  - Move to AmbushSlot.Pos
  - Stay within slot radius
  - HiddenTag active (not targetable)
  - Wait for trigger (enemy in radius)
  - Max patience timer (e.g., 5 minutes), then RTB if no trigger

Intercept:
  - Calculate intercept point = EnemyPos + EnemyVelocity × InterceptTime
  - Move to intercept point via normal movement system
  - Engage when in weapons range
  - If enemy flees or destroyed:
      Return to Patrolling

RTB:
  - Find nearest friendly base/refuel station
  - Navigate to base
  - On arrival:
      Refuel, repair
      Transition to Patrolling (or despawn if mission complete)

Burst-Friendly:
  - State transitions based on pure data (no callbacks)
  - IJobEntity over all patrols
  - Movement commands written to NavigationCommand component (consumed by MovementSystem)
```

#### RevealRulesSystem
```
Purpose: Remove HiddenTag when ambush conditions met

Input:
  - Entities with HiddenTag + AmbushSlot assignment
  - Enemy positions (spatial query)
  - Weapon fire events

Output:
  - HiddenTag removed (entity becomes targetable)

Trigger Conditions:
  1. Enemy within AmbushSlot.Radius:
      distance(EnemyPos, Slot.Pos) <= Slot.Radius
  2. Unit fired weapon:
      WeaponFired event component present
  3. Enemy active sensor ping:
      Sensor sweep hit unit (rare, special ability)
  4. Max patience timer:
      Current time - Slot.OccupiedTick > MaxPatienceSeconds

Algorithm:
  For each hidden unit:
      Query enemies in spatial hash within slot radius
      If any found OR weapon fired OR timer expired:
          EntityCommandBuffer.RemoveComponent<HiddenTag>(unit)
          Add RevealedEvent (for FX/audio)

Burst-Friendly:
  - Spatial query via NativeMultiHashMap<int3, Entity> (grid-based)
  - Component removal via ECB (deferred)
```

### Game-Specific Adapter Systems

#### Space4X_JumpPlannerSystem
```
Purpose: Schedule micro-jumps for units with JumpCapability

Input:
  - Units with JumpCapability + Intercept state
  - Enemy positions
  - SensorField (to find sensor blind spots)

Output:
  - JumpCommand component (position, timing)

Algorithm:
  For each interceptor with JumpCapability:
      If direct path to intercept crosses high-risk cells:
          1. Calculate enemy sensor cone (from enemy position + facing)
          2. Find jump point:
              - Behind enemy (opposite to enemy facing)
              - Outside sensor cone (angle > 120°)
              - Within JumpRange
              - Preferably near asteroid/planet for cover
          3. Check constraints:
              - Cooldown elapsed?
              - Sufficient energy?
              - Jump point not in hazard?
          4. If valid:
              Add JumpCommand(target=jumpPoint, executeTick=now+5)
              Deduct energy
              Record LastJumpTick

Burst-Friendly:
  - Geometric calculations (dot products, angles)
  - No physics raycasts (use field data)

Integration:
  - MovementSystem checks for JumpCommand
  - On execute tick, teleport unit instantly
  - Apply jump FX (flash, warp effect) via presentation buffer
```

#### Godgame_FlankPathSystem
```
Purpose: Choose flank path around terrain when safer than direct

Input:
  - Units with FlankPathRef (prebaked flank routes)
  - Direct path risk (from pathfinding)

Output:
  - Updated NavigationCommand (switch to flank path)

Algorithm:
  For each unit with flank option:
      DirectRisk = sample PatrolNeedField along direct path
      FlankRisk = sample PatrolNeedField along flank path

      If FlankRisk < DirectRisk × 0.7:  // 30% safer
          Switch NavigationCommand to flank waypoints
          Add FlankingTag (for tactical bonus)
      Else:
          Use direct path

      Update periodically (every 2 seconds), not every frame

Burst-Friendly:
  - Path sampling via field lookups (no dynamic pathfinding)
  - Conditional component add via ECB

Tactical Bonus:
  - Units with FlankingTag get +20% attack bonus (surprise)
  - Removed after first attack
```

---

## Integration with Existing PureDOTS Systems

### HazardGrid Integration
```
Use Case: Patrols avoid hazards during movement

Connection:
  - PatrolBehaviorSystem writes NavigationCommand
  - MovementSystem reads NavigationCommand + HazardGrid
  - Avoidance applied automatically (existing system)

Special Case: Ambush Exits
  - Ambusher reveals, now needs to escape
  - Path out of ambush slot avoids hazards
  - May use JumpCapability (Space4X) or flank path (Godgame)
```

### Movement Model Integration
```
Use Case: Patrols move using existing movement physics

Connection:
  - Patrol units have MovementModelSpec component
  - PatrolBehaviorSystem sets desired velocity/heading
  - MovementSystem applies physics, handles collision avoidance

Patrol-Specific:
  - Corridor following uses waypoint navigation (existing)
  - Shadowing uses offset pursuit (calculate target velocity)
  - Intercept uses predicted intercept point (one-time waypoint)
```

### Compliance System Integration (Space4X)
```
Use Case: Border violations trigger sanctions

Connection:
  - OwnershipGrid defines borders
  - Patrols detect enemies in friendly territory (BorderField < 0 from enemy perspective)
  - ComplianceViolationEvent emitted
  - ComplianceSystem raises sanctions/hostility

Patrol Role as Enforcers:
  - Patrols challenge border violators (demand retreat)
  - If ignored, engage (legal under compliance rules)
  - Successful enforcement raises faction legitimacy
```

### Scenario Runner Integration
```
Use Case: Testing and debugging patrol systems

CLI Commands:
  - `patrol.spawn_corridor <faction> <borderDepth> <density>`
      → Force emit patrol tickets along faction's borders

  - `patrol.seed_ambush <faction> <count>`
      → Create ambush slots in low-sensor cells

  - `patrol.simulate_intrusion <enemyFaction> <duration>`
      → Spawn enemy units crossing border, measure intercept time

  - `patrol.debug_fields`
      → Export BorderField, SensorField, PatrolNeedField to images/JSON

Integration:
  - ScenarioRunner CLI invokes systems directly
  - Metrics exported for analysis (coverage, response time)
```

---

## Performance & Burst Optimization

### Field Update Costs

#### BorderField (SDF Calculation)
```
Complexity:
  - Worst case: O(cells × iterations)
  - Typical: O(border_cells × depth)

Optimization:
  - Only recompute affected region when ownership changes
  - Use jump flooding algorithm (parallel SDF, O(log n) iterations)
  - Burst-compile flood fill job

Budget:
  - 256×256 grid: ~2ms (one-time or infrequent)
  - 512×512 grid: ~10ms (acceptable for initialization)
```

#### SensorField (Coverage Map)
```
Complexity:
  - O(sensors × cells in range)

Optimization:
  - Spatial partitioning (only process cells in sensor radius)
  - Pre-bake static sensors (watchtowers) at map load
  - Dynamic sensors (units) update incrementally

Budget:
  - 50 sensors, 256×256 grid: ~1ms per frame
  - 200 sensors, 512×512 grid: ~5ms per frame (batch every 5 frames)
```

#### PatrolNeedField (Composite)
```
Complexity:
  - O(cells) (read from multiple fields, write once)

Optimization:
  - IJobParallelFor over cells
  - Burst-compile weighted sum calculation
  - Update only patrol band cells (not entire grid)

Budget:
  - 256×256 grid: <0.5ms per frame
  - Update every 60 ticks, not every frame
```

### Spatial Queries

#### Enemy Detection (Intercept Triggers)
```
Challenge:
  - "Find enemies within InterceptLookahead distance of each patrol"
  - Naive: O(patrols × enemies)

Optimization:
  - NativeMultiHashMap<int3, Entity> (grid-based spatial hash)
  - Hash enemy positions to grid cells
  - Patrol queries only nearby cells (3×3 or 5×5 region)
  - Complexity: O(patrols × nearby_enemies)

Budget:
  - 100 patrols, 200 enemies: <0.2ms with spatial hash
```

#### Ambush Slot Assignment
```
Challenge:
  - "Assign ambusher to nearest available ambush slot"

Optimization:
  - Maintain NativeList of available slots (unoccupied)
  - Sort by distance to spawner (or use spatial hash)
  - Assign to closest, mark occupied

Budget:
  - 20 ambush slots, 10 ambushers: <0.1ms
```

### Ticket Emission Determinism

#### Seeded RNG for Patrol Roles
```
Requirement:
  - Same patrol corridor, same frame → same patrol type

Implementation:
  - Seed = Hash(FactionId, CorridorPoint.x, CorridorPoint.z, FrameCount)
  - Use Unity.Mathematics.Random with seed
  - Roll for role: Scout (60%), Interceptor (30%), Ambusher (10%)

Determinism Guarantee:
  - Hash function is deterministic (bit-exact)
  - Random.NextFloat() is deterministic given same seed
  - Result: Perfect replay
```

### Memory Layout (SoA for Cache Efficiency)

#### Patrol Arrays
```
Instead of AoS (Array of Structs):
  struct Patrol { State state; float3 pos; float fuel; Entity target; }

Use SoA (Struct of Arrays):
  NativeArray<PatrolState> States;
  NativeArray<float3> Positions;
  NativeArray<float> FuelLevels;
  NativeArray<Entity> Targets;

Benefits:
  - Better cache locality when iterating over States alone
  - Parallel jobs can process disjoint arrays
  - Burst vectorization more effective

Trade-off:
  - More complex to author (multiple arrays)
  - Unity ECS already provides this via archetypes (components = SoA)
```

---

## Testing & Validation

### EditMode Tests (No Simulation)

#### BorderField_SDF_Correctness
```
Test:
  - Create 10×10 OwnershipGrid with known borders
  - Faction A owns left half, Faction B owns right half
  - Run BorderFieldBuildSystem
  - Assert:
      Distance at (4, 5) ≈ +1.0 (1 cell from border, friendly)
      Distance at (5, 5) = 0.0 (on border)
      Distance at (6, 5) ≈ -1.0 (1 cell from border, hostile)

Validation:
  - Signed distance correct within ε (0.01)
  - No NaN/Inf values
```

#### PatrolPolicy_Sanity
```
Test:
  - Create PatrolPolicy with extreme values
  - BorderDepth = -10 (invalid)
  - DensityPerKm = 1000 (too high)
  - AmbushProb = 1.5 (out of range)

Assert:
  - Validation system clamps/rejects:
      BorderDepth clamped to [0, MaxDepth]
      DensityPerKm clamped to [0, MaxDensity]
      AmbushProb clamped to [0, 1]
```

### PlayMode Tests (Simulation)

#### Patrols_Fill_Gaps_Deterministically
```
Test:
  1. Create border between two factions
  2. Set PatrolPolicy: DensityPerKm = 2.0
  3. Run PatrolPlannerSystem for 10 frames with fixed seed
  4. Count patrol tickets emitted
  5. Reset, run again with same seed
  6. Assert:
      Ticket count matches exactly
      Ticket positions match (bit-exact)
      Coverage gap metric reduced by ≥ 50%

Validation:
  - Determinism (same seed = same output)
  - Effectiveness (gaps actually filled)
```

#### Ambush_Reveal_Only_On_Trigger
```
Test:
  1. Spawn ambusher with HiddenTag at AmbushSlot
  2. Spawn enemy far away (outside slot radius)
  3. Run RevealRulesSystem for 100 ticks
  4. Assert: HiddenTag still present
  5. Move enemy inside slot radius
  6. Run RevealRulesSystem for 1 tick
  7. Assert: HiddenTag removed

Validation:
  - Ambushers stay hidden until triggered
  - Reveal immediate once condition met
```

#### Intercept_Point_Predictable
```
Test:
  1. Spawn enemy at (0, 0, 0), velocity (10, 0, 0) m/s
  2. Spawn patrol at (0, 50, 0), InterceptLookahead = 5s
  3. Run PatrolBehaviorSystem
  4. Calculate expected intercept point:
      EnemyPos + EnemyVel × 5 = (50, 0, 0)
  5. Assert: Patrol's intercept target within ε of (50, 0, 0)

Validation:
  - Intercept prediction accurate for constant velocity
  - Within 1m tolerance (acceptable for 50m distance)
```

#### JumpBehind_Respects_Cooldown (Space4X)
```
Test:
  1. Spawn unit with JumpCapability (Cooldown = 10s)
  2. Execute jump at T=0
  3. Try jump again at T=5s
  4. Assert: JumpCommand not added (cooldown not elapsed)
  5. Advance to T=11s
  6. Try jump again
  7. Assert: JumpCommand added (cooldown elapsed)

Validation:
  - Cooldown prevents spam
  - Timing exact (not approximate)
```

#### FlankPath_Lowers_Risk (Godgame)
```
Test:
  1. Create direct path (risk = 0.8)
  2. Create flank path (risk = 0.4)
  3. Spawn unit with both options
  4. Run Godgame_FlankPathSystem
  5. Assert: Unit switched to flank path (0.4 < 0.8 × 0.7)

Validation:
  - Risk comparison correct
  - Path switching deterministic
```

### Determinism Tests

#### Determinism_30_60_120
```
Test:
  - Run patrol scenario at 30Hz, 60Hz, 120Hz
  - Record final positions of all patrols after 300 frames
  - Assert: Positions match within ε (frame-rate independent)

Validation:
  - FixedStep ensures frame-rate independence
  - Position errors < 0.01m (floating-point tolerance)
```

#### Rewind_Replay_Bytewise
```
Test:
  1. Run patrol scenario, record state at T=100 ticks
  2. Continue to T=200 ticks
  3. Rewind: restore state from T=100
  4. Replay: simulate T=100 → T=200 again
  5. Assert: Final state at T=200 matches exactly (byte-for-byte)

Validation:
  - Perfect determinism (replay = original)
  - No drift (floating-point or RNG)
```

### Performance Benchmarks

#### Scenario Metrics (Export JSON)
```
Metrics to Capture:
  - border_km: Total border length
  - desired_patrols: DensityPerKm × border_km
  - active_patrols: Currently spawned
  - coverage_gap: PatrolNeedField metric (0-1, lower = better)
  - tickets_emitted: Per frame count
  - ambush_slots: Count of active slots
  - ambush_successes: Ambushes that triggered
  - intercepts: Successful intercept engagements
  - fixed_tick_ms: Time per FixedStep tick
  - alloc_bytes: Memory allocated per frame

Budgets:
  - fixed_tick_ms ≤ 16.6 (for 60Hz simulation)
  - alloc_bytes ≈ 0 (no per-frame allocations)
  - coverage_gap < 0.2 (80%+ border covered)
  - patrol_density_error < 0.1 (within 10% of desired)

Export Format (JSON):
  {
    "scenario": "Border Patrol Stress Test",
    "frame_count": 3600,
    "metrics": {
      "border_km": 12.5,
      "desired_patrols": 25,
      "active_patrols": 23,
      "coverage_gap": 0.15,
      ...
    }
  }
```

---

## Example Scenarios

### Godgame: Forest Border Patrol

```
Setup:
  - Two villages: Greenvale (faction 1), Ironhold (faction 2)
  - Border: Forest edge (50 cells, ~500m)
  - Terrain: Trees block line-of-sight (low SensorField)

PatrolPolicy (Greenvale):
  - BorderDepth: 100m (patrol just inside forest)
  - DensityPerKm: 3.0 (1.5 patrols along 500m border)
  - AmbushProb: 0.4 (high, due to forest cover)

Execution:
  1. BorderFieldBuilder: Identifies forest edge as border
  2. PatrolNeedField: Forest has low sensor coverage → high need
  3. PatrolPlanner: Emits 2 patrol tickets (1 Scout, 1 Ambusher)
  4. PatrolSpawner: Spawns light cavalry (Scout), hidden pike (Ambusher)
  5. Scout patrols along forest edge, spots Ironhold raider
  6. Scout transitions to Shadowing, follows raider at distance
  7. Raider approaches ambush slot (hidden pike in trees)
  8. RevealRules: Raider within slot radius → pike reveals
  9. Pike charges, raider surprised (flank bonus), defeated

Result:
  - Border defended without alerting enemy to full strength
  - Ambush successful due to low sensor coverage
```

### Space4X: Asteroid Belt Defense

```
Setup:
  - Two factions: Federation (faction 1), Rebels (faction 2)
  - Border: Asteroid belt (3D, 20km × 20km × 5km)
  - Hazard: Solar storm approaching from star

PatrolPolicy (Federation):
  - BorderDepth: 2000m (patrol inward from belt edge)
  - DensityPerKm: 1.5 (sparse, 3D space)
  - AmbushProb: 0.6 (asteroids provide cover)
  - InterceptLookahead: 30s (long-range sensors)

Execution:
  1. BorderFieldBuilder: Belt edge defined by ownership + asteroid density
  2. SensorField: Asteroids create sensor shadows (blind spots)
  3. PatrolPlanner: Emits 5 patrol tickets (2 Scouts, 2 Interceptors, 1 Ambusher)
  4. PatrolSpawner: Spawns corvettes (Scouts), destroyers (Interceptors), cloaked bomber (Ambusher)
  5. Solar storm detected, Federation fleets move behind asteroids (shadow system)
  6. Rebel raiders exploit storm, attack exposed Federation mining station
  7. Interceptor detects raiders (30s lookahead), calculates intercept
  8. JumpPlanner: Direct path crosses storm → schedule micro-jump behind raiders
  9. Interceptor jumps 8km, appears behind raiders (outside sensor cone)
  10. Raiders surprised, engaged from rear, forced to retreat

Result:
  - Border defended despite solar storm hazard
  - Micro-jump allowed flanking without storm exposure
  - Ambusher (cloaked bomber) remained hidden, deterrent effect
```

---

## Summary

**Unified Concept**: Field-based patrol planning using signed distance fields, sensor coverage, and threat assessment to autonomously deploy and maneuver patrols along borders.

**Game-Agnostic Core**:
- Border/sensor/need fields (same algorithms, 2D or 3D)
- Patrol planner (ticket emission)
- Patrol behavior (state machine)
- Ambush mechanics (hidden units, reveal triggers)

**Game-Specific Adapters**:
- Space4X: Micro-jump flanking, 3D sensor propagation
- Godgame: Flank paths around terrain, 2D line-of-sight

**Deterministic & Performant**:
- FixedStep only (no async)
- Seeded RNG (perfect replay)
- Burst-compiled (0.2-0.5ms per frame)
- Spatial partitioning (efficient queries)

**Integration**:
- HazardGrid (avoidance)
- Movement (physics)
- Compliance (border enforcement)
- ScenarioRunner (testing/debugging)

**Testing Strategy**:
- Field correctness (SDF accuracy)
- Determinism (replay, rewind)
- Performance (budgets, metrics)
- Gameplay (intercepts, ambushes, flanking)

This is ready for implementation when you are - all the conceptual pieces are in place!

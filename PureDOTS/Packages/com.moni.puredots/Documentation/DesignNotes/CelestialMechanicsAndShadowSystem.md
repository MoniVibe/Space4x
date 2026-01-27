# Celestial Mechanics and Shadow System (Game-Agnostic)

## Unified Concept

**Core Principle**: Everything in PureDOTS exists in relation to light sources (stars, suns). Entities orbit these light sources with rotation and velocity, creating day/night cycles and shadow mechanics.

**Game-Agnostic Truth**:
- **Godgame**: Plants orbit sun → day/night cycle → crops need sunlight, mushrooms need shade
- **Space4X**: Fleets orbit star → solar phenomena (storms, radiation) → hide behind asteroids/planets for protection
- **Underlying Mechanic**: Entity position relative to light source + occlusion by blocking entities = light/shadow state

## Conceptual Model

```
Light Source (Star/Sun)
    ↓ (emits light in all directions)
Blocking Entity (Planet/Asteroid/Mountain/Cave)
    ↓ (occludes light, casts shadow)
Receiving Entity (Plant/Fleet/Villager)
    ↓ (in light OR in shadow)
Result:
    - Light: Receives sunlight service (photosynthesis, warmth)
    - Shadow: Protected from light (mushrooms grow, fleets avoid storms)
```

### Examples Across Games

#### Godgame
```
Sun (light source)
    → Village (orbits sun with rotation)
        → Day: Crops receive sunlight → photosynthesis
        → Night: No sunlight → crops dormant
    → Cave (blocks sunlight)
        → Inside: Always shadow
        → Mushrooms grow in caves (avoid light)
    → Tree (casts local shadow)
        → Under tree: Partial shade
        → Shade-loving plants grow
```

#### Space4X
```
Star (light source)
    → Solar Storm (radiation phenomenon in light)
        → Fleet in open space: Takes damage from storm
    → Planet (blocks starlight)
        → Fleet behind planet: In shadow, protected from storm
    → Asteroid Field (partial occlusion)
        → Fleet hiding in asteroids: Reduced storm damage
```

## Component Architecture

### Light Sources
**Concept**: Entities that emit light in all directions.

**Data Needed**:
- Position (float3)
- Light radius (how far light reaches)
- Light intensity (0-1, affects gameplay effects)
- Light type (star, sun, magical orb, etc.)
- Emission pattern (omnidirectional, directional, etc.)

**Examples**:
- Sun in Godgame: Radius = infinite, Intensity = 1.0
- Star in Space4X: Radius = solar system bounds, Intensity = varies by distance
- Torch in dungeon: Radius = 10 units, Intensity = 0.5

**Burst Notes**:
- Store light sources in a separate archetype for efficient queries
- Use spatial hash or octree for "find nearest light source" queries
- Light intensity can be pre-calculated per zone/sector to avoid per-entity calculations

---

### Orbital Mechanics
**Concept**: Entities orbit light sources with rotation and velocity, creating cycles.

**Data Needed**:
- Orbit center (Entity reference to light source)
- Orbit radius (distance from center)
- Orbit speed (radians per tick)
- Current angle (0-2π)
- Rotation speed (day/night cycle speed)
- Current rotation angle (0-2π for local day/night)

**Calculation**:
```
Position = OrbitCenter.Position +
           (cos(OrbitAngle) * OrbitRadius, 0, sin(OrbitAngle) * OrbitRadius)

OrbitAngle += OrbitSpeed * DeltaTime
RotationAngle += RotationSpeed * DeltaTime

IsDay = RotationAngle between 0-π
IsNight = RotationAngle between π-2π
```

**Burst Notes**:
- Pure math operations, easily Burst-compiled
- No branching needed (use math.select for IsDay/IsNight)
- Can batch-process all orbiting entities in parallel job
- Store orbit data in SoA format for cache efficiency

**Variations**:
- Fixed orbit: OrbitSpeed = constant (planets)
- Variable orbit: OrbitSpeed = f(time) (comets, elliptical orbits)
- No orbit: OrbitSpeed = 0, but RotationSpeed > 0 (rotating platform)

---

### Shadow Receivers
**Concept**: Entities that care about light/shadow state.

**Data Needed**:
- Is in light (bool or 0-1 light level)
- Light source entity reference
- Required light level (minimum for effect)
- Shadow tolerance (can survive partial shade?)
- Last light update tick (for caching)

**Effects**:
- **Godgame Plants**:
  - Crops: RequiredLight = 0.7+ (need full sun)
  - Shade plants: RequiredLight = 0.3-0.6 (partial shade)
  - Mushrooms: RequiredLight = 0.0-0.2 (must be in shadow)

- **Space4X Fleets**:
  - In light during storm: TakeDamage(stormIntensity)
  - In shadow during storm: Protected
  - In light normally: Visual detection range increased

**Burst Notes**:
- Store light level as float (0-1) rather than bool for gradients
- Use ComponentLookup for light source queries
- Cache light state per zone/sector instead of per-entity when possible

---

### Shadow Casters
**Concept**: Entities that block light and create shadows.

**Data Needed**:
- Shadow casting enabled (bool)
- Occlusion radius (how large is the shadow cone)
- Occlusion height (vertical extent)
- Shadow strength (full occlusion or partial?)
- Bounds (AABB or sphere for quick rejection)

**Shadow Types**:
1. **Hard Shadow**: Complete occlusion (planet blocks starlight)
2. **Soft Shadow**: Partial occlusion (asteroid field reduces light)
3. **Local Shadow**: Small-scale (tree, building)
4. **Volumetric Shadow**: 3D occlusion (cave, underground)

**Burst Notes**:
- Spatial partitioning essential (octree, BVH, spatial hash)
- Broad-phase rejection using AABBs before raycast
- Batch raycasts using Unity.Physics or custom raycast job
- Consider light baking for static shadow casters (terrain, buildings)

---

## System Architecture

### 1. Orbital Update System
**Purpose**: Update positions of orbiting entities.

**Process**:
```
For each entity with OrbitalComponent:
    1. Update orbit angle based on speed
    2. Calculate new position
    3. Update rotation angle
    4. Determine local time (day/night based on rotation)
    5. Write to Transform component
```

**Burst Approach**:
- Simple math job, no dependencies
- Parallel processing (IJobEntity or IJobParallelFor)
- No structural changes, pure data transformation
- Schedule early in frame (other systems depend on positions)

**Optimizations**:
- Pre-calculate orbit positions for fixed orbits
- Use lookup tables for sin/cos if orbit speeds are discrete
- Skip entities that haven't moved (static orbits)

---

### 2. Shadow Casting System
**Purpose**: Determine which entities are in light vs shadow.

**Process**:
```
For each ShadowReceiver:
    1. Find nearest light source(s)
    2. For each light source:
        a. Query spatial structure for potential shadow casters between receiver and light
        b. For each potential caster:
            i. Check if caster actually blocks line-of-sight (raycast or analytical)
            ii. Calculate occlusion strength
        c. Accumulate total light level (1.0 - sum of occlusions)
    3. Update receiver's light level
    4. Trigger effects if light level changed thresholds
```

**Burst Approach**:
- Use spatial hash for "entities between A and B" queries
- Batch raycasts if using physics (Unity.Physics supports this)
- Analytical shadow calculation for simple shapes (sphere-sphere, sphere-plane)
- Store results in NativeArray for next frame's dependency chains

**Optimizations**:
- **Zone-based shadowing**: Pre-calculate light levels for grid sectors
  - Receivers just look up their sector's light level
  - Only recalculate sectors when shadow casters move
  - Godgame: 10x10 grid sectors, recalculate on day/night transitions
  - Space4X: Octree sectors, recalculate when fleets/asteroids move

- **Hierarchical shadowing**:
  - Large shadow casters (planets) checked first
  - If fully occluded, skip smaller casters
  - Reduces raycast count by 80%+

- **Temporal caching**:
  - If receiver and all nearby casters haven't moved, light level unchanged
  - Cache light level for N ticks before recalculating

---

### 3. Light Effect System
**Purpose**: Apply gameplay effects based on light/shadow state.

**Process**:
```
For each entity with light-dependent behavior:
    1. Read light level from ShadowReceiver
    2. Apply effects:
        - Godgame: Photosynthesis rate, growth multiplier
        - Space4X: Storm damage, detection range
    3. Update buffs/debuffs
```

**Burst Approach**:
- Read-only access to ShadowReceiver components
- Write to effect components (ResourceRate, DamageOverTime, etc.)
- Can run in parallel with other effect systems
- Schedule after Shadow Casting System

**Integration Points**:
- **Godgame ResourceGatherSystem**: Photosynthesis rate = LightLevel * BaseRate
- **Space4X DamageSystem**: If LightLevel > 0.5 && StormActive, ApplyStormDamage
- **Villager Mood**: Some villagers prefer day, others prefer night (personality)

---

## Shadow Calculation Methods

### Method 1: Raycast (Accurate, Expensive)
**Use Case**: When precision matters, few receivers.

**Process**:
```
For each receiver-light pair:
    Cast ray from receiver to light source
    If ray hits shadow caster:
        Calculate occlusion based on caster size and distance
        Accumulate shadow strength
```

**Pros**:
- Physically accurate
- Handles complex geometry
- Works with Unity.Physics

**Cons**:
- Expensive (N receivers × M lights × K casters per frame)
- Requires physics colliders on shadow casters

**Burst Implementation**:
- Use Unity.Physics RaycastCommand batch API
- Schedule job with Physics dependency
- Process results in follow-up job

**When to Use**:
- Space4X: Fleets hiding from solar phenomena (few fleets, critical accuracy)
- Godgame: Special plants in complex terrain (rare edge cases)

---

### Method 2: Analytical (Fast, Simple Geometry)
**Use Case**: When shadow casters are simple shapes (spheres, cylinders).

**Process**:
```
For each receiver:
    For each shadow caster:
        If caster is sphere:
            Calculate angle subtended by sphere from receiver viewpoint
            If light source within that angle, occluded
            Occlusion strength = f(sphere radius, distance)
```

**Math**:
```
Vector from receiver to light: L = LightPos - ReceiverPos
Vector from receiver to caster: C = CasterPos - ReceiverPos

Dot product: dot(L, C)
If dot < 0, caster is behind receiver (no shadow)

Angular radius of caster sphere:
    θ = arcsin(CasterRadius / distance(Receiver, Caster))

If angle between L and C < θ, receiver is in shadow
```

**Pros**:
- Very fast (pure math, no physics)
- Deterministic
- Burst-friendly (no managed code)

**Cons**:
- Only works for simple shapes
- No complex geometry support

**Burst Implementation**:
- IJobParallelFor over all receivers
- ComponentLookup for shadow casters
- All math.* operations (Burst-compatible)

**When to Use**:
- Space4X: Planets/asteroids are spheres (most common case)
- Godgame: Sun/moon shadows with spherical approximation

---

### Method 3: Sector-Based (Fastest, Approximate)
**Use Case**: When many receivers, light changes slowly.

**Process**:
```
1. Divide world into sectors (grid or octree)
2. For each sector:
    Calculate average light level based on shadow casters
    Store in sector data
3. Receivers just look up their sector's light level
4. Only recalculate sectors when:
    - Shadow casters move into/out of sector
    - Light source moves (day/night transition)
```

**Sector Calculation**:
```
For sector S:
    Center point = S.Center
    Cast 1 ray from center to each light source
    If occluded, LightLevel = 0
    Else, LightLevel = 1.0 * (distance falloff)
    All receivers in S use this value
```

**Pros**:
- Amortizes cost over many receivers
- Extremely fast lookups (O(1) per receiver)
- Can be async/background thread

**Cons**:
- Less accurate (receivers share light level)
- Granularity limited by sector size

**Burst Implementation**:
- IJob for sector updates (not parallel, but infrequent)
- Parallel job for receivers reading sectors
- Store sector data in NativeHashMap<int3, float> (grid coords → light level)

**When to Use**:
- Godgame: Thousands of plants, day/night transitions only
- Space4X: Large-scale fleet positioning, don't need per-ship precision

---

## Godgame Integration

### Day/Night Cycle
```
Sun Entity:
    - LightSource: Radius = infinite, Intensity = 1.0
    - Position: Fixed at (0, 1000, 0) (high above world)

Village Entity:
    - OrbitalComponent: OrbitCenter = Sun, OrbitRadius = 0 (doesn't orbit)
    - RotationSpeed: 2π / (24 * 3600 ticks) = 1 full rotation per day
    - CurrentRotation: 0-2π

Plants:
    - ShadowReceiver: LightSource = Sun
    - Check: dot(PlantUp, SunDirection) > 0 = Day
              dot(PlantUp, SunDirection) < 0 = Night
    - Photosynthesis active only during Day

Alternative (Simpler):
    - Don't actually rotate village, just track time of day
    - LocalTime = (CurrentTick % DayLength) / DayLength
    - IsDay = LocalTime < 0.5
    - Skip orbital math entirely, purely time-based
```

### Cave/Shadow Mechanics
```
Cave Entity:
    - ShadowCaster: Enabled = true, OcclusionStrength = 1.0 (full shadow)
    - Bounds: AABB of cave interior

Mushroom (inside cave):
    - ShadowReceiver: RequiredLight = 0.0-0.2 (needs shadow)
    - Shadow Casting System checks:
        If Mushroom.Position inside Cave.Bounds:
            LightLevel = 0.0 (cave fully occludes sun)
        Mushroom.CanGrow = true

Tree Entity:
    - ShadowCaster: Enabled = true, OcclusionStrength = 0.6 (partial shade)
    - Bounds: Cylinder (trunk + canopy)

Shade Plant (under tree):
    - ShadowReceiver: RequiredLight = 0.3-0.6
    - Shadow Casting System checks:
        Ray from Plant to Sun hits Tree:
            LightLevel = 1.0 - Tree.OcclusionStrength = 0.4
        Shade Plant.CanGrow = true (0.4 in required range)
```

---

## Space4X Integration

### Solar Phenomena
```
Star Entity:
    - LightSource: Radius = solar system, Intensity = varies
    - Emits: SolarStorm events periodically

SolarStorm Entity:
    - StormRadius: Cone emanating from star
    - StormIntensity: Damage per tick to exposed entities
    - Duration: How long storm lasts

Fleet Entity:
    - ShadowReceiver: LightSource = Star
    - StormDamage System:
        If LightLevel > 0.5 && StormActive:
            TakeDamage(StormIntensity * LightLevel)
        If LightLevel < 0.3:
            Protected (in shadow, no damage)

Planet Entity:
    - ShadowCaster: Enabled = true, OcclusionStrength = 1.0
    - Bounds: Sphere (planet radius)

Tactical Decision:
    - Fleet detects incoming solar storm
    - Pathfinding: Move behind planet to enter shadow
    - Result: Fleet protected from storm
```

### Asteroid Field Shadows
```
AsteroidField Entity (aggregate):
    - Contains many asteroid entities
    - ShadowCaster: OcclusionStrength = 0.5 (partial)
    - Bounds: AABB of field

Fleet (hiding in field):
    - Shadow Casting System:
        Multiple small asteroids between Fleet and Star
        Each asteroid contributes partial occlusion
        Total LightLevel = 1.0 - sum(occlusions) = 0.4
    - StormDamage reduced to 40% of normal

Strategic Depth:
    - Players position fleets in asteroid belts for protection
    - Trade-off: Harder to detect enemies in shadows, but also harder to see out
```

---

## Performance Considerations

### Scalability Targets

#### Godgame
```
Entities:
    - 1 Sun (light source)
    - 500 Plants (shadow receivers)
    - 50 Trees/Buildings (shadow casters)
    - 20 Caves (volumetric shadows)

Budget:
    - Shadow update: 0.2ms per frame
    - Acceptable: Sector-based (recalculate only on day/night)
    - Method: Analytical for trees, bounds check for caves
```

#### Space4X
```
Entities:
    - 3-5 Stars (light sources)
    - 50 Fleets (shadow receivers)
    - 20 Planets (shadow casters)
    - 5 Asteroid Fields (complex shadow casters)

Budget:
    - Shadow update: 0.5ms per frame
    - Acceptable: Analytical for planets, raycast for asteroids
    - Method: Per-fleet precision (tactical importance)
```

---

### Optimization Strategies

#### 1. Spatial Partitioning
```
Use spatial hash or octree for:
    - "Find shadow casters near this receiver"
    - Broad-phase culling (don't raycast distant entities)

Implementation:
    - Unity.Entities doesn't have built-in spatial queries
    - Options:
        a) Custom NativeHashMap<int3, NativeList<Entity>>
        b) Unity.Physics queries (if colliders present)
        c) BVH (Bounding Volume Hierarchy) for static casters

Recommendation: Hybrid
    - Static casters: BVH, built once
    - Dynamic casters: Spatial hash, rebuilt per frame
```

#### 2. LOD (Level of Detail)
```
Distance-based shadow fidelity:

Close range (< 50 units):
    - Full raycast or analytical per receiver
    - Accurate shadow edges

Medium range (50-200 units):
    - Sector-based light levels
    - Approximate shadows

Far range (> 200 units):
    - Don't calculate shadows at all
    - Assume full light or full shadow based on heuristic

Godgame: Camera distance determines LOD
Space4X: Tactical zoom level determines LOD
```

#### 3. Update Frequency
```
Not all receivers need every-frame updates:

Critical (every frame):
    - Space4X fleets in active combat
    - Godgame plants on screen edge during fast day/night

Normal (every 5-10 frames):
    - Most plants
    - Fleets not in combat

Low priority (every 60 frames):
    - Off-screen plants
    - Distant fleets

Implementation:
    - Stagger updates using (Entity.Index % UpdateInterval) == CurrentFrame
    - Burst-friendly (no allocations)
```

#### 4. Dirty Tracking
```
Only recalculate shadows when something changes:

Mark dirty when:
    - Shadow caster moves
    - Shadow receiver moves
    - Light source moves/changes intensity
    - Day/night transition

Skip calculation when:
    - All entities static
    - No light changes

Implementation:
    - Version numbers on components
    - Compare LastUpdateVersion with current
    - Burst-friendly (simple int comparison)
```

---

## Data Flow Example

### Godgame: Crop Growth in Day/Night

```
Frame Start:
    1. OrbitalUpdateSystem:
        - Sun stays at fixed position
        - Village rotation angle += RotationSpeed * DeltaTime
        - Village.LocalTime = (RotationAngle / 2π) * 24 hours

    2. ShadowCastingSystem (sector-based):
        - Check if LocalTime crossed day/night boundary
        - If yes:
            For each sector:
                If Village.LocalTime in [6, 18]: LightLevel = 1.0 (day)
                Else: LightLevel = 0.0 (night)
        - Plants read their sector's LightLevel

    3. PhotosynthesisSystem:
        - For each plant with ShadowReceiver:
            If LightLevel > Plant.RequiredLight:
                ResourceGatherRate = BaseRate * LightLevel
            Else:
                ResourceGatherRate = 0 (dormant at night)

Result:
    - Crops produce resources during day
    - Mushrooms produce during night
    - Shade plants produce in partial shade (under trees)
```

### Space4X: Fleet Avoiding Solar Storm

```
Frame Start:
    1. OrbitalUpdateSystem:
        - Fleet orbits star with velocity
        - Fleet.Position = calculate_orbit_position()

    2. SolarStormSystem:
        - Storm detected approaching fleet
        - Fleet AI: FindShadow(Storm.Direction)

    3. PathfindingSystem:
        - Calculate path to nearest planet shadow
        - Path = A* from Fleet to Planet.ShadowZone

    4. ShadowCastingSystem (analytical):
        - For Fleet:
            Vector FleetToStar = normalize(Star.Pos - Fleet.Pos)
            For each Planet:
                Vector FleetToPlanet = normalize(Planet.Pos - Fleet.Pos)
                If dot(FleetToStar, FleetToPlanet) > cos(shadow_angle):
                    Fleet in shadow cone
                    LightLevel = 0.0
                    Break (found shadow)

    5. StormDamageSystem:
        - For each Fleet with ShadowReceiver:
            If Storm.Active && LightLevel > 0.5:
                TakeDamage(StormIntensity * LightLevel)
            Else:
                No damage (in shadow)

Result:
    - Fleet reaches planet shadow before storm
    - No damage taken
    - Gameplay: Strategic positioning matters
```

---

## Technical Recommendations

### Component Design
```
Prefer composition over monolithic components:

Good:
    - OrbitalComponent (orbit data only)
    - RotationComponent (rotation data only)
    - ShadowReceiverComponent (light state only)
    - LightSourceComponent (emission data only)

Bad:
    - CelestialBodyComponent (orbit + rotation + light + shadow = bloated)

Rationale:
    - Systems can query only what they need
    - Easier to Burst-compile (smaller data sets)
    - Better cache coherency
```

### System Scheduling
```
Dependency chain:

OrbitalUpdateSystem (writes: Position, Rotation)
    ↓
ShadowCastingSystem (reads: Position, Rotation; writes: LightLevel)
    ↓
LightEffectSystem (reads: LightLevel; writes: ResourceRate, Damage, etc.)

Schedule:
    - OrbitalUpdateSystem in TransformSystemGroup (early)
    - ShadowCastingSystem in SimulationSystemGroup (mid)
    - LightEffectSystem in SimulationSystemGroup (late)

Use [UpdateAfter] attributes to enforce order
```

### Burst Compatibility
```
All systems should be Burst-compatible:

Required:
    - No managed types (string, object, etc.)
    - Use FixedString for names
    - Use NativeArray/NativeList for collections
    - Use ComponentLookup for entity references
    - math.* functions (not Mathf.*)

Shadow Casting Job Example (conceptual):
    [BurstCompile]
    struct ShadowCastJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LightSource> LightSources;
        [ReadOnly] public ComponentLookup<ShadowCaster> Casters;
        [ReadOnly] public NativeHashMap<int3, NativeList<Entity>> SpatialHash;

        void Execute(ref ShadowReceiver receiver, in LocalTransform transform)
        {
            // Pure math, no allocations, Burst-friendly
        }
    }
```

---

## Future Extensions

### Advanced Features (Optional)

1. **Multiple Light Sources**
   - Multiple suns (binary star systems)
   - Accumulate light from all sources
   - Complex shadow overlaps

2. **Colored Lighting**
   - Red dwarf star → red light → affects plant growth differently
   - Blue giant → high radiation → more damage
   - Store Light.Color (float3 RGB)

3. **Atmospheric Scattering**
   - Planets with atmospheres diffuse light
   - Shadow edges are softer (penumbra)
   - OcclusionStrength varies with atmosphere density

4. **Time Dilation**
   - Relativistic effects near massive bodies
   - Time passes slower → orbit speed appears faster
   - Space4X only (Godgame doesn't need this)

5. **Eclipse Events**
   - Moon passes between sun and planet
   - Total shadow for brief period
   - Triggers special events (rituals in Godgame, tactical opportunity in Space4X)

---

## Summary

**Unified Mechanic**: Entities orbit light sources → shadow casting determines light/shadow state → gameplay effects

**Implementation Approach**:
- Components: Orbital, Rotation, LightSource, ShadowCaster, ShadowReceiver
- Systems: OrbitalUpdate, ShadowCasting, LightEffect
- Shadow Calculation: Raycast (accurate), Analytical (fast), Sector-based (fastest)

**Burst-Friendly**:
- Pure math operations
- Spatial partitioning (NativeHashMap, BVH)
- Batch processing (IJobParallelFor)
- No managed code

**Game Integration**:
- Godgame: Day/night affects photosynthesis, caves provide shade for mushrooms
- Space4X: Fleets hide behind planets to avoid solar storms

**Performance**: 0.2-0.5ms per frame for hundreds of entities using optimizations (spatial partitioning, LOD, dirty tracking, sector-based)

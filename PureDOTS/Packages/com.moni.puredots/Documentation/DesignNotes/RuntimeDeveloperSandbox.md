# Runtime Developer Sandbox & Live Tweaking System

## Core Concept

**Vision**: A game-agnostic developer suite that allows real-time manipulation of any entity, component, or world property during simulation—no stops, no rebuilds, instant feedback.

**Use Cases**:
- **Rapid prototyping**: "What if this unit was 10x faster?"
- **Stress testing**: "Can the pathfinding handle a 1000-unit swarm?"
- **Balancing**: "How does combat feel with 2x damage?"
- **Debugging**: "Why is this entity stuck? Let me teleport it."
- **Content creation**: "Let me tweak these values until it feels right, then export"

**Key Requirement**: Everything must be **serializable** and **reflectable** for runtime inspection and modification.

---

## Design Principles

### 1. Runtime-First
```
Traditional Unity workflow:
  - Stop simulation
  - Change inspector value
  - Restart simulation
  - Hope you remember what you changed

PureDOTS Sandbox workflow:
  - Simulation running
  - Adjust slider
  - See instant effect
  - Snapshot config
  - Continue iterating
```

### 2. Determinism-Aware
```
Challenge: Live tweaking can break replay

Solution: Log all tweaks as "override events"
  - Replay system can apply same overrides at same tick
  - Or: Disable determinism during sandbox mode (flag it clearly)
  - Or: Separate "sandbox timeline" from "canonical simulation"

Recommendation: Three modes
  - Production: No tweaking, full determinism
  - Sandbox: Tweaking allowed, determinism optional
  - Replay: Apply logged tweaks deterministically
```

### 3. Serialization as Foundation
```
Everything exposed to sandbox must be serializable:
  - Components → JSON/binary
  - Blobs → JSON (readable) + binary (compact)
  - Entities → Full state snapshot
  - World state → Complete scene dump

Why:
  - Save/load configurations
  - Copy/paste between entities
  - Export to authoring assets
  - Share stress test scenarios
```

### 4. Presentation-Agnostic
```
Same backend, multiple frontends:
  - ImGui (in-game overlay)
  - Unity Editor window (development)
  - Web dashboard (remote debugging)
  - CLI (automated stress tests)
  - JSON API (external tools)

Core: "TweakingService" that handles mutations
Frontend: Sends commands to service, displays state
```

---

## Architecture Overview

### Component Registry & Reflection

**Problem**: ECS components are structs with no runtime type info.

**Solution**: Maintain a **ComponentTypeRegistry** with metadata:

```
ComponentTypeRegistry:
  - Maps ComponentType → Metadata

ComponentMetadata:
  - TypeName (string)
  - Fields (array of FieldMetadata)
  - Size (bytes)
  - Serializer (func)
  - Deserializer (func)
  - UIRenderer (func for sliders/fields)

FieldMetadata:
  - FieldName (string)
  - FieldType (float, int, bool, Entity, etc.)
  - Offset (byte offset in struct)
  - Range (min/max for sliders)
  - Units (m/s, kg, etc.)
  - Tooltip (description)
```

**Implementation Approach**:
1. **Source Generators** (C#): Auto-generate registry from component definitions
2. **Reflection** (fallback): Use C# reflection at startup to populate registry
3. **Manual Registration** (custom types): Explicit registration for special cases

**Example Generated Entry**:
```
Component: MovementModelSpec
Fields:
  - MaxSpeed (float, range: 0-1000, units: m/s)
  - Acceleration (float, range: 0-100, units: m/s²)
  - TurnRate (float, range: 0-360, units: deg/s)
  - Mass (float, range: 0.1-10000, units: kg)
Serializer: ToJson_MovementModelSpec
UIRenderer: RenderSliders_MovementModelSpec
```

### Entity Inspector

**Concept**: Select any entity at runtime, view/edit all components.

**Features**:
1. **Entity Selection**:
   - Click in game (raycast to Entity)
   - Search by name/ID/type
   - Filter by archetype (e.g., "show all fleets")
   - Bookmark entities (quick access)

2. **Component View**:
   - List all components on entity
   - Expand/collapse sections
   - Color-coded (data, tags, buffers)
   - Show related entities (references)

3. **Live Editing**:
   - Sliders for numeric fields
   - Toggles for bools
   - Dropdowns for enums
   - Text fields for strings
   - Entity pickers for Entity references
   - Vector editors (X/Y/Z sliders)

4. **Component Management**:
   - Add component (from registry)
   - Remove component
   - Copy component data
   - Paste component data
   - Reset to default

**UI Layout (Conceptual)**:
```
[Entity Inspector: Fleet_Alpha (ID: 12345)]

  [Components]
    ▼ LocalTransform (data)
      Position: [ X: 150.2 ][ Y: 300.5 ][ Z: 0.0 ]
      Rotation: [ X: 0.0   ][ Y: 45.0  ][ Z: 0.0 ]
      Scale:    [ 1.5 ]

    ▼ MovementModelSpec (data)
      MaxSpeed:      [====|====] 500 m/s (range: 0-1000)
      Acceleration:  [==|======] 50 m/s² (range: 0-100)
      TurnRate:      [===|=====] 90 deg/s (range: 0-360)
      Mass:          [======|==] 1500 kg (range: 0.1-10000)

    ▼ HealthComponent (data)
      CurrentHP:     [======|==] 750 / 1000
      Regeneration:  [=|=======] 5 HP/s

    ▶ Fleet (aggregate)
    ▶ PatrolState (data)
    • SpeedBuff (tag)
    • Selected (tag)

  [Actions]
    [Add Component ▼] [Remove Component] [Clone Entity] [Delete Entity]
    [Save Preset]     [Load Preset]      [Reset All]
```

---

## Global Tweaks & World Settings

### Time Control
```
Global Time Settings:
  - TimeScale (float, 0.0-10.0):
      1.0 = normal speed
      0.1 = slow motion (10% speed)
      5.0 = fast forward (5x speed)
      0.0 = pause (freeze simulation)

  - FixedDeltaTime (float, 0.001-1.0):
      Adjust simulation step size
      Lower = more accurate, slower
      Higher = faster, less accurate

  - TicksPerSecond (int, 1-120):
      Simulation frequency
      60 = 60 ticks/second
      120 = high precision

Implementation:
  - Unity.Core.TimeData override
  - Affects all FixedStep systems
  - Presentation layer unaffected (always renders at display Hz)
```

### Physics Overrides
```
Global Physics Settings:
  - Gravity (float3):
      Godgame: (0, -9.81, 0) (default)
      Space4X: (0, 0, 0) (zero-g)
      Sandbox: (0, 100, 0) (reverse gravity!)

  - DragCoefficient (float, 0-10):
      Higher = more air resistance
      0 = no drag (space)
      1 = normal (air)
      5 = underwater

  - CollisionEnabled (bool):
      Toggle all collision detection
      Useful for testing pathfinding without physics
```

### World Bounds
```
Spatial Settings:
  - WorldSize (float3):
      Dynamically resize playable area
      Useful for stress testing large maps

  - ChunkSize (int):
      Grid cell size for spatial partitioning
      Smaller = more precise, slower
      Larger = faster, less precise

  - CullingDistance (float):
      How far to simulate entities
      Reduce for performance testing
```

---

## Preset System (Save/Load Configurations)

### Entity Presets
```
Concept: Save entity configuration, apply to other entities

Use Case:
  1. Tweak unit until perfect (sliders, combat stats, movement)
  2. Save as preset: "SuperScout_v3"
  3. Apply preset to 50 other scouts
  4. All scouts now have exact same stats

Preset Format (JSON):
  {
    "name": "SuperScout_v3",
    "archetype": "Fleet",
    "components": [
      {
        "type": "MovementModelSpec",
        "data": {
          "MaxSpeed": 850.0,
          "Acceleration": 75.0,
          "TurnRate": 180.0,
          "Mass": 500.0
        }
      },
      {
        "type": "HealthComponent",
        "data": {
          "MaxHP": 500,
          "CurrentHP": 500,
          "Regeneration": 10.0
        }
      }
    ]
  }

Operations:
  - Save: Serialize selected entity to preset file
  - Load: Apply preset to selected entity (merge/replace)
  - Batch Apply: Apply to multiple entities at once
  - Export to Authoring: Generate ScriptableObject from preset
```

### World Presets (Scenarios)
```
Concept: Save entire world state for stress testing

Use Case:
  1. Set up stress test: 1000 units, extreme speeds, complex terrain
  2. Save as scenario: "PathfindingStressTest_1000units"
  3. Load anytime to reproduce exact conditions
  4. Share with team for collaborative testing

Scenario Format (JSON):
  {
    "name": "PathfindingStressTest_1000units",
    "worldSettings": {
      "TimeScale": 1.0,
      "Gravity": [0, -9.81, 0],
      "WorldSize": [5000, 500, 5000]
    },
    "entities": [
      { "prefab": "Scout", "count": 500, "distribution": "random" },
      { "prefab": "Tank", "count": 300, "distribution": "line" },
      { "prefab": "Obstacle", "count": 200, "positions": [...] }
    ],
    "overrides": [
      { "entityFilter": "type:Scout", "component": "MovementModelSpec", "field": "MaxSpeed", "value": 1000.0 }
    ]
  }

Operations:
  - Save Scenario: Full world snapshot
  - Load Scenario: Destroy current, spawn from scenario
  - Merge Scenario: Add entities to existing world
  - Compare Scenarios: Diff two scenarios (what changed?)
```

---

## Stress Test Templates

### Predefined Stress Tests

#### 1. Speed Stress Test
```
Template: "Extreme Speed"

Modifications:
  - All units: MaxSpeed × 10
  - All units: Acceleration × 10
  - TimeScale: 5.0 (fast forward)

Expected Behavior:
  - Units move at extreme speeds
  - Test collision detection at high velocity
  - Observe pathfinding with rapid direction changes

Metrics:
  - Frame time (should stay <16.6ms)
  - Collision misses (should be zero)
  - Pathfinding errors (count)
```

#### 2. Giant Entity Test
```
Template: "Kaiju Mode"

Modifications:
  - Selected entity: Scale × 100
  - Selected entity: Mass × 1000
  - Selected entity: HP × 100

Expected Behavior:
  - Massive entity dominates battlefield
  - Test collision with huge AABB
  - Observe spatial partitioning efficiency

Metrics:
  - Spatial query time (find nearby entities)
  - Rendering performance (large mesh)
  - Pathfinding around giant obstacle
```

#### 3. Instant Movement Test
```
Template: "Teleport Mode"

Modifications:
  - Selected entity: MaxSpeed = float.MaxValue
  - Selected entity: Acceleration = float.MaxValue
  - Physics: DragCoefficient = 0

Expected Behavior:
  - Entity reaches destination instantly
  - Test interpolation smoothness
  - Observe presentation layer lag compensation

Metrics:
  - Simulation position vs presentation position delta
  - Jitter/stuttering in rendering
```

#### 4. Swarm Stress Test
```
Template: "1000-Unit Swarm"

Modifications:
  - Spawn 1000 units at origin
  - All units: same target destination
  - All units: collision enabled

Expected Behavior:
  - Test spatial partitioning scalability
  - Observe collision resolution (units separate)
  - Measure pathfinding performance

Metrics:
  - Frame time per 100 units
  - Collision checks per frame
  - Memory allocations
```

#### 5. Zero Gravity Test (Space4X)
```
Template: "Newtonian Physics"

Modifications:
  - Physics: Gravity = (0, 0, 0)
  - All units: DragCoefficient = 0
  - All units: No auto-braking

Expected Behavior:
  - Units drift indefinitely
  - Test momentum conservation
  - Observe orbital mechanics

Metrics:
  - Energy conservation (kinetic energy stable)
  - Drift distance after thrust stop
```

---

## Live Patching (Hot Reload)

### Component Hot Swap
```
Concept: Change component values without destroying/recreating entity

Traditional ECS:
  - Remove old component
  - Add new component with modified values
  - Entity archetype changes (expensive)

Live Patching:
  - Directly modify component data in-place
  - No archetype change
  - Instant, zero allocations

Implementation Approach:
  1. Get component data pointer: EntityManager.GetComponentDataRW<T>()
  2. Modify fields directly via reflection or generated setters
  3. Mark component as "dirty" for dependent systems
  4. No structural changes, no ECB needed

Caveat:
  - Only works if archetype doesn't change
  - Can't add/remove components this way (use ECB for that)
  - Must be done in MainThread system, not Job
```

### Blob Hot Reload
```
Concept: Modify blob data (e.g., stats catalog) without reloading assets

Challenge:
  - Blobs are immutable (BlobAssetReference<T>)
  - Can't modify in-place

Solution:
  1. Create new blob with modified data
  2. Replace BlobAssetReference on all entities using old blob
  3. Dispose old blob
  4. Systems automatically use new blob (reference updated)

Use Case:
  - Tweak unit stats catalog (e.g., "Scout MaxSpeed = 500 → 800")
  - All scout entities immediately use new stats
  - No entity recreation needed

Implementation:
  - BlobBuilder creates new blob
  - ComponentLookup<StatCatalogRef> updates all references
  - Old blob disposed after next frame (ensure no jobs using it)
```

---

## UI/UX Concepts

### Slider Modes

#### 1. Linear Slider
```
Use: Most numeric fields with known range

Example: MaxSpeed (0-1000 m/s)
  [====|====] 500

  Drag to adjust
  Click to type exact value
  Scroll wheel for fine adjustment
```

#### 2. Logarithmic Slider
```
Use: Wide range values (e.g., Mass: 0.1 - 100000 kg)

Example: Mass (0.1-100000 kg)
  [=|=======] 1500

  Left side: fine control for small values (0.1-10)
  Right side: coarse control for large values (1000-100000)
```

#### 3. Vector Slider
```
Use: float3, quaternion, etc.

Example: Position
  X: [====|====] 150.2
  Y: [==|======] 300.5
  Z: [========|] 0.0

  Or: Unified XYZ slider (joystick-style)
```

#### 4. Comparative Slider
```
Use: Show original vs modified value

Example: MaxSpeed (original: 500 m/s)
  [====|====] 800 (+300)
       ↑ original marker

  Shows delta from baseline
  Reset button to restore original
```

### Live Feedback Indicators

```
Component Modified Indicator:
  ▼ MovementModelSpec (data) [MODIFIED] [RESET]
    MaxSpeed: [====|====] 800 m/s (original: 500)
                              ↑ value changed, shown in different color

Entity Modified Indicator:
  [Entity Inspector: Fleet_Alpha (ID: 12345)] [*MODIFIED*]

  Asterisk indicates unsaved changes
  Prompt on close: "Save preset before closing?"

Performance Warning:
  [!] Warning: Frame time increased to 25ms (target: 16.6ms)
  [!] Allocation detected: 1.2 MB (should be zero)

  Visual feedback when tweaks cause performance issues
```

### Search & Filter

```
Entity Search:
  [Search: "fleet max>500"] [Go]

  Query language:
    "fleet" → contains "fleet" in name
    "type:Scout" → archetype contains Scout component
    "max>500" → MaxSpeed > 500
    "hp<50%" → CurrentHP < 50% of MaxHP

  Results:
    Fleet_Alpha (ID: 12345) - MaxSpeed: 850
    Fleet_Bravo (ID: 12346) - MaxSpeed: 600
    ...

Component Filter:
  [Show Components: ☑ Data ☑ Tags ☐ Buffers]
  [Sort: ☑ Alphabetical ☐ Size ☐ Modified]

  Reduce clutter, show only relevant components
```

---

## Integration Points

### ScenarioRunner CLI

```
Extend ScenarioRunner with sandbox commands:

sandbox.select <entityId>
  → Open inspector for entity

sandbox.set <entityId> <component> <field> <value>
  → Modify component field
  Example: sandbox.set 12345 MovementModelSpec MaxSpeed 1000

sandbox.preset.save <name>
  → Save current entity as preset

sandbox.preset.load <name>
  → Load preset onto selected entity

sandbox.stress <template>
  → Apply stress test template
  Example: sandbox.stress extreme_speed

sandbox.snapshot <name>
  → Save full world state

sandbox.restore <name>
  → Load world state snapshot

sandbox.time <scale>
  → Set time scale
  Example: sandbox.time 5.0 (fast forward)

sandbox.physics.gravity <x> <y> <z>
  → Override gravity

sandbox.export <entityId> <path>
  → Export entity as JSON/ScriptableObject
```

### Performance Monitor Integration

```
While tweaking, show live metrics:

[Performance Monitor]
  Frame Time: 12.5 ms (target: 16.6 ms) ✓
  Allocations: 0 bytes (target: 0) ✓
  Entity Count: 1523
  Active Jobs: 8

  System Breakdown:
    PatrolBehaviorSystem: 2.1 ms
    MovementSystem: 3.4 ms
    ShadowCastingSystem: 1.8 ms
    ...

  Warning Triggers:
    - Frame time > 16.6 ms → Red indicator
    - Allocations > 0 → Yellow indicator
    - Entity count > 10000 → Performance warning
```

### Replay Integration

```
Sandbox + Replay = Powerful Debugging

Workflow:
  1. Encounter bug in replay
  2. Pause replay at problematic tick
  3. Enter sandbox mode
  4. Inspect entities, modify values
  5. Resume replay, see if fix works
  6. Export fix as preset/patch

Implementation:
  - Sandbox mode can read replay state (read-only)
  - Modifications create "override timeline"
  - Can export overrides as patch to apply in production
```

---

## Serialization Architecture

### Component Serialization

```
Approach 1: JSON (Human-Readable)

Pros:
  - Easy to read/edit manually
  - Version control friendly (diffs)
  - Debug-friendly

Cons:
  - Slow to parse
  - Large file size
  - Precision loss on floats

Use: Presets, scenarios, export to tools

Example:
  {
    "type": "MovementModelSpec",
    "data": {
      "MaxSpeed": 500.0,
      "Acceleration": 50.0,
      "TurnRate": 90.0,
      "Mass": 1500.0
    }
  }
```

```
Approach 2: Binary (Compact & Fast)

Pros:
  - Very fast to parse
  - Small file size
  - Bit-exact (no precision loss)

Cons:
  - Not human-readable
  - Hard to version control
  - Requires schema for interpretation

Use: Runtime snapshots, network sync, save files

Format:
  [ComponentType hash: 4 bytes]
  [Component size: 4 bytes]
  [Component data: N bytes (raw struct)]
```

```
Approach 3: Hybrid (Best of Both)

Strategy:
  - Dev builds: JSON (easy debugging)
  - Production builds: Binary (performance)
  - Tool: Convert JSON ↔ Binary

Serializer Interface:
  interface IComponentSerializer<T>
  {
      void SerializeJson(T component, JsonWriter writer);
      T DeserializeJson(JsonReader reader);
      void SerializeBinary(T component, BinaryWriter writer);
      T DeserializeBinary(BinaryReader reader);
  }

Auto-generated for all components via source generator
```

### Entity Graph Serialization

```
Challenge: Entities reference other entities (Entity fields)

Example:
  Fleet → references → Flagship entity
  Patrol → references → AmbushSlot entity

Naive Approach (Fails):
  Serialize Fleet.FlagshipEntity as raw Entity value
  → Entity.Index is only valid in current world
  → Deserialization creates dangling reference

Correct Approach: Entity ID Mapping

1. Assign stable IDs to entities (e.g., GUID or sequential)
2. Serialize: Entity → StableID
3. Deserialize: StableID → Entity (via lookup table)

Implementation:
  struct StableEntityId : IComponentData
  {
      public FixedString64Bytes Id; // GUID or "Fleet_Alpha_001"
  }

  During serialization:
    Entity reference → Look up StableEntityId → Serialize ID string

  During deserialization:
    ID string → Look up entity by StableEntityId → Restore Entity reference
```

---

## Safety & Validation

### Value Clamping

```
Concept: Prevent invalid values that crash simulation

Examples:
  - MaxSpeed: Clamp to [0, 10000] (negative speed = undefined)
  - Mass: Clamp to [0.1, 100000] (zero mass = divide-by-zero)
  - HP: Clamp to [0, MaxHP] (negative HP = undefined)

Implementation:
  - Slider UI enforces range
  - Text input validates and clamps
  - SetField() method validates before applying

Warning on Clamp:
  User types "MaxSpeed = -500"
  → System clamps to 0
  → Warning: "Value -500 clamped to valid range [0, 10000]"
```

### Dependency Validation

```
Concept: Some field changes require updating dependent fields

Example 1: HP and MaxHP
  - User sets CurrentHP = 1500
  - But MaxHP = 1000
  - Invalid state (HP > MaxHP)

  Solution:
    Detect: CurrentHP > MaxHP
    Prompt: "CurrentHP exceeds MaxHP. Increase MaxHP to 1500?"
    Auto-fix: Set MaxHP = CurrentHP

Example 2: Mass and Size
  - User sets Mass = 10 kg (very light)
  - But Size = 100 m (very large)
  - Suspicious: Density = 0.001 kg/m³ (less than air)

  Solution:
    Warning: "Mass/Size ratio is unusually low. Intended?"
    Suggest: "Typical density for this type: 1000 kg/m³"
```

### Undo/Redo Stack

```
Concept: Allow reverting changes during experimentation

Operations:
  - Every field change pushed to undo stack
  - Ctrl+Z: Undo last change
  - Ctrl+Y: Redo last undone change
  - Max stack depth: 50 operations

Undo Entry:
  {
    EntityId: 12345,
    ComponentType: MovementModelSpec,
    FieldName: "MaxSpeed",
    OldValue: 500.0,
    NewValue: 800.0,
    Timestamp: tick
  }

Implementation:
  - Circular buffer (no allocations)
  - Burst-friendly (value types only)
  - Serialize undo stack with presets (restore full history)
```

---

## Workflow Examples

### Example 1: Balancing Unit Speed

```
Goal: Find perfect MaxSpeed for Scout unit

Steps:
  1. Select Scout entity in game (click or search)
  2. Inspector shows MovementModelSpec component
  3. Adjust MaxSpeed slider: 500 → 600 → 700 → 650
  4. Observe in real-time: unit moves faster
  5. Too fast? Undo (Ctrl+Z) back to 600
  6. Perfect! Save preset: "Scout_Balanced_v5"
  7. Apply preset to all other scouts: Batch Apply
  8. Export preset to ScriptableObject: scout_stats.asset
  9. Commit to version control

Result: Balanced scout speed in <5 minutes, no code changes
```

### Example 2: Stress Testing Pathfinding

```
Goal: Test pathfinding with 1000 units

Steps:
  1. Load scenario: "Empty_5000x5000_map"
  2. Apply stress test: "1000-Unit Swarm"
  3. System spawns 1000 scouts at origin
  4. Set time scale: 5.0 (fast forward)
  5. Set all scouts' target: random positions
  6. Observe:
      - Frame time: 14.2 ms (good!)
      - Collision count: 2500/frame (expected)
      - Allocations: 0 bytes (perfect)
  7. Tweak: Increase MaxSpeed 10x (stress collision)
  8. Observe:
      - Frame time: 22.8 ms (over budget!)
      - Collision misses: 15 (bug detected!)
  9. Save scenario: "PathfindingStressTest_HighSpeed"
  10. File bug: "Collision detection fails at >5000 m/s"

Result: Found edge case in <10 minutes, reproducible scenario saved
```

### Example 3: Creating Giant Boss

```
Goal: Make a giant boss entity for testing

Steps:
  1. Select enemy entity
  2. Adjust sliders:
      - Scale: 1.0 → 50.0 (giant!)
      - Mass: 1500 kg → 100000 kg (very heavy)
      - HP: 1000 → 50000 (tanky)
      - MaxSpeed: 500 → 100 (slow but unstoppable)
  3. Add component: "BossTag" (for special AI)
  4. Test: Player attacks, boss barely takes damage (good!)
  5. Save preset: "GiantBoss_v1"
  6. Clone entity: Create 3 more bosses
  7. Export preset to Godgame authoring: boss_template.asset

Result: Boss prototype in <2 minutes, ready for content team
```

---

## Technical Considerations

### Thread Safety

```
Challenge: ECS systems run on job threads, but UI runs on main thread

Solution: Command Buffer for Tweaks

  1. UI thread: User adjusts slider
  2. UI queues command: SetComponentField(entity, component, field, value)
  3. Main thread system: Process command queue
  4. Apply changes via EntityManager (main thread only)
  5. Changes take effect next frame

NO direct mutation from UI thread (race conditions!)
```

### Performance Impact

```
Overhead of Sandbox Mode:

Negligible when inactive:
  - Registry exists, but not queried
  - No per-frame cost

Moderate when inspecting:
  - Entity lookup: O(1) (Entity.Index)
  - Component read: O(1) (direct memory access)
  - UI rendering: ~0.5ms for full inspector

Heavy when modifying:
  - Component write: O(1) but may invalidate caches
  - Archetype change: O(n) if add/remove component (expensive!)
  - Blob reload: O(entities using blob) (can be hundreds)

Recommendation:
  - Sandbox mode = separate build config
  - Strip registry from production builds (save memory)
  - Or: Keep registry but disable UI (for remote debugging)
```

### Memory Management

```
Serialization Buffers:

Problem:
  - Serializing 1000 entities → 1 MB+ JSON
  - Don't allocate every frame!

Solution:
  - Persistent NativeList<byte> buffer (reused)
  - Grow as needed, never shrink (amortize allocations)
  - Clear and reuse for next serialization

Undo Stack:

Problem:
  - 50 undo entries × 100 bytes = 5 KB per entity
  - 1000 entities = 5 MB

Solution:
  - Only track undo for selected entity
  - Discard undo stack on entity deselect
  - Or: Limit undo stack to last 10 operations (smaller)
```

---

## Future Extensions

### Remote Debugging

```
Concept: Connect to running game from external tool

Use Case:
  - Game running on console/phone
  - Developer on PC wants to inspect
  - Connect via network, stream entity data

Architecture:
  - Game: Hosts "SandboxServer" (JSON API over WebSocket)
  - Tool: Web dashboard or Unity Editor plugin
  - Protocol: JSON commands (get entity, set field, etc.)

Security:
  - Only enabled in dev builds
  - Require authentication token
  - Rate limit to prevent spam
```

### Collaborative Tweaking

```
Concept: Multiple developers tweak same simulation

Use Case:
  - Designer adjusts unit stats
  - Programmer tweaks physics
  - Both see changes in real-time

Implementation:
  - Central "tweaking session" (shared state)
  - Each user's changes broadcasted to others
  - Conflict resolution: Last write wins (or prompt)

Workflow:
  1. Designer joins session: "BalancingSession_2024_12_01"
  2. Programmer joins same session
  3. Designer tweaks Scout.MaxSpeed → all clients update
  4. Programmer tweaks Gravity → all clients update
  5. Save session: snapshot with all changes
```

### AI-Assisted Tweaking

```
Concept: AI suggests optimal values based on goals

Use Case:
  - User: "I want scouts to feel fast but not overpowered"
  - AI: Analyzes current values, suggests:
      MaxSpeed: 650 m/s (was 500)
      Acceleration: 80 m/s² (was 50)
      HP: 400 (was 500) (glass cannon)
  - User: Apply suggestions, iterate

Implementation:
  - ML model trained on "good" unit stats
  - Input: Current values + design goals (text)
  - Output: Suggested values + explanation
```

---

## Summary

**Core Idea**: Make everything tweakable at runtime through serialization + reflection + UI.

**Key Components**:
1. **ComponentTypeRegistry**: Metadata for all components (fields, ranges, types)
2. **Entity Inspector**: Select entity, view/edit all components
3. **Preset System**: Save/load entity configurations
4. **Stress Test Templates**: Predefined extreme scenarios
5. **Global Tweaks**: Time scale, physics, world settings
6. **CLI Integration**: ScenarioRunner commands for automation
7. **Serialization**: JSON (readable) + Binary (fast) + Hybrid

**Benefits**:
- ✅ Rapid iteration (no rebuilds)
- ✅ Stress testing (extreme values)
- ✅ Balancing (instant feedback)
- ✅ Debugging (live inspection)
- ✅ Content creation (export to authoring)
- ✅ Collaboration (share presets/scenarios)

**Implementation Notes**:
- Use source generators for registry
- Command buffer for thread-safe tweaking
- Stable entity IDs for serialization
- Undo/redo stack for experimentation
- Performance monitoring while tweaking

This is the ultimate developer playground for PureDOTS!

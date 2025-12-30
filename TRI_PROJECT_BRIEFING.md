# Unity Tri-Project Briefing

**Last Updated**: 2025-12-30  
**Purpose**: Master orientation for agents working across PureDOTS, Space4X, and Godgame

---

## Project Overview

This workspace contains three interconnected Unity DOTS projects:

| Project | Path | Purpose |
|---------|------|---------|
| **PureDOTS** | `C:\dev\Tri\puredots` (Windows) or `/home/oni/Tri/puredots` (WSL ext4) | Shared DOTS framework package |
| **Space4X** | `C:\dev\Tri\space4x` (Windows) or `/home/oni/Tri/space4x` (WSL ext4) | Carrier-first 4X strategy game |
| **Godgame** | `C:\dev\Tri\godgame` (Windows) or `/home/oni/Tri/godgame` (WSL ext4) | Divine intervention god-game simulation

**WSL path policy**: Use `/home/oni/Tri` for WSL work (ext4). Avoid `/mnt/c` for active work due to drvfs I/O errors. When running dual clones (Windows + WSL), keep ownership boundaries: Windows edits `Assets/` + `.meta`; WSL edits PureDOTS/logic; do not share `Library` across OSes. Keep `Packages/manifest.json` and `Packages/packages-lock.json` synced across clones when logic changes to avoid slice-only compile errors.

### Architecture Pattern

```
┌─────────────────────────────────────────────────────────────────┐
│                         GAME PROJECTS                           │
│  ┌─────────────────────┐       ┌─────────────────────┐         │
│  │      Space4X        │       │      Godgame        │         │
│  │  (Carrier 4X Game)  │       │   (God-Game Sim)    │         │
│  │                     │       │                     │         │
│  │  - Mining/Hauling   │       │  - Villager AI      │         │
│  │  - Fleet Combat     │       │  - Village/Band     │         │
│  │  - Module System    │       │  - Miracles         │         │
│  │  - Tech Diffusion   │       │  - Biomes/Weather   │         │
│  └──────────┬──────────┘       └──────────┬──────────┘         │
│             │                             │                     │
│             └──────────────┬──────────────┘                     │
│                            │                                    │
│                            ▼                                    │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                       PureDOTS                           │   │
│  │              (Shared DOTS Framework Package)             │   │
│  │                                                          │   │
│  │  - Time/Rewind Spine    - Registry Infrastructure       │   │
│  │  - Spatial Partitioning - Telemetry & Debug             │   │
│  │  - Authoring Tools      - Scenario Runner               │   │
│  │  - Villager Systems     - Resource Systems              │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Development Philosophy

The TRI project operates on three parallel tracks with distinct responsibilities:

### Headless Track
- **Owns**: Proofs, exit codes, telemetry contracts, smoke test maintenance
- **Responsibility**: Keeps smoke tests green and ensures deterministic simulation correctness
- **Gating**: Headless track failures block integration milestones

### Presentation Track
- **Owns**: Look/feel, asset import, visual presentation, camera/HUD systems
- **Responsibility**: Delivers polished user experience without affecting simulation correctness
- **Gating**: Does not gate headless track; presentation work can proceed independently

### Implementation Track
- **Owns**: Gameplay mechanics, system evolution, feature development
- **Responsibility**: Builds new capabilities and refines existing systems
- **Gating**: Must maintain headless proofs; changes affecting proofs require expectation updates

### Integration Milestones
- Integration happens at convenient milestones, not continuously
- Rebuild servers at integration points to validate cross-track compatibility
- Any change affecting headless proofs requires:
  - Updating test expectations, OR
  - Disabling per-scenario tests with documented rationale

### Change Impact Rules
- **Headless-breaking changes**: Must update expectations or disable scenarios before merge
- **Presentation-only changes**: Can proceed without headless approval
- **Implementation changes**: Must maintain or update headless proofs

---

## PureDOTS Framework

**Location**: `C:\dev\Tri\puredots` (Windows) or `/home/oni/Tri/puredots` (WSL)  
**Package**: `Packages/com.moni.puredots`

### What It Provides

| Domain | Components/Systems |
|--------|-------------------|
| **Time/Rewind** | `TimeState`, `RewindState`, `TickTimeState`, `TimeControlCommand` |
| **Registries** | Villager, Storehouse, Resource, Band, Miracle, Logistics, Construction |
| **Spatial** | `SpatialGridConfig`, `SpatialGridState`, `SpatialGridResidency` |
| **Telemetry** | `TelemetryStream`, `TelemetryMetric`, debug display |
| **AI Pipeline** | Sensors, utility scoring, steering, task resolution |
| **Scenario** | ScenarioRunner, JSON scenarios, headless CLI |

### Design Pillars

1. **Deterministic Core** - Fixed-step, rewind-safe simulation
2. **DOTS-Only** - No MonoBehaviour service locators in simulation
3. **Game Agnostic** - No game-specific types in PureDOTS
4. **Burst/IL2CPP Safe** - All hot paths Burst-compiled
5. **Scalable** - Target 50k-100k complex entities

**Patterns (group/individual):** Pattern evaluation lives in `Packages/com.moni.puredots/Runtime/Systems/Patterns/PatternSystem.cs`, writing `GroupPatternModifiers` + `ActivePatternTag`. Pattern IDs are enums (`Runtime/Patterns/PatternComponents.cs`) to keep Burst happy—avoid FixedString construction in static contexts. Demo patterns: HardworkingVillage, ChaoticBand, OverstressedGroup.

### ECS Architecture: Body/Mind/Aggregate Worlds

**Canonical Truth**: We only use one ECS: Unity.Entities 1.4.

All "Mind ECS", "Body ECS", and "Aggregate ECS" in TRI are architectural slices we define inside our own codebase, not separate packages.

They are implemented as distinct Worlds + SystemGroups running on top of the same com.unity.entities runtime:

- **Body ECS** – canonical deterministic sim world (time/rewind, spatial grid, registries, core sim loops).
- **Mind ECS** – thinking/planning/what-if world (goals, planning, evaluation, AgentSyncBus, etc.).
- **Aggregate ECS** – higher-level aggregates: bands, fleets, empires, dynasties, regional summaries, etc.

All three live inside our local framework com.moni.puredots and the game projects that reference it (Godgame, Space4X).

**There is nothing to install from Package Manager called "Mind ECS", "Body ECS", or "Aggregate ECS".**

If you are looking for them, you are looking for namespaces, bootstraps, and system groups in com.moni.puredots, not for extra packages.

### Key Documentation

- `README_BRIEFING.md` - Project briefing
- `Docs/Vision.md` - Design pillars and roadmap
- `Docs/PUREDOTS_INTEGRATION_SPEC.md` - Integration patterns
- `Docs/Architecture/ThreePillarECS_Architecture.md` - Three Pillar ECS overview
- `Docs/Architecture/AgentSyncBus_Specification.md` - AgentSyncBus API and cadence
- `Docs/Guides/MultiECS_Integration_Guide.md` - Multi-ECS integration cookbook
- `Docs/FoundationGuidelines.md` - Coding guidelines

## Burst Rulebook (PureDOTS + Space4X + Godgame)

**Always Burst (pure, no logs/strings/allocs):** movement/steering/pathing/orbits; per-tick AI math (targeting, mining internals, combat resolution); needs/resource deltas/regen/decay/facility processing; aggregation jobs (morale/power/workforce/fleet stats); PureDOTS spines (time/tick/rewind, spatial/proximity, logistics math, identity/focus/morale numeric updates); render mapping systems (catalog → MaterialMeshInfo/RenderBounds) via shared PureDOTS.Resolve/Apply presenters.

**Never Burst:** UI/camera/input/presentation; ECS↔Mono bridges touching GameObject/Transform/Canvas; high-level orchestration/meta (WorldSnapshot orchestration, narrative bridges, diplomacy controllers, mission board, captain escalation); IntergroupRelations cluster (OrgIntegration/OrgOwnership/OrgRelationInit/OrgRelationEventImpact/OrgPolicyCompute, etc.) until a dedicated Burst pass; editor/tools/menus/gizmos/scene wizards.

**Split (Burst core + [BurstDiscard] logs):** runtime debug counters/sanity checks (mining debug, render sanity, time/rewind debug, registry sanity); render sanity systems. Pattern:

```csharp
[BurstCompile]
public partial struct SomeDebugSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        int count = 0;
        foreach (var _ in SystemAPI.Query<RefRO<SomeTag>>()) count++;
#if UNITY_EDITOR
        LogCount(count);
#endif
    }

    [BurstDiscard]
    static void LogCount(int count) => UnityEngine.Debug.Log($"[SomeDebug] count:{count}");
}
```

**Banned in Burst paths:** `new ComponentType[] { ... }`, `new EntityQueryDesc[] { ... }`, `new[] { ComponentType... }`, `new[] { EntityQueryDesc... }`; Debug.Log* or any string concat/interpolation; List/Dictionary/allocs; FixedString constructors or enum .ToString(); managed references. All diagnostics go through [BurstDiscard] helpers. Allowed Burst-safe query patterns: `state.GetEntityQuery(ComponentType.ReadOnly<A>(), ComponentType.Exclude<B>());`; `foreach (var _ in SystemAPI.Query<RefRO<TagX>>()) { … }`; `EntityQueryBuilder` / `SystemAPI.QueryBuilder().WithAll<...>().Build();`. If a system truly needs `EntityQueryDesc`, build it in non-Burst code or keep that system non-Burst.

**Special handling:** IntergroupRelations stays non-Burst/stub until rework; WorldSnapshotPlaybackSystem may stay non-Burst or stubbed (empty OnUpdate) until a small Burst core exists; render catalog systems stay Burst but move all diagnostics to [BurstDiscard] helpers; Space4X_MiningEntityDebugSystem and similar follow the Split pattern.

---

## Space4X Game

**Location**: `C:\dev\Tri\space4x` (Windows) or `/home/oni/Tri/space4x` (WSL)

### Game Vision

Carrier-first 4X strategy where players command carrier task forces across a living galaxy. Core loops: mining, hauling, exploration, and combat.

### Core Mechanics

| System | Status | Description |
|--------|--------|-------------|
| Mining Loop | ✅ Complete | Carrier mining execution, yield spawn, resource pickup |
| Module System | ✅ Complete | Modular carrier customization, refit/repair/degradation |
| Alignment/Compliance | ✅ Complete | Crew alignment, mutiny/desertion detection |
| Tech Diffusion | ✅ Complete | Research spreads across empire |
| Fleet Combat | 🚧 Partial | Fleet interception, needs full combat resolution |
| Registry Bridge | ✅ Complete | Colonies, fleets, logistics routes, anomalies |

### Integration Pattern

```csharp
// Space4X consumes PureDOTS via package reference
// Packages/manifest.json:
{
  "dependencies": {
    "com.moni.puredots": "file:../../puredots/Packages/com.moni.puredots"
  }
}
```

### Key Documentation

- `Docs/ORIENTATION.md` - Project orientation
- `Docs/INDEX.md` - Documentation index
- `space.plan.md` - Demo readiness plan

---

## Godgame

**Location**: `C:\dev\Tri\godgame` (Windows) or `/home/oni/Tri/godgame` (WSL)

### Game Vision

God-game simulation where players influence villagers, villages, and bands through divine intervention. Focus on emergent behavior and aggregate dynamics.

### Core Mechanics

| System | Status | Description |
|--------|--------|-------------|
| Registry Bridge | ✅ Complete | Villager, Storehouse, Band, Miracle, Spawner, Logistics |
| Time Controls | ✅ Complete | Pause/speed/step/rewind with determinism |
| Construction | ✅ Complete | Ghost → payment → completion flow |
| Resource Systems | ✅ Complete | Storehouse API, aggregate piles, overflow |
| Weather/Biomes | ✅ Complete | Climate integration, presentation |
| Miracles | ✅ Complete | Input/release with registry sync |
| Villager AI | 🚧 Partial | Basic loop, needs interrupts/needs/GOAP |
| Aggregate AI | 🚧 Partial | Village/band components, needs decision-making |

### Integration Pattern

```csharp
// Godgame consumes PureDOTS via package reference
// Packages/manifest.json:
{
  "dependencies": {
    "com.moni.puredots": "file:../../puredots/Packages/com.moni.puredots"
  }
}
```

### Key Documentation

- `Docs/Project_Orientation.md` - Project orientation
- `Docs/INDEX.md` - Documentation index
- `god.plan.md` - Development plan

---

## Critical DOTS Coding Patterns

**MANDATORY for ALL projects. Violations cause compile errors that block parallel work.**

### P0: Verify Dependencies Before Writing Code

Before writing any system that uses a type:
```bash
# Verify type exists
grep -r "struct TimeState" --include="*.cs"
grep -r "struct RewindState" --include="*.cs"
```
**If not found: CREATE IT FIRST or flag as blocker.**

### P1: Buffer Mutation - Use Indexed Access

```csharp
// ❌ WRONG (CS1654/CS1657) - NEVER mutate foreach iteration variables
foreach (var item in buffer) { item.Value = 5; }

// ✅ CORRECT
for (int i = 0; i < buffer.Length; i++)
{
    var item = buffer[i];
    item.Value = 5;
    buffer[i] = item;
}
```

### P1: Blob Access - Always Use Ref

```csharp
// ❌ WRONG (EA0001/EA0009) - Copies blob data
var catalog = blobRef.Value;

// ✅ CORRECT - References blob directly
ref var catalog = ref blobRef.Value;

// ❌ WRONG - Blob types in out parameters
bool TryFind(out ProjectileSpec spec) { ... }

// ✅ CORRECT - Use ref/in for blob types
bool TryFind(ref ProjectileSpec spec) { ... }
float GetDamage(in ProjectileSpec spec) { ... }
```

### P2: Type Conversion - Explicit Casts

```csharp
// Component stores byte, code uses enum
// ❌ WRONG (CS0266)
component.ModeRaw = AvoidanceMode.Flee;

// ✅ CORRECT
component.ModeRaw = (byte)AvoidanceMode.Flee;
var mode = (AvoidanceMode)component.ModeRaw;
```

### P2: Rewind Guard - Check Before Mutation

```csharp
public void OnUpdate(ref SystemState state)
{
    var rewind = SystemAPI.GetSingleton<RewindState>();
    if (rewind.Mode != RewindMode.Record) return;
    // ... safe to mutate ...
}
```

### P3: C# Version - Unity Uses C# 9, NOT C# 12

```csharp
// ❌ WRONG (CS1031) - 'ref readonly' is C# 12
ref readonly var spec = ref FindSpec(...);

// ✅ CORRECT - C# 9 syntax
ref var spec = ref FindSpec(...);           // Local variables
void Process(in ProjectileSpec spec) { }    // Read-only parameters
```

### P4: Blob Parameters - Must Use `ref`, NOT `in`

```csharp
// ❌ WRONG (EA0009) - Unity Entities requires 'ref' for blob types
void Process(in ProjectileSpec spec) { }

// ✅ CORRECT
void Process(ref ProjectileSpec spec) { }
```

### P5: Buffer Elements - Must Implement IBufferElementData

```csharp
// ❌ WRONG (CS0411) - IComponentData can't go in buffers
public struct MyState : IComponentData { }
DynamicBuffer<MyState> buffer;  // Fails!

// ✅ CORRECT
public struct MyStateElement : IBufferElementData { }
DynamicBuffer<MyStateElement> buffer;  // Works!
```

### P6: Authoring Classes - Must Inherit MonoBehaviour

```csharp
// ❌ WRONG (CS0311)
public class MyAuthoring { }
public class MyBaker : Baker<MyAuthoring> { }  // Fails!

// ✅ CORRECT
public class MyAuthoring : MonoBehaviour { }
public class MyBaker : Baker<MyAuthoring> { }  // Works!
```

### P7: Burst Parameters - Use `in` for Structs

```csharp
// ❌ WRONG (BC1064) - Structs by value fail in Burst external calls
void Helper(Entity e, EntityCommandBuffer.ParallelWriter ecb) { }

// ✅ CORRECT
void Helper(in Entity e, in EntityCommandBuffer.ParallelWriter ecb) { }
```

### P8: No Managed Code in Burst

```csharp
// ❌ WRONG (BC1016) - All managed operations fail in Burst:
new FixedString64Bytes("literal");    // Managed constructor!
someString.ToString();                 // Managed method!

// ✅ CORRECT - Pre-define constants outside Burst:
private static readonly FixedString64Bytes MyName = "name";

// Use in Burst:
var name = MyName;  // Just reference the constant
```

### P9: Required Using Directives

```csharp
// For math.*, half, float2, float3:
using Unity.Mathematics;

// For UnsafeUtility pointer helpers:
using Unity.Collections.LowLevel.Unsafe;

// For UnsafeRef / RefPtr (Burst-safe null-ref helpers):
using PureDOTS.Runtime.LowLevel;
```

**Never** reference `System.Runtime.CompilerServices.Unsafe.IsNullRef` / `Unsafe.NullRef` in Burst paths.
Use `UnsafeRef.IsNull` / `UnsafeRef.Null` (PureDOTS.Runtime.LowLevel) instead so Burst compiles cleanly.

### P10: Stale Type/Namespace References

```csharp
// ❌ WRONG (CS0234/CS0246) - Referencing moved/deleted types
using Godgame.Presentation;  // Namespace doesn't exist!
var tools = FindObjectOfType<DevTools>();  // Type was removed!

// ✅ CORRECT - Verify types exist before referencing
// Step 1: grep -r "namespace Godgame.Presentation" --include="*.cs"
// Step 2: If not found, remove reference or find correct namespace
// Step 3: Update using statements to match actual locations
```

**Common causes:**
- Type was moved to different namespace
- Type was deleted during refactor
- Assembly reference missing in `.asmdef`

### P11: Obsolete Unity Object Find Methods

```csharp
// ❌ WRONG (CS0618) - Deprecated in Unity 2023+
var obj = FindObjectOfType<MyComponent>();
var objs = FindObjectsOfType<MyComponent>();
var objs2 = FindObjectsOfType<MyComponent>(true);

// ✅ CORRECT - Use new typed alternatives
var obj = FindFirstObjectByType<MyComponent>();       // Finds first match
var obj2 = FindAnyObjectByType<MyComponent>();        // Faster, any match
var objs = FindObjectsByType<MyComponent>(FindObjectsSortMode.None);         // Unsorted (faster)
var objs2 = FindObjectsByType<MyComponent>(FindObjectsSortMode.InstanceID);  // Legacy sort order
var objs3 = FindObjectsByType<MyComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);  // Include inactive
```

### P12: CreateAssetMenu on Non-ScriptableObject

```csharp
// ❌ WRONG - Attribute ignored, clutters console with warnings
[CreateAssetMenu(menuName = "Game/My Catalog")]
public class MyCatalogAuthoring : MonoBehaviour { }  // MonoBehaviour, not ScriptableObject!

// ✅ CORRECT Option A - Make it a ScriptableObject if it's data-only
[CreateAssetMenu(menuName = "Game/My Catalog")]
public class MyCatalog : ScriptableObject { }

// ✅ CORRECT Option B - Remove attribute if it must stay MonoBehaviour
public class MyCatalogAuthoring : MonoBehaviour { }  // No attribute
```

### P13: FixedString Constructor Inside Burst-Compiled Code

```csharp
// ❌ WRONG (BC1016) - String constructor is managed, fails in Burst
[BurstCompile]
public void OnUpdate(ref SystemState state)
{
    // These all fail - string operations are managed!
    var name = new FixedString64Bytes("Hello");
    var desc = new FixedString128Bytes(enumValue.ToString());
    FixedString64Bytes result = "literal";  // Implicit conversion also fails!
}

// ✅ CORRECT - Pre-define ALL string constants as static readonly OUTSIDE the system
private static readonly FixedString64Bytes HelloString = "Hello";
private static readonly FixedString64Bytes GoalIdle = "Idle";
private static readonly FixedString64Bytes GoalWork = "Work";
private static readonly FixedString64Bytes GoalRest = "Rest";

[BurstCompile]
public void OnUpdate(ref SystemState state)
{
    var name = HelloString;  // Just copy the pre-made constant
    
    // For enum-to-string, use a switch with pre-defined constants:
    var desc = goal switch
    {
        Goal.Idle => GoalIdle,
        Goal.Work => GoalWork,
        Goal.Rest => GoalRest,
        _ => default
    };
}
```

### P14: SystemAPI Scope - No Static Helpers

```csharp
// ❌ WRONG (EA0004/EA0006) - SystemAPI used in static helper or static method
public static void AddMember(...) { SystemAPI.TryGetSingleton<TimeState>(); }

// ✅ CORRECT - keep SystemAPI in system instance methods
public void OnUpdate(ref SystemState state)
{
    var time = SystemAPI.GetSingleton<TimeState>();
}

// ✅ Option B - in helpers, use EntityQuery via SystemState
var query = state.GetEntityQuery(ComponentType.ReadOnly<TimeState>());
if (!query.TryGetSingleton(out TimeState timeState)) return;
```

### P15: Burst Function Pointers Need Blittable Bools

```csharp
// ❌ WRONG (BC1063) - bool fields passed by ref are not blittable in calli
public struct AnchoredSimConfig { public bool AlwaysFullSimulation; }

// ✅ CORRECT - marshal bool as U1 or use byte instead
public struct AnchoredSimConfig { [MarshalAs(UnmanagedType.U1)] public bool AlwaysFullSimulation; }
```

**Known violators (fix required in PureDOTS):**
- `BandFormationSystem.GoalToDescription` (line 255)
- `SpellEffectExecutionSystem.ApplyShieldEffect` (line 311)
- `WhatIfSimulationSystem.OnUpdate` (line 108)
- `LessonAcquisitionSystem.CheckAttributeRequirement` (line 358)

### P16: Presentation Code Uses Frame-Time, Not Tick-Time

```csharp
// ❌ WRONG - Don't drive presentation off deterministic tick
[BurstCompile]
public void OnUpdate(ref SystemState state)
{
    // This would make camera stutter/jerk during rewind/pause
    var cameraPos = SystemAPI.GetSingleton<CameraState>();
    cameraPos.Position += moveVector * SystemAPI.Time.DeltaTime;  // Fixed tick time!
}

// ✅ CORRECT - Presentation uses frame time
public class CameraController : MonoBehaviour
{
    void Update()
    {
        // Smooth camera movement using frame time
        transform.position += moveVector * Time.deltaTime;  // Frame time for smooth movement
    }
}
```

**Rule**: "Presentation" code (camera & HUD) uses frame-time, not tick-time, unless there is a deliberate reason otherwise.

Use `Time.deltaTime` in camera scripts and HUD animations.

Use `TickTimeState` / sim time only for things that must stay aligned with the deterministic tick (e.g., playback scrubbers, sim-time displays).

### P17: Simulation vs Presentation Separation

Deterministic simulation lives in PureDOTS packages. Cameras, HUD, and presentation code live in game projects.

```csharp
// ✅ CORRECT - Simulation stays deterministic
public partial struct MovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Pure simulation logic here
        var time = SystemAPI.GetSingleton<TimeState>();
        // ... deterministic movement ...
    }
}

// ✅ CORRECT - Presentation in game projects
namespace Space4X.Presentation  // Not in PureDOTS!
{
    public class CameraRig : MonoBehaviour
    {
        void Update()
        {
            // Non-deterministic presentation here
            transform.position = Vector3.Lerp(current, target, Time.deltaTime * speed);
        }
    }
}
```

### P18: Shared Component Access - Use EntityManager

SystemAPI has no GetSharedComponent<T>() method. Use EntityManager for shared component access.

```csharp
// ❌ WRONG - SystemAPI.GetSharedComponent doesn't exist
var rma = SystemAPI.GetSharedComponent<RenderMeshArray>(catalogEntity);

// ✅ CORRECT - Use EntityManager.GetSharedComponentManaged
var rma = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(catalogEntity);

// For setting shared components:
state.EntityManager.SetSharedComponentManaged(entity, new RenderMeshArray { ... });
```

---

## Camera Organization & Artifacts Warning

### **CRITICAL: Game Code Belongs in Game Projects**

**🚨 DO NOT implement Game folders in PureDOTS workspace!**

Camera controllers, input bridges, and presentation rigs belong in their respective **game project directories**, not in the PureDOTS framework workspace. The PureDOTS workspace should contain only:

- ✅ Framework infrastructure (CameraRigService, CameraRigApplier, CameraRigState)
- ✅ Shared camera components and systems
- ✅ BW2-style reusable camera rig
- ❌ Game-specific implementations

**📖 OFFICIAL CONTRACT: See `Packages/com.moni.puredots/Runtime/Camera/README.md`**

### **Recognizing Artifacts vs Real Code**

If you find camera controller files in PureDOTS workspace paths like:
- `Assets/Projects/Space4X/Scripts/...`
- `Assets/Scripts/Space4x/Camera/...`

**These are likely development artifacts that should be moved or removed.** Check the actual project directories:
- **Space4X**: `C:\dev\Tri\space4x` (Windows) or `/home/oni/Tri/space4x` (WSL)
- **Godgame**: `C:\dev\Tri\godgame` (Windows) or `/home/oni/Tri/godgame` (WSL)

### **Proper Camera Architecture**

```
PureDOTS Framework (Shared)
├── CameraRigService.cs         ✅ Framework
├── CameraRigApplier.cs         ✅ Framework
├── BW2StyleCameraController.cs ✅ Reusable rig
└── CameraRigState.cs           ✅ Shared types

Space4X Game Project
└── Assets/Scripts/Space4x/
    ├── Camera/
    │   ├── Space4XCameraController.cs    ✅ Game-specific
    │   └── Space4XCameraInputBridge.cs  ✅ Game-specific
    └── Authoring/
        └── Space4XCameraAuthoring.cs     ✅ Game-specific

Godgame Game Project
└── Assets/Scripts/Godgame/
    └── Camera/
        └── GodgameCameraController.cs    ✅ Game-specific
```

### **Before Implementing Cameras**

1. **Verify location**: Are you in the correct project directory?
2. **Check existing**: Does the real game project already have camera code?
3. **Avoid artifacts**: Don't leave development code in PureDOTS workspace

---

## Pre-Commit Checklist

Before completing ANY task:

### Core Patterns
- [ ] **Build passes**: `dotnet build` or Unity domain reload succeeds
- [ ] **Dependencies verified**: All referenced types confirmed via grep
- [ ] **No foreach mutation**: Buffer elements modified via indexed access
- [ ] **Blob access uses ref**: All `blobRef.Value` accessed with `ref`
- [ ] **Explicit casts present**: Enum↔byte conversions have casts
- [ ] **Rewind guards present**: Mutating systems check `RewindState.Mode`
- [ ] **Shared components use EntityManager**: No `SystemAPI.GetSharedComponent`, use `state.EntityManager.GetSharedComponentManaged`

### C# / Unity Compatibility
- [ ] **No `ref readonly`**: Use `ref` for returns, `in` for parameters
- [ ] **Blob params use `ref`**: Not `in` - EA0009 requires `ref`
- [ ] **Buffer elements correct**: Types implement `IBufferElementData`
- [ ] **Authoring inherits MonoBehaviour**: All Baker<T> authoring classes

### Burst Compliance
- [ ] **No managed strings in Burst**: No `new FixedString("literal")` or `.ToString()`
- [ ] **String constants pre-defined**: `static readonly` outside Burst
- [ ] **Struct params use `in`**: `in Entity`, `in EntityCommandBuffer.ParallelWriter`
- [ ] **Imports present**: `Unity.Mathematics`, `Unity.Collections.LowLevel.Unsafe`

### Editor/Authoring Code
- [ ] **No stale type refs**: All referenced types verified via grep
- [ ] **Modern Find APIs**: Use `FindObjectsByType`/`FindFirstObjectByType`
- [ ] **CreateAssetMenu valid**: Attribute only on `ScriptableObject` subclasses

---

## Common Error Quick Reference

| Error | Cause | Fix |
|-------|-------|-----|
| CS1654 | foreach mutation | Use indexed `for` loop |
| EA0009 | Blob not by ref | `ref var x = ref blob.Value` |
| CS0266 | Implicit enum cast | `(byte)enum` or `(MyEnum)byte` |
| CS1031 | `ref readonly` C# 12 | Use `ref` or `in` |
| CS0411 | Bad buffer type | Implement `IBufferElementData` |
| CS0311 | Bad authoring type | Inherit `MonoBehaviour` |
| BC1064 | Struct by value | Use `in` modifier |
| BC1016 | Managed in Burst | Pre-define string constants as `static readonly` |
| EA0004/EA0006 | `SystemAPI` in static method/helper | Keep SystemAPI inside system instance methods or use `EntityQuery` via `SystemState` |
| BC1063 | bool field not blittable in Burst calli | Mark bool with `[MarshalAs(UnmanagedType.U1)]` or use byte |
| CS0103 | Missing `math`/`Unsafe` | Add using directive |
| CS0234 | Namespace not found | Verify namespace exists, check `.asmdef` refs |
| CS0246 | Type not found | Grep for type, update using or remove ref |
| CS0618 | Obsolete API | Use `FindObjectsByType`/`FindFirstObjectByType` |
| CreateAssetMenu warning | Attribute on non-SO | Remove attribute or inherit `ScriptableObject` |
| CS0101 | Duplicate type from lingering stubs | Delete/`#if false` stub files when real types return |
| RewindState duplicate | Multiple bootstrap paths created another singleton | Only RewindBootstrapSystem seeds RewindState from RewindConfig; remove manual AddComponentData |
| CS1061 | 'SystemAPI' does not contain a definition for 'GetSharedComponent' | SystemAPI has no shared component methods | Use `state.EntityManager.GetSharedComponentManaged<T>()` |

**Stub cleanup rule (canonical names):**
- If you add a stub in the canonical namespace, mark it `// STUB: REMOVE when real <Type>` and track it (e.g., `Docs/StubTypes.md`), then delete it as soon as the real implementation lands.
- Prefer minimal canonical files (`Detectable.cs`, `CombatLearningState.cs`, etc.) over mega `*Stubs.cs` files so they can be expanded instead of duplicated.
- Never ship stubs alongside real types; before closing work, `grep -r "Stubs.cs"` in `com.moni.puredots` and remove or `#if false` any leftovers.

---

## Parallel Work Coordination

### When Multiple Agents Work Simultaneously

1. **Shared types first**: Create components, enums, structs before consumer systems
2. **Verify integration**: After parallel merges, run full build
3. **Flag blockers**: If a dependency doesn't exist, report immediately
4. **Don't assume**: Never assume types exist based on design docs

### Documentation Sync Requirement

⚠️ **This briefing document exists in 3 locations and must stay synchronized:**

| Location | Path |
|----------|------|
| Unity Root (Canonical) | `C:\dev\Tri\TRI_PROJECT_BRIEFING.md` (Windows) or `/home/oni/Tri/TRI_PROJECT_BRIEFING.md` (WSL) |
| Space4X | `C:\dev\Tri\space4x\TRI_PROJECT_BRIEFING.md` (Windows) or `/home/oni/Tri/space4x/TRI_PROJECT_BRIEFING.md` (WSL) |
| Godgame | `C:\dev\Tri\godgame\TRI_PROJECT_BRIEFING.md` (Windows) or `/home/oni/Tri/godgame/TRI_PROJECT_BRIEFING.md` (WSL) |

**When updating this document:**
1. Make changes in the Unity root version (canonical source)
2. Copy to the Space4X and Godgame versions

**When discovering new error patterns:**
- Document in `Docs/FoundationGuidelines.md` (PureDOTS)
- Add to this briefing's error table
- Sync all copies

### Error Ownership

Some errors originate in game-specific code and are fixed by game teams:

| Project | Responsible For |
|---------|-----------------|
| **PureDOTS** | Core framework, shared systems, packages |
| **Space4X** | Space game systems, carrier/fleet code |
| **Godgame** | God game systems, villager/miracle code |

When fixing errors:
- **PureDOTS errors** → Fix in `Packages/com.moni.puredots/`
- **Game errors** → Fix in respective game project
- **Cross-project patterns** → Document here, sync copies

### Common Error Categories

| Category | Example | Prevention |
|----------|---------|------------|
| Missing Types | `TimeState not found` | Verify via grep before writing consumers |
| Foreach Mutation | `CS1654: Cannot modify` | Use indexed `for` loop |
| Blob Access | `EA0001: Access by ref` | Use `ref var x = ref blob.Value` |
| Type Mismatch | `CS0266: Cannot convert` | Add explicit casts |
| Stale References | `CS0234/CS0246` | Remove dead refs after refactors |
| Obsolete APIs | `CS0618: Obsolete` | Use modern Unity APIs |
| Burst String Ops | `BC1016: Managed function` | Pre-define string constants |
| Bad Attributes | `CreateAssetMenu ignored` | Only on ScriptableObject subclasses |
| Shared Component API | `SystemAPI.GetSharedComponent not found` | Use EntityManager for shared components |

---

## Development Workflow

### Starting Work

1. Check `Docs/INDEX.md` and the plan file (`space.plan.md` / `god.plan.md`) in the target project
2. Verify PureDOTS types exist for your feature
3. Update TODO with task start

### During Work

1. Follow Burst compliance: `[BurstCompile]`, parallel jobs
2. Use PureDOTS contracts: Don't duplicate shared schemas
3. Add tests for new features
4. Document new systems

### Finishing Work

1. Run full build: Verify no compile errors
2. Run tests: Use CI commands
3. Update TODOs: Mark complete, note blockers
4. Log progress in the target plan file (`space.plan.md` / `god.plan.md`) or team tracker

---

## Operational Scripts

Headless builds and runs use the shared scripts in `tools/` plus the runbook:

- `tools/build_space4x_linux_from_wsl.sh`
- `tools/run_space4x_headless.sh`
- `tools/build_godgame_linux_from_wsl.sh`
- `tools/run_godgame_headless.sh`
- `Docs/Headless/headless_runbook.md`

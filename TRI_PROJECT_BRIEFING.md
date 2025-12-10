# Unity Tri-Project Briefing

**Last Updated**: 2025-11-27  
**Purpose**: Master orientation for agents working across PureDOTS, Space4X, and Godgame

---

## Project Overview

This workspace contains three interconnected Unity DOTS projects:

| Project | Path | Purpose |
|---------|------|---------|
| **PureDOTS** | `C:\Users\Moni\Documents\claudeprojects\unity\PureDOTS` | Shared DOTS framework package |
| **Space4X** | `C:\Users\Moni\Documents\claudeprojects\unity\Space4x` | Carrier-first 4X strategy game |
| **Godgame** | `C:\Users\Moni\Documents\claudeprojects\unity\Godgame` | Divine intervention god-game simulation |

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

## PureDOTS Framework

**Location**: `C:\Users\Moni\Documents\claudeprojects\unity\PureDOTS`  
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

---

## Space4X Game

**Location**: `C:\Users\Moni\Documents\claudeprojects\unity\Space4x`

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
    "com.moni.puredots": "file:../../PureDOTS/Packages/com.moni.puredots"
  }
}
```

### Key Documentation

- `Docs/ORIENTATION.md` - Project orientation
- `Docs/Progress.md` - Current status
- `space.plan.md` - Demo readiness plan

---

## Godgame

**Location**: `C:\Users\Moni\Documents\claudeprojects\unity\Godgame`

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
    "com.moni.puredots": "file:../../PureDOTS/Packages/com.moni.puredots"
  }
}
```

### Key Documentation

- `Docs/Project_Orientation.md` - Project orientation
- `Docs/Progress.md` - Current status
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

// For Unsafe.IsNullRef, Unsafe.NullRef:
using Unity.Collections.LowLevel.Unsafe;
```

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
- **Space4X**: `C:\Users\Moni\Documents\claudeprojects\unity\Space4x`
- **Godgame**: `C:\Users\Moni\Documents\claudeprojects\unity\Godgame`

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

⚠️ **This briefing document exists in 4 locations and must stay synchronized:**

| Location | Path |
|----------|------|
| Unity Root | `C:\Users\Moni\Documents\claudeprojects\unity\TRI_PROJECT_BRIEFING.md` |
| PureDOTS | `C:\Users\Moni\Documents\claudeprojects\unity\PureDOTS\TRI_PROJECT_BRIEFING.md` |
| Space4X | `C:\Users\Moni\Documents\claudeprojects\unity\Space4x\TRI_PROJECT_BRIEFING.md` |
| Godgame | `C:\Users\Moni\Documents\claudeprojects\unity\Godgame\TRI_PROJECT_BRIEFING.md` |

**When updating this document:**
1. Make changes in the PureDOTS version (canonical source)
2. Copy to all other locations:
```bash
cp PureDOTS/TRI_PROJECT_BRIEFING.md ../TRI_PROJECT_BRIEFING.md
cp PureDOTS/TRI_PROJECT_BRIEFING.md ../Space4x/TRI_PROJECT_BRIEFING.md
cp PureDOTS/TRI_PROJECT_BRIEFING.md ../Godgame/TRI_PROJECT_BRIEFING.md
```

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

---

## Development Workflow

### Starting Work

1. Check `Docs/Progress.md` in target project
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
4. Log progress in `Docs/Progress.md`

---

## CLI Commands

### Build Verification
```bash
# Unity batch build
Unity -batchmode -projectPath <path> -quit -buildWindows64Player Build/Game.exe
```

### Test Execution
```bash
# EditMode tests
Unity -batchmode -projectPath <path> -runTests -testPlatform editmode

# Scenario runner
Unity -batchmode -nographics -executeMethod PureDOTS.Runtime.Devtools.ScenarioRunnerEntryPoints.RunScenarioFromArgs --scenario <path>
```

### Dependency Verification
```bash
# Check if type exists
grep -r "struct TypeName" --include="*.cs"

# Find all usages
grep -r "TypeName" --include="*.cs" | grep -v "//"
```

---

## Version Requirements

| Package | Version | Notes |
|---------|---------|-------|
| Unity Entities | **1.4.2** | NOT 1.5+ (version lock) |
| Unity Burst | 1.8.24 | |
| Unity Collections | 2.6.2 | |
| Unity Mathematics | 1.3.2 | |
| Unity Physics | 1.0.16 | |
| Input System | 1.7.0 | NOT legacy UnityEngine.Input |

**IMPORTANT**: Do NOT upgrade Entities to 1.5+ without coordination.

---

## Quick Reference: Project Paths

| Project | Path |
|---------|------|
| PureDOTS | `C:\Users\Moni\Documents\claudeprojects\unity\PureDOTS` |
| Space4X | `C:\Users\Moni\Documents\claudeprojects\unity\Space4x` |
| Godgame | `C:\Users\Moni\Documents\claudeprojects\unity\Godgame` |
| PureDOTS Package | `PureDOTS/Packages/com.moni.puredots` |

---

## Summary

The three projects form a cohesive ecosystem:

- **PureDOTS** provides deterministic DOTS infrastructure (time, registries, spatial, telemetry)
- **Space4X** builds a carrier-first 4X strategy game on this foundation
- **Godgame** builds a god-game simulation on the same foundation

All projects share coding patterns, the PureDOTS package, and architectural principles. Follow the critical DOTS patterns to avoid compile errors that block parallel development.

**Full documentation**: Each project's `Docs/` folder contains detailed orientation and progress tracking.

error fixing:
Error-hunt pipeline for agents

Have your agents follow this exact loop per project (PureDOTS → Space4X → Godgame):

Step A: Get a clean error list

Trigger a full compile (enter play mode or force a domain reload).

Export all compiler errors (not warnings) into a single text file per project:

PureDOTS/Docs/ErrorLog_PureDOTS.txt

Space4x/Docs/ErrorLog_Space4X.txt

Godgame/Docs/ErrorLog_Godgame.txt

Step B: Classify by error pattern, not by file

Use the quick-reference table + P0–P17 sections in the brief to classify every error into buckets:

Foreach mutation / buffer misuse (CS1654/CS1657) → P1

Blob access / ref issues (EA0001/EA0009) → P1, P4

Enum/byte conversion (CS0266) → P2

ref readonly / C#12 features (CS1031) → P3

Buffer element vs component (CS0411) → P5

Baker / authoring inheritance (CS0311) → P6

Burst parameter passing (BC1064 etc.) → P7

Managed string use in Burst (BC1016) → P8, P13

Missing using / namespace (CS0103, CS0234, CS0246) → P9, P10

Obsolete Unity APIs (CS0618) → P11

CreateAssetMenu on non-SO → P12

SystemAPI in static context (EA0004/EA0006) → P14

Bool in blittable struct with function pointer (BC1063) → P15

Then fix errors by bucket, applying the same patch pattern everywhere. That way you never “invent” ad-hoc fixes system by system.

3. Concrete fix patterns (what your agents should actually do)
3.1. Buffers & foreach mutation (P1)

Symptom:
CS1654: Cannot modify members of '...' because it is a 'foreach iteration variable'

Mandatory fix pattern:

// Before
foreach (var elem in buffer)
{
    elem.Value += delta;  // ❌
}

// After
for (int i = 0; i < buffer.Length; i++)
{
    var elem = buffer[i];
    elem.Value += delta;
    buffer[i] = elem;
}


Apply this globally to every DynamicBuffer<T> mutation in systems and jobs.

3.2. Blob refs & Burst rules (P1, P4, P8, P13)

Symptoms:

EA0001/EA0009 around blob access.

BC1016 around FixedStrings / .ToString() in Burst systems.

Fix pattern:

// Always:
ref var specCatalog = ref blobRef.Value;   // ❌ var = blobRef.Value;

// Helper methods:
static bool TryGetSpec(ref SpecBlob spec, int id, out SomeSpec result)
{
    // Use ref spec, never by value
}


For strings and descriptions in Burst systems:

Move all FixedString literals to static readonly fields outside of the Burst-compiled type.

Replace enum .ToString() in Burst with a switch returning pre-made FixedString constants.

static readonly FixedString64Bytes GoalIdle  = "Idle";
static readonly FixedString64Bytes GoalWork  = "Work";

static FixedString64Bytes DescribeGoal(Goal goal)
{
    return goal switch
    {
        Goal.Idle => GoalIdle,
        Goal.Work => GoalWork,
        _ => default
    };
}


Then call DescribeGoal from Burst systems instead of constructing FixedStrings directly.

3.3. Query tags & SGQC001 (Entities source generator)

Symptom:
SGQC001: WithAll<T>() is not supported. WithAll<T>() may only be invoked on types that implement IComponentData, ... or UnityEngine.Object.

Why: You’re calling WithAll<SomeTag>() where SomeTag is not an IComponentData, IBufferElementData, IAspect or UnityEngine.Object.

Fix pattern (no guessing names):

For gameplay tags used in queries, always define them as empty IComponentData tags:

public struct SomeGameplayTag : IComponentData {}


If a type is a pure simulation concept (no fields or only a few scalar fields) and you query it in WithAll/WithNone/WithAny, move it to IComponentData or wrap it in a thin IComponentData tag used only for filtering; keep richer data in a separate component if needed.

Ensure its namespace matches the using in the system; if necessary, adjust using imports rather than duplicating types.

Then rebuild – this usually wipes a whole cluster of SGQC001s.

3.4. Buffer element vs component confusion (P5)

Symptom:
CS0411: The type arguments for method 'DynamicBuffer<T>' cannot be inferred...
or “<Type> does not implement IBufferElementData”.

Fix pattern:

Every type used in DynamicBuffer<T> must implement IBufferElementData.

// Before
public struct PlannedAction
{
    public int Id;
    public float Score;
}

// After
public struct PlannedActionElement : IBufferElementData
{
    public int Id;
    public float Score;
}


Rename the buffer type to *Element to keep things obvious.

Update all buffer usages to use the new type.

3.5. Authoring/Baker errors (P6, P12)

Symptoms:

CS0311: MyBaker : Baker<T> where T is not a MonoBehaviour.

CreateAssetMenu warnings on non-ScriptableObject types.

Fix pattern:

// Authoring:
public class SomethingAuthoring : MonoBehaviour
{
    // Serialized fields only
}

public class SomethingBaker : Baker<SomethingAuthoring>
{
    public override void Bake(SomethingAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new SomethingComponent { ... });
    }
}


If the data is meant to live as assets (catalogs, specs): use ScriptableObject with [CreateAssetMenu].

If it’s a scene component: MonoBehaviour without [CreateAssetMenu].

3.6. SystemAPI usage & static helpers (P14)

Symptom:
EA0004/EA0006: SystemAPI.* can only be used in a system's OnCreate/OnUpdate...

Fix pattern:

Never call SystemAPI in static helpers.

Keep SystemAPI calls inside OnCreate, OnDestroy, OnUpdate instance methods.

For helpers:

// Helper uses SystemState and queries:
static bool TryGetTime(ref SystemState state, out TimeState time)
{
    var q = state.GetEntityQuery(ComponentType.ReadOnly<TimeState>());
    return q.TryGetSingleton(out time);
}


Then call TryGetTime from OnUpdate.

3.7. Bool + Burst function pointers (P15)

Symptom:
BC1063: 'bool' is not blittable in calli / similar.

Fix pattern:

For structs passed through Burst function pointers, change bool fields to byte or mark with [MarshalAs(UnmanagedType.U1)] as listed in the brief.

Prefer using byte flags for configs to keep everything trivially blittable.

3.8. C# version compatibility (P3)

Symptoms:

ref readonly local usage, records, or other C# 12 syntax.

Fix pattern:

Replace ref readonly locals with ref locals + in parameters.

No records; stick to struct + interfaces.

4. PureDOTS-specific cleanup that unblocks both games

The brief explicitly calls out some known violators in PureDOTS (e.g. description helpers, spell effect code, what-if systems) that break Burst rules. These are high-value to fix because both games rely on them. 

TRI_PROJECT_BRIEFING

Have an agent:

Open the systems mentioned under P15 and P13 in the brief.

Apply:

FixedString fixes (static readonly constants, no .ToString() in Burst paths).

Bool/byte alignment for configs.

Rebuild PureDOTS until:

No Burst compile errors in Packages/com.moni.puredots.

No stub types left that mask real implementations.

This gives you a clean, deterministic core for both games.

5. Game-side AI systems: “fix, don’t bypass” strategy

For both Space4X and Godgame, treat systems as follows:

5.1. For every AI system that currently fails to compile

Make sure all components it uses exist in PureDOTS (Body ECS) with complete definitions:

“Needs” component.

“Task/Job/Order” state component(s).

“Motion/Position” or equivalent.

If a field is currently “design-only” and never read, implement a minimal real field, don’t comment it out.

Where the logic is too ambitious right now (e.g. full GOAP/plans), keep the system enabled but gate heavy logic with simple component flags or config values:

if (!config.AISimulationEnabled)
    return; // ai system still exists, it just short-circuits cleanly


or

if (!SystemAPI.HasComponent<SomePrerequisite>(entity))
    continue;


This keeps the system structurally correct and ready for expansion, without having to remove it.

5.2. For runtime “no singleton / no registry” errors

Whenever a system assumes a singleton or registry exists:

Ensure the singleton/registry is created in a single bootstrap flow:

One config (authoring / asset).

One bootstrap system that seeds the runtime component or singleton from that config.

Remove extra manual EntityManager.AddComponentData paths that double-seed things like TimeState / RewindState.

This generally fixes a lot of “nothing moves / nothing updates / sanity systems complaining” issues.
any non-per frame or hot systems may be de-bursted.
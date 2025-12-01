# Unity DOTS Common Errors and Prevention Guide

**Status:** Reference Guide  
**Category:** Error Prevention  
**Scope:** Common Unity DOTS/ECS compilation errors and how to avoid them  
**Created:** 2025-11-26  
**Last Updated:** 2025-11-26

---

## Purpose

This guide documents common Unity DOTS errors encountered in the Space4X project and provides preventive measures for agents working on the codebase. Read this document BEFORE modifying ECS components, systems, or adding new types.

---

## Error Categories

### 1. CS0101: Duplicate Type Definitions

**Error Example:**
```
error CS0101: The namespace 'Space4X.Registry' already contains a definition for 'StrikeCraftState'
```

**Root Cause:** Multiple files define the same type name in the same namespace.

**Common Scenarios:**
- Adding a new component without checking if it already exists
- Copying code from one file to another without renaming
- Different authors creating similar components in different files

**Prevention:**
1. **ALWAYS grep before creating new types:**
   ```bash
   # Check for existing type before adding
   grep -r "public struct MyNewComponent" Assets/Scripts/Space4x/
   grep -r "public enum MyNewEnum" Assets/Scripts/Space4x/
   ```

2. **Use canonical component files:**
   - Combat components → `Space4XCombatComponents.cs`
   - Alignment components → `Space4XAlignmentComponents.cs`
   - Module components → `Space4XModuleComponents.cs` or `ModuleDataSchemas.cs`
   - Stance components → Choose ONE file (not both `Space4XStanceComponents.cs` AND `ModuleDataSchemas.cs`)

3. **When finding duplicates:**
   - Keep the more complete definition
   - Update all references to use the canonical location
   - Delete the duplicate

**Files Commonly Affected:**
- `ModuleDataSchemas.cs` (large catch-all file)
- Individual component files like `Space4XStrikeCraftComponents.cs`

---

### 2. CS0104: Ambiguous Type References

**Error Example:**
```
error CS0104: 'Random' is an ambiguous reference between 'Unity.Mathematics.Random' and 'UnityEngine.Random'
error CS0104: 'PresentationSystemGroup' is an ambiguous reference between 'Unity.Entities.PresentationSystemGroup' and 'PureDOTS.Systems.PresentationSystemGroup'
```

**Root Cause:** Two namespaces define the same type name and both are imported via `using` statements.

**Prevention:**

1. **For `Random`:** Always use fully-qualified name in DOTS code:
   ```csharp
   // ❌ BAD - ambiguous
   var random = new Random(seed);
   
   // ✅ GOOD - explicit
   var random = new Unity.Mathematics.Random(seed);
   ```

2. **For `PresentationSystemGroup`:** Use the PureDOTS version with explicit namespace:
   ```csharp
   // ❌ BAD - ambiguous
   [UpdateInGroup(typeof(PresentationSystemGroup))]
   
   // ✅ GOOD - explicit (prefer PureDOTS in this project)
   [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
   // OR use alias
   using PresentationSystemGroup = Unity.Entities.PresentationSystemGroup;
   ```

3. **Use namespace aliases when needed:**
   ```csharp
   using MathRandom = Unity.Mathematics.Random;
   using EngineRandom = UnityEngine.Random;
   ```

**Files Commonly Affected:**
- Systems with dev/spawn logic
- Camera systems
- Telemetry systems
- Any file importing both `PureDOTS.Systems` and `Unity.Entities`

---

### 3. CS8377: ComponentLookup Requires Non-Nullable Value Type

**Error Example:**
```
error CS8377: The type 'VesselStanceComponent' must be a non-nullable value type, along with all fields at any level of nesting, in order to use it as parameter 'T' in the generic type or method 'ComponentLookup<T>'
```

**Root Cause:** The component contains nullable fields or reference types that violate ECS blittable requirements.

**Prevention:**

1. **ECS components must be blittable:**
   ```csharp
   // ❌ BAD - contains reference type
   public struct MyComponent : IComponentData
   {
       public string Name;  // Reference type!
       public int? OptionalValue;  // Nullable!
   }
   
   // ✅ GOOD - all blittable types
   public struct MyComponent : IComponentData
   {
       public FixedString64Bytes Name;  // Fixed-size blittable string
       public int Value;
       public byte HasValue;  // Use byte flag instead of nullable
   }
   ```

2. **Allowed types in ECS components:**
   - Primitives: `int`, `float`, `byte`, `uint`, etc.
   - `half` (16-bit float)
   - Unity.Mathematics types: `float3`, `quaternion`, etc.
   - `Entity`
   - `FixedString32Bytes`, `FixedString64Bytes`, `FixedString128Bytes`
   - Other blittable structs

3. **Forbidden types in ECS components:**
   - `string`
   - `object`
   - Any reference type
   - Nullable value types (`int?`, `float?`)
   - Arrays (use `DynamicBuffer` instead)

---

### 4. CS0315: Boxing Conversion for ECS Interfaces

**Error Example:**
```
error CS0315: The type 'AICommandQueue' cannot be used as type parameter 'T' in the generic type or method 'BufferLookup<T>'. There is no boxing conversion from 'AICommandQueue' to 'IBufferElementData'.
```

**Root Cause:** Using wrong lookup type for the component's interface.

**Prevention:**

1. **Match lookup type to interface:**
   ```csharp
   // For IComponentData - use ComponentLookup
   public struct MyComponent : IComponentData { }
   ComponentLookup<MyComponent> lookup;  // ✅
   
   // For IBufferElementData - use BufferLookup
   public struct MyBufferElement : IBufferElementData { }
   BufferLookup<MyBufferElement> bufferLookup;  // ✅
   
   // ❌ WRONG - mismatched types
   BufferLookup<MyComponent> wrongLookup;  // ERROR!
   ```

2. **Common interface types:**
   - `IComponentData` → Single component per entity → `ComponentLookup<T>`
   - `IBufferElementData` → Dynamic array per entity → `BufferLookup<T>`
   - `ISharedComponentData` → Shared across entities → Special handling
   - `IEnableableComponent` → Can be enabled/disabled

3. **Check interface before using lookups:**
   ```csharp
   // Find the struct definition first
   public struct AICommandQueue : IComponentData  // Uses ComponentLookup
   public struct AffiliationTag : IBufferElementData  // Uses BufferLookup
   ```

---

### 5. CS0246: Type or Namespace Not Found

**Error Example:**
```
error CS0246: The type or namespace name 'VesselMovement' could not be found
error CS0246: The type or namespace name 'ReadOnlyAttribute' could not be found
```

**Root Cause:** Missing `using` statement or type defined in different namespace/assembly.

**Prevention:**

1. **Common missing namespaces in Space4X:**
   ```csharp
   // For VesselMovement, VesselAIState
   using Space4X.Runtime;
   
   // For [ReadOnly] attribute in jobs
   using Unity.Collections;
   
   // For ECS types
   using Unity.Entities;
   
   // For math types
   using Unity.Mathematics;
   
   // For LocalTransform, etc.
   using Unity.Transforms;
   
   // For PureDOTS components
   using PureDOTS.Runtime.Components;
   using PureDOTS.Systems;
   ```

2. **Check where types are defined:**
   ```bash
   grep -r "public struct VesselMovement" Assets/Scripts/
   # Output: Assets/Scripts/Space4x/Runtime/VesselComponents.cs
   # → Need: using Space4X.Runtime;
   ```

3. **For custom system groups:**
   - `TelemetrySystemGroup` → Check PureDOTS or define locally
   - `Space4XTransportAISystemGroup` → Define if missing or use existing

### 5a. CS0246 in Source-Generated Code

**Error Example:**
```
SystemGenerator\..\Space4XDockingSystem__System_4542016951.g.cs(169,81): error CS0246: The type or namespace name 'VesselAIState' could not be found
```

**Root Cause:** The **source system file** is missing a `using` statement. Unity's source generator copies usings from the source file, so if the source is missing `using Space4X.Runtime;`, the generated code will also be missing it.

**Key Insight:** You CANNOT fix generated `.g.cs` files directly. Fix the SOURCE file that triggered the generation.

**Why These Errors Persist:**
- Generated code errors appear repeatedly in console until source files are fixed
- Each Unity domain reload regenerates the files with the same errors
- The same error may appear 5-10+ times (once per generated method that uses the type)

**Prevention:**
1. When you see errors in `*.g.cs` files, find the source file:
   - `Space4XDockingSystem__System_4542016951.g.cs` → Fix `Space4XDockingSystem.cs`
   - `Space4XFormationSystem__System_18837275231.g.cs` → Fix `Space4XFormationSystem.cs`

2. Add the missing `using` statement to the source file
3. Unity will regenerate the `.g.cs` file with the fix

**Mapping Pattern:** `{SystemName}__System_{hash}.g.cs` → `{SystemName}.cs`

---

### 6. Burst Compilation Errors (BC1016, BC1091)

**Error Examples:**
```
Burst error BC1016: The managed function `System.String.get_Length` is not supported
Burst error BC1091: External and internal calls are not allowed inside static constructors
```

**Root Cause:** 
- BC1016: Using managed (non-blittable) types in Burst-compiled code
- BC1091: Static constructors (`static readonly` field initialization) run inside Burst context

**Prevention:**

1. **Never use `string` in Burst code:**
   ```csharp
   // ❌ BAD - managed string
   [BurstCompile]
   void MyBurstMethod()
   {
       string name = "test";  // ERROR in Burst!
   }
   
   // ✅ GOOD - fixed string built without managed string in Burst
   [BurstCompile]
   void MyBurstMethod()
   {
       var name = MyIds.DefaultName; // prebuilt in OnCreate
   }

   // ✅ Pattern: prebuild tokens outside Burst and read them inside Burst
   [BurstDiscard] static FixedString64Bytes FS(string s)
   {
       var fs = default(FixedString64Bytes);
       for (int i = 0; i < s.Length; i++) fs.Append(s[i]);
       return fs;
   }

   public void OnCreate(ref SystemState state)
   {
       var e = state.EntityManager.CreateEntity(typeof(MyIds));
       state.EntityManager.SetComponentData(e, new MyIds { DefaultName = FS("Shield") });
   }

   [BurstCompile]
   public void OnUpdate(ref SystemState state)
   {
       var ids = SystemAPI.GetSingleton<MyIds>(); // safe in Burst
   }
   ```

2. **⚠️ NEW: Static readonly FixedString fields cause BC1091:**
   ```csharp
   // ❌ BAD - static constructor runs in Burst context
   public partial class MySystem : SystemBase
   {
       private static readonly FixedString32Bytes Name = new FixedString32Bytes("Shield");
       // BC1091: FixedString constructor called in static constructor!
   }
   
   // ✅ GOOD - Initialize in OnCreate or use [BurstDiscard]
   public partial class MySystem : SystemBase
   {
       private FixedString32Bytes _name;
       
       protected override void OnCreate()
       {
           _name = new FixedString32Bytes("Shield");  // Safe - not in Burst
       }
   }
   ```

3. **Use [BurstDiscard] for debug-only code:**
   ```csharp
   [BurstCompile]
   public void OnUpdate(ref SystemState state)
   {
       // Burst-compatible code here
       LogDebug("Some message");  // Uses BurstDiscard
   }
   
   [BurstDiscard]
   private void LogDebug(string message)
   {
       Debug.Log(message);  // Only runs when Burst disabled
   }
   ```

4. **Common problematic operations:**
   - String interpolation (`$"Value: {x}"`)
   - `Debug.Log()` directly in Burst code
   - LINQ operations
   - Boxing (`object x = myInt;`)
   - Static field initialization with `new FixedString*()`

---

### 6a. EA0002: Entity Construction Warning

**Warning Example:**
```
warning EA0002: You should only construct new BuffEntry
warning EA0002: You should only construct new LessonEntry
```

**Root Cause:** Constructing ECS component/buffer element types with `new` in authoring code where it expects entity baking.

**Files Affected (PureDOTS):**
- `BuffCatalogAuthoring.cs:73`
- `LessonCatalogAuthoring.cs:79`
- `CultureStoryCatalogAuthoring.cs:65`
- `SpellCatalogAuthoring.cs:83`

**Note:** These are warnings in PureDOTS authoring code, not Space4X.

---

### 6b. CS0618: Obsolete Type Usage Warning

**Warning Example:**
```
warning CS0618: 'VillageAlignmentState' is obsolete: 'Use VillagerAlignment instead.'
warning CS0618: 'GuildAlignment' is obsolete: 'GuildAlignment has been replaced by VillagerAlignment + GuildOutlookSet'
```

**Root Cause:** Using deprecated component types that have been replaced.

**Files Affected (PureDOTS):**
- `LegacyAggregateAlignmentMigrationSystem.cs` - This is intentionally a migration system

**Note:** The migration system uses obsolete types intentionally to migrate old data. These warnings can be suppressed with `#pragma warning disable CS0618`.

---

### 7. CreateAssetMenu on Non-ScriptableObject (Warning)

**Warning Example:**
```
CreateAssetMenu attribute on PureDOTS.Authoring.Culture.CultureStoryCatalogAuthoring will be ignored as it is not derived from ScriptableObject.
```

**Root Cause:** `[CreateAssetMenu]` attribute applied to a class that doesn't inherit from `ScriptableObject`.

**Prevention:**
1. Only use `[CreateAssetMenu]` on classes that inherit from `ScriptableObject`
2. For authoring components that inherit from `MonoBehaviour`, use baking instead
3. If you need both editor asset creation AND authoring:
   - Create a separate `ScriptableObject` for the data
   - Reference it from the authoring component

**Note:** These warnings are in PureDOTS package, not Space4X. Fix them in the PureDOTS project.

---

### 8. Burst Errors in Package Code (BC1016)

**Error Example:**
```
Burst error BC1016: The managed function `System.String.get_Length` is not supported
at PureDOTS.Systems.Spells.SpellEffectExecutionSystem.ProcessSpellEffectsJob.ApplyShieldEffect
```

**Root Cause:** PureDOTS code is using managed `string` types inside Burst-compiled methods.

**Key Files to Fix (in PureDOTS, not Space4X):**
- `SpellEffectExecutionSystem.cs:311` - `ApplyShieldEffect` uses string
- `TimelineBranchSystem.cs:108` - `WhatIfSimulationSystem` uses string
- `BandFormationSystem.cs:255` - `GoalToDescription` uses string
- `LessonAcquisitionSystem.cs:358` - `CheckAttributeRequirement` uses string

**Prevention (for PureDOTS):**
1. Use `FixedString64Bytes` or `FixedString128Bytes` instead of `string`
2. Initialize fixed strings OUTSIDE of Burst code, pass as parameters
3. Use `[BurstDiscard]` for debug-only string operations

**Note:** These errors require fixes in the PureDOTS project at `C:\Users\Moni\Documents\claudeprojects\unity\PureDOTS\`.

---

### 9. CS0117/CS1061: Component Schema Mismatch

**Error Examples:**
```
error CS0117: 'Space4XShield' does not contain a definition for 'CurrentStrength'
error CS1061: 'HullIntegrity' does not contain a definition for 'Maximum'
error CS0117: 'Space4XWeapon' does not contain a definition for 'WeaponId'
```

**Root Cause:** Component definitions have been refactored with different field names, but usage sites weren't updated.

**Common Schema Mismatches Found:**

| Component | Old Field Names (Usage) | New Field Names (Definition) |
|-----------|------------------------|------------------------------|
| `HullIntegrity` | `CurrentHull`, `MaxHull`, `BaseMaxHull`, `Maximum` | `Current`, `Max`, `BaseMax`, `Ratio` (property) |
| `Space4XShield` | `CurrentStrength`, `MaxStrength`, `RegenRate`, `RegenDelay`, `LastDamageTick` | `Current`, `Maximum`, `RechargeRate`, `RechargeDelay`, `CurrentDelay` |
| `Space4XWeapon` | `WeaponId`, `Range`, `FireRate`, `LastFireTick` | `Type`, `OptimalRange`, `MaxRange`, `CooldownTicks`, `CurrentCooldown` |

**Prevention:**

1. **When refactoring component fields:**
   - Search for all usages before renaming: `grep -r "myComponent\." Assets/Scripts/`
   - Update all usage sites in the same commit
   
2. **Use IDE refactoring tools:**
   - Rider/VS: Right-click → Rename (updates all references)
   
3. **Check the actual definition:**
   ```bash
   grep -A 20 "public struct MyComponent" Assets/Scripts/Space4x/
   ```

**Files Commonly Affected:**
- `Space4XDevSpawnSystem.cs` - creates entities with old field names
- `Space4XCombatDemoAuthoring.cs` - baking uses old names
- Various systems accessing component fields

---

### 10. CS1654: Cannot Modify Foreach Iteration Variable

**Error Example:**
```
error CS1654: Cannot modify members of 'weapons' because it is a 'foreach iteration variable'
error CS1654: Cannot modify members of 'modifiers' because it is a 'foreach iteration variable'
```

**Root Cause:** Trying to modify struct fields during foreach iteration of a buffer.

**Why This Happens:**
- `DynamicBuffer<T>` foreach returns value copies (structs)
- Modifying the copy doesn't modify the buffer
- C# prevents this to avoid bugs

**Prevention:**

```csharp
// ❌ BAD - modifying iteration variable
foreach (var weapon in weaponBuffer)
{
    weapon.CurrentCooldown -= 1;  // ERROR!
}

// ✅ GOOD - use indexed access
for (int i = 0; i < weaponBuffer.Length; i++)
{
    var weapon = weaponBuffer[i];
    weapon.CurrentCooldown -= 1;
    weaponBuffer[i] = weapon;  // Write back
}

// ✅ GOOD - use ElementAt with ref (Unity 2022+)
for (int i = 0; i < weaponBuffer.Length; i++)
{
    ref var weapon = ref weaponBuffer.ElementAt(i);
    weapon.CurrentCooldown -= 1;  // Modifies in-place
}
```

**Files Commonly Affected:**
- `Space4XDiplomacySystem.cs` - modifiers, statuses buffers
- `Space4XEconomySystem.cs` - prices, offers, events buffers
- `Space4XGrudgeSystem.cs` - grudge buffers
- `Space4XSupplySystem.cs` - morale modifiers
- `Space4XCombatSystem.cs` - weapons buffer
- `Space4XFocusSystem.cs` - abilities buffer
- `Space4XPatriotismSystem.cs` - belongings, affiliations

---

### 11. EA0004/EA0006: SystemAPI Usage Errors

**Error Examples:**
```
error EA0004: You may not use the SystemAPI member 'Query' outside of a system
error EA0006: You may not use the SystemAPI member 'HasComponent' inside of a static method
```

**Root Cause:** `SystemAPI` methods require system context and cannot be used in:
- Static methods
- Helper classes
- Jobs (unless using SystemAPI.Query in OnUpdate)

**Prevention:**

```csharp
// ❌ BAD - SystemAPI in static method
public static void ProcessEntities()
{
    foreach (var (data, entity) in SystemAPI.Query<RefRW<MyData>>())  // ERROR!
    { }
}

// ✅ GOOD - Pass lookups from OnUpdate
public void OnUpdate(ref SystemState state)
{
    var myDataLookup = SystemAPI.GetComponentLookup<MyData>();
    ProcessEntities(ref state, myDataLookup);
}

private void ProcessEntities(ref SystemState state, ComponentLookup<MyData> lookup)
{
    // Use lookup instead of SystemAPI
}

// ❌ BAD - SystemAPI.Query in job
public partial struct MyJob : IJobEntity
{
    public void Execute(Entity entity)
    {
        foreach (var x in SystemAPI.Query<...>())  // ERROR!
    }
}

// ✅ GOOD - Pass data via job fields
public partial struct MyJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<OtherData> OtherLookup;
    
    public void Execute(Entity entity)
    {
        if (OtherLookup.HasComponent(entity)) { ... }
    }
}
```

**Files Commonly Affected:**
- `Space4XThreatBehaviorSystem.cs` - Query in non-system context
- `Space4XStrikeCraftBehaviorSystem.cs` - Query in helper
- `Space4XFleetCoordinationAISystem.cs` - Query outside system
- `VesselMovementSystem.cs` - Query in helper
- `Space4XAutomationSystem.cs` - SystemAPI in static methods
- `Space4XDockingSystem.cs` - HasBuffer/GetBuffer in static

---

### 12. CS0120: Static Method Accessing Instance Member

**Error Example:**
```
error CS0120: An object reference is required for the non-static field 'Space4XAutomationSystem.__TypeHandle'
```

**Root Cause:** Unity ECS source generator creates `__TypeHandle` as an instance field, but static methods try to access it.

**Prevention:**

```csharp
// ❌ BAD - Static method using SystemAPI
public partial class MySystem : SystemBase
{
    private static void StaticHelper(Entity entity)
    {
        // This generates code that tries to access __TypeHandle
        var data = SystemAPI.GetComponent<MyData>(entity);  // ERROR!
    }
}

// ✅ GOOD - Convert to instance method
public partial class MySystem : SystemBase
{
    private void InstanceHelper(Entity entity)
    {
        var data = SystemAPI.GetComponent<MyData>(entity);  // OK
    }
}

// ✅ GOOD - Pass lookup as parameter
public partial class MySystem : SystemBase
{
    protected override void OnUpdate()
    {
        var lookup = SystemAPI.GetComponentLookup<MyData>(true);
        StaticHelper(entity, lookup);
    }
    
    private static void StaticHelper(Entity entity, ComponentLookup<MyData> lookup)
    {
        var data = lookup[entity];  // OK
    }
}
```

---

### 13. CS0246: Missing PureDOTS Types (TimeState, RewindState)

**Error Example:**
```
error CS0246: The type or namespace name 'TimeState' could not be found
error CS0246: The type or namespace name 'RewindState' could not be found
error CS0103: The name 'RewindMode' does not exist in the current context
```

**Root Cause:** Space4X systems reference PureDOTS time/rewind types that either:
1. Don't exist yet in PureDOTS
2. Are in a different namespace
3. Need assembly reference

**Prevention:**

1. **Check if type exists:**
   ```bash
   grep -r "public struct TimeState" ../PureDOTS/
   grep -r "public enum RewindMode" ../PureDOTS/
   ```

2. **If missing, either:**
   - Create locally in Space4X:
     ```csharp
     public struct TimeState : IComponentData
     {
         public uint CurrentTick;
         public float DeltaTime;
         public bool IsPaused;
     }
     ```
   - Or remove the dependency until PureDOTS provides it

3. **If exists in PureDOTS, add proper using:**
   ```csharp
   using PureDOTS.Runtime.Time;  // or wherever defined
   ```

**Files Commonly Affected:**
- Most systems in `Registry/` that check time-based conditions
- `Space4XAutomationSystem.cs`, `Space4XDockingSystem.cs`, `Space4XFormationSystem.cs`, etc.

---

### 14. CS0315: IComponentData Used with AddBuffer

**Error Example:**
```
error CS0315: The type 'Space4XWeapon' cannot be used as type parameter 'T' in 'EntityCommandBuffer.AddBuffer<T>()'. There is no boxing conversion from 'Space4XWeapon' to 'IBufferElementData'.
```

**Root Cause:** `Space4XWeapon` is defined as `IComponentData` but code tries to use `AddBuffer<Space4XWeapon>()` which requires `IBufferElementData`.

**Decision Required:** Should weapons be:
- **Single component** (`IComponentData`) - one weapon per entity
- **Buffer** (`IBufferElementData`) - multiple weapons per entity

**If weapons should be a buffer:**
```csharp
// Change definition
public struct Space4XWeapon : IBufferElementData  // was IComponentData
{
    // ... fields
}

// Usage stays the same
ecb.AddBuffer<Space4XWeapon>(entity);
```

**If weapons should stay as component:**
```csharp
// Change usage - don't use AddBuffer
ecb.AddComponent<Space4XWeapon>(entity, new Space4XWeapon { ... });
```

---

### 15. Type Conversion Errors (half, FixedString)

**Error Examples:**
```
error CS0266: Cannot implicitly convert type 'sbyte' to 'Unity.Mathematics.half'
error CS0266: Cannot implicitly convert type 'float' to 'Unity.Mathematics.half'
error CS0029: Cannot implicitly convert type 'FixedString64Bytes' to 'ushort'
```

**Prevention:**

```csharp
// ❌ BAD - implicit conversion to half fails
half value = someFloat;   // ERROR
half value = -1;          // ERROR (sbyte)

// ✅ GOOD - explicit conversion
half value = (half)someFloat;
half value = (half)(-1);

// ❌ BAD - wrong type entirely
ushort id = someFixedString;  // ERROR - completely different types!

// ✅ GOOD - use correct type
FixedString64Bytes name = someFixedString;  // Same type
// OR convert properly
ushort id = ParseFixedStringToId(someFixedString);  // Custom conversion
```

---

### 16. SGQC001: WithAll on Ambiguous Types

**Error Example:**
```
error SGQC001: WithAll<ResourceSourceState>() is not supported. WithAll<T>() may only be invoked on types that implement IComponentData...
```

**Root Cause:** The type name exists in MULTIPLE namespaces (e.g., both `Space4X.Registry.ResourceSourceState` and `PureDOTS.Runtime.Components.ResourceSourceState`). The compiler picks the wrong one which may not implement `IComponentData`.

**Prevention:**

```csharp
// ❌ BAD - ambiguous imports
using PureDOTS.Runtime.Components;  // Has ResourceSourceState
using Space4X.Registry;             // Also has ResourceSourceState

// In QueryBuilder - which ResourceSourceState?
.WithAll<ResourceSourceState>()  // SGQC001!

// ✅ GOOD - use explicit namespace alias
using PureDOTSResource = PureDOTS.Runtime.Components.ResourceSourceState;
// Then only use the Space4X version directly:
.WithAll<Space4X.Registry.ResourceSourceState>()

// OR remove the conflicting using:
// using PureDOTS.Runtime.Components;  // REMOVE if not needed
```

---

### 17. SGSG0002: Missing SystemState Parameter

**Error Example:**
```
error SGSG0002: No reference to SystemState was found for function with SystemAPI.Query access, add `ref SystemState ...` as method parameter.
```

**Root Cause:** `SystemAPI.Query` and other SystemAPI methods require access to `ref SystemState`, but you're calling them from a helper method that doesn't receive the state.

**Prevention:**

```csharp
// ❌ BAD - helper method without SystemState
private void FindTargets(Entity entity, float3 position)
{
    foreach (var x in SystemAPI.Query<...>())  // SGSG0002!
    { }
}

// ✅ GOOD - pass SystemState to helper
private void FindTargets(ref SystemState state, Entity entity, float3 position)
{
    foreach (var x in SystemAPI.Query<...>())  // OK - state is available
    { }
}

// Call from OnUpdate:
public void OnUpdate(ref SystemState state)
{
    FindTargets(ref state, entity, position);
}
```

**Note:** This is different from EA0004 (Query outside system). SGSG0002 means you're in a system but the helper method lacks the `ref SystemState` parameter.

---

### 18. CS0104: Ambiguous TimeState/RewindState/RewindMode

**Error Example:**
```
error CS0104: 'TimeState' is an ambiguous reference between 'Space4X.Registry.TimeState' and 'PureDOTS.Runtime.Components.TimeState'
error CS0104: 'RewindState' is an ambiguous reference between 'Space4X.Registry.RewindState' and 'PureDOTS.Runtime.Components.RewindState'
error CS0104: 'RewindMode' is an ambiguous reference between 'Space4X.Registry.RewindMode' and 'PureDOTS.Runtime.Components.RewindMode'
```

**Root Cause:** Both Space4X and PureDOTS define `TimeState`, `RewindState`, and `RewindMode`. When both namespaces are imported, the compiler cannot determine which to use.

**Decision Required:** Use ONE source for time types:
- **Option A:** Delete Space4X versions, use PureDOTS exclusively
- **Option B:** Keep Space4X versions, don't import `PureDOTS.Runtime.Components` 
- **Option C:** Use fully-qualified names everywhere

**Prevention (Recommended: Use PureDOTS):**

```csharp
// ❌ BAD - conflicting imports
using PureDOTS.Runtime.Components;  // Has TimeState
using Space4X.Registry;             // Also has TimeState

// ✅ FIX OPTION 1: Use alias for one
using PureDOTSTimeState = PureDOTS.Runtime.Components.TimeState;
// Then use Space4X.Registry.TimeState directly

// ✅ FIX OPTION 2: Fully qualify at usage
foreach (var (time, _) in SystemAPI.Query<RefRO<PureDOTS.Runtime.Components.TimeState>>())

// ✅ FIX OPTION 3 (RECOMMENDED): Delete Space4X duplicates
// Delete Space4XTimeComponents.cs
// Use only PureDOTS.Runtime.Components.TimeState
```

**Files Commonly Affected (20+ files):**
- Most AI systems: VesselAISystem, VesselTargetingSystem, VesselMovementSystem, etc.
- Carrier systems: CarrierPickupSystem
- Mining systems: MiningResourceSpawnSystem
- Scenario systems: Space4XMiningScenarioSystem, Space4XRefitScenarioSystem
- Fleet coordination: Space4XFleetCoordinationAISystem
- All systems that check time/pause state

---

### 19. CS0103/CS0117: Undefined Variables and Missing Enum Values

**Error Examples:**
```
error CS0103: The name 'speed' does not exist in the current context
error CS0103: The name 'EngagementState' does not exist in the current context
error CS0103: The name 'dockedBufferLookup' does not exist in the current context
error CS0117: 'SituationPhase' does not contain a definition for 'None'
error CS0117: 'SituationType' does not contain a definition for 'LifeSupportFailure'
error CS0117: 'EventCategory' does not contain a definition for 'Disaster'
```

**Root Cause:** 
- Variables referenced but never declared
- Enum values that don't exist in the enum definition
- Helper variables from refactored code that were deleted

**Prevention:**

1. **For undefined variables:** Check if variable was declared, or rename to match actual field:
   ```csharp
   // ❌ 'speed' doesn't exist
   var velocity = speed * direction;
   
   // ✅ Use actual field name
   var velocity = movement.Speed * direction;
   ```

2. **For missing enum values:** Add to enum definition or use existing value:
   ```csharp
   // Check actual enum definition:
   public enum SituationPhase : byte
   {
       Idle = 0,      // Use this instead of 'None'
       Active = 1,
       Complete = 2
   }
   ```

3. **For missing lookups:** Ensure they're declared in `OnCreate` or passed from `OnUpdate`:
   ```csharp
   // Declare lookups
   private BufferLookup<DockedVessel> _dockedBufferLookup;
   private ComponentLookup<DockingState> _dockingLookup;
   
   public void OnCreate(ref SystemState state)
   {
       _dockedBufferLookup = state.GetBufferLookup<DockedVessel>();
       _dockingLookup = state.GetComponentLookup<DockingState>();
   }
   ```

---

### 20. Extensive Component Schema Mismatches (Batch 2)

**Error Examples:**
```
error CS0117: 'MoraleState' does not contain a definition for 'BaseMorale'
error CS0117: 'MoraleState' does not contain a definition for 'CurrentMorale'
error CS0117: 'TargetSelectionProfile' does not contain a definition for 'PreferredEngagementRange'
error CS1061: 'TargetSelectionProfile' does not contain a definition for 'MaxTrackedTargets'
error CS1061: 'FormationAssignment' does not contain a definition for 'Cohesion'
error CS0117: 'CaptainState' does not contain a definition for 'Experience'
error CS1061: 'DepartmentStats' does not contain a definition for 'StressLevel'
error CS1061: 'ProcessingFacility' does not contain a definition for 'ProcessingSpeedMultiplier'
error CS1061: 'TargetPriority' does not contain a definition for 'CurrentTargetEntity'
error CS1061: 'MoraleModifier' does not contain a definition for 'SourceType'
error CS0117: 'TelemetryMetric' does not contain a definition for 'Timestamp'
error CS1061: 'Space4XEngagement' does not contain a definition for 'State'
error CS1061: 'SituationState' does not contain a definition for 'CurrentType'
```

**Root Cause:** Component definitions have been refactored but usage sites haven't been updated. This is a common pattern when multiple agents work on different files.

**Component Schema Mapping (Extended):**

| Component | Usage (Old) | Definition (New) |
|-----------|------------|------------------|
| `MoraleState` | `BaseMorale`, `CurrentMorale` | Check actual fields |
| `TargetSelectionProfile` | `PreferredEngagementRange`, `MaxTrackedTargets` | Check actual fields |
| `FormationAssignment` | `FleetEntity`, `Cohesion` | Check actual fields |
| `CaptainState` | `Experience`, `Morale` | Check actual fields |
| `DepartmentStats` | `StressLevel` | Check actual fields |
| `ProcessingFacility` | `ProcessingSpeedMultiplier`, `QualityMultiplier`, `EfficiencyMultiplier` | Check actual fields |
| `TargetPriority` | `CurrentTargetEntity`, `Score` | Check actual fields |
| `MoraleModifier` | `SourceType`, `Value`, `ExpirationTick` | Check actual fields |
| `TelemetryMetric` | `Timestamp` | Check actual fields |
| `Space4XEngagement` | `State`, `OpponentEntity` | Check actual fields |
| `SituationState` | `CurrentType` | Check actual fields |

**Prevention:**
1. Before using a component field, grep for the struct definition:
   ```bash
   grep -A 20 "public struct MoraleState" Assets/Scripts/
   ```
2. Use IDE "Go to Definition" to verify field names
3. When refactoring component fields, search for ALL usages

---

### 21. CS1654: Cannot Modify Foreach Iteration Variable

**Error Examples:**
```
error CS1654: Cannot modify members of 'weapons' because it is a 'foreach iteration variable'
error CS1654: Cannot modify members of 'speciesGrudges' because it is a 'foreach iteration variable'
error CS1654: Cannot modify members of 'moraleModifiers' because it is a 'foreach iteration variable'
```

**Root Cause:** In C#, foreach iteration variables are read-only. When iterating over a `DynamicBuffer<T>`, attempting to modify the struct elements directly fails because structs are value types.

**Prevention:**

```csharp
// ❌ BAD - foreach variable is read-only
foreach (var weapon in weaponsBuffer)
{
    weapon.CurrentCooldown -= 1;  // CS1654! Can't modify iteration variable
}

// ✅ GOOD - use indexed for loop with write-back
for (int i = 0; i < weaponsBuffer.Length; i++)
{
    var weapon = weaponsBuffer[i];  // Copy to local
    weapon.CurrentCooldown -= 1;     // Modify local
    weaponsBuffer[i] = weapon;       // Write back to buffer
}
```

**Common Buffer Types Affected:**
- Weapons buffers (`DynamicBuffer<Space4XWeapon>`)
- Grudge buffers (`DynamicBuffer<FactionGrudge>`, `DynamicBuffer<PersonalGrudge>`)
- Morale modifiers (`DynamicBuffer<MoraleModifier>`)
- Affiliation buffers (`DynamicBuffer<AffiliationTag>`)
- Price/trade buffers (`DynamicBuffer<PriceEntry>`)
- Relation buffers (`DynamicBuffer<FactionRelation>`)

**Files Commonly Affected:**
- `Space4XCombatSystem.cs`
- `Space4XGrudgeSystem.cs`
- `Space4XEconomySystem.cs`
- `Space4XDiplomacySystem.cs`
- `Space4XPatriotismSystem.cs`
- `Space4XSupplySystem.cs`
- `Space4XFocusSystem.cs`

---

### 22. CS0103: Undefined Variable (Missing Declaration)

**Error Examples:**
```
error CS0103: The name 'pricesBuffer' does not exist in the current context
error CS0103: The name 'dockedBufferLookup' does not exist in the current context
```

**Root Cause:** Variable is used but never declared. Often happens when:
- Code was copy-pasted without including variable declarations
- Refactoring moved variable declaration but not usage
- BufferLookup/ComponentLookup fields weren't added to class

**Prevention:**

```csharp
// ❌ BAD - using undeclared buffer
var price = pricesBuffer[index];  // CS0103!

// ✅ GOOD - declare buffer before use
var pricesBuffer = SystemAPI.GetBuffer<PriceEntry>(marketEntity);
var price = pricesBuffer[index];

// For lookups, declare as class fields:
private BufferLookup<DockedVessel> _dockedBufferLookup;
private ComponentLookup<DockingState> _dockingLookup;

public void OnCreate(ref SystemState state)
{
    _dockedBufferLookup = state.GetBufferLookup<DockedVessel>();
    _dockingLookup = state.GetComponentLookup<DockingState>();
}

public void OnUpdate(ref SystemState state)
{
    _dockedBufferLookup.Update(ref state);  // Update every frame
    _dockingLookup.Update(ref state);
    // Now can use: var buffer = _dockedBufferLookup[entity];
}
```

---

## Quick Reference Checklist

Before adding new code, verify:

- [ ] **New Type?** → Grep codebase for existing definitions
- [ ] **Using Random?** → Use `Unity.Mathematics.Random` explicitly
- [ ] **System Group?** → Use fully-qualified name if ambiguous
- [ ] **New Component?** → Verify all fields are blittable
- [ ] **Using Lookup?** → Match lookup type to component interface
- [ ] **Missing Type?** → Add appropriate `using` statement
- [ ] **Burst Method?** → No strings, no Debug.Log, use FixedString
- [ ] **Refactoring Fields?** → Update all usage sites first
- [ ] **Modifying Buffer?** → Use indexed access, not foreach
- [ ] **SystemAPI?** → Only in non-static system methods
- [ ] **AddBuffer?** → Type must implement `IBufferElementData`
- [ ] **Using half?** → Explicit cast required from float/int
- [ ] **Helper Method with Query?** → Add `ref SystemState` parameter
- [ ] **Multiple Usings?** → Check for type name conflicts (SGQC001/CS0104)
- [ ] **TimeState/RewindState?** → Use PureDOTS version exclusively (CS0104)
- [ ] **Using Enum Value?** → Verify value exists in enum definition
- [ ] **Using Variable?** → Verify it's declared in scope

---

## File Organization Guidelines

| Component Type | Canonical File |
|---------------|----------------|
| Combat | `Space4XCombatComponents.cs` |
| Alignment | `Space4XAlignmentComponents.cs` |
| Module Data | `ModuleDataSchemas.cs` |
| Vessel AI | `VesselComponents.cs` (Runtime) |
| Strike Craft | `Space4XStrikeCraftComponents.cs` |
| Stance (choose one!) | `Space4XStanceComponents.cs` OR `ModuleDataSchemas.cs` |
| Demo/Dev | `Space4XDemoComponents.cs` |

---

## Related Documentation

- `AGENTS.md` - Repository guidelines
- `Docs/ORIENTATION.md` - Project overview
- `Docs/Guides/Space4X_PureDOTS_Entity_Mapping.md` - Entity patterns

---

**Last Updated:** 2025-11-27 (Round 2)  
**Status:** Reference Guide - Active


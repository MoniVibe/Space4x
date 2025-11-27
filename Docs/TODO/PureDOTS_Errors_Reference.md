# PureDOTS Errors Reference

**Created:** 2025-11-27  
**Purpose:** Track PureDOTS-related errors found in Space4X console output  
**Target Project:** `C:\Users\Moni\Documents\claudeprojects\unity\PureDOTS\`

---

## ⚠️ Formal Requests Submitted

These errors have been submitted as formal extension requests to:
**`PureDOTS/Docs/ExtensionRequests/`**

| Request File | Priority | Status |
|-------------|----------|--------|
| `2025-11-27-burst-bc1016-fixedstring-errors.md` | P0 | PENDING |
| `2025-11-27-spatial-partition-authoring-export.md` | P1 | PENDING |
| `2025-11-27-createassetmenu-warnings.md` | P2 | PENDING |

---

## Overview

These errors originate from the PureDOTS package and should be fixed in that project, not Space4X.

---

## Burst Errors (BC1016)

**Error:** `System.String.get_Length` is not supported in Burst

**Root Cause:** Using `new FixedString*(string)` constructor in Burst-compiled code calls `string.Length` which is a managed function.

### Affected Files (in PureDOTS)

| File | Line | Method | Error Type |
|------|------|--------|------------|
| `SpellEffectExecutionSystem.cs` | 311 | `ApplyShieldEffect` | `new FixedString*(string)` |
| `BandFormationSystem.cs` | 255 | `GoalToDescription` | `new FixedString*(string)` |
| `AggregateHelpers.cs` | 227 | `GeneratePseudoHistory` | `new FixedString*(string)` |
| `LessonAcquisitionSystem.cs` | 358 | `CheckAttributeRequirement` | `new FixedString*(string)` |
| `HazardEmitFromDamageSystem.cs` | 128 | `HazardEmitFromDamageJob.Execute` | `.ToString()` on FixedString |
| `LifeBoatEjectorSystem.cs` | 85 | `LifeBoatEjectorJob.Execute` | `.ToString()` on FixedString |
| `SchoolFoundingSystem.cs` | 148 | `ProcessFoundingRequestsJob.Execute` | `new FixedString*(string)` |

### Fix Patterns

**Pattern 1: `new FixedString*(string)` in Burst**
```csharp
// ❌ BAD - string in Burst code
var name = new FixedString64Bytes("Shield");  // BC1016!

// ✅ GOOD - pre-defined constant outside Burst
private static readonly FixedString64Bytes ShieldName = new FixedString64Bytes("Shield");

// Then use in Burst:
var name = ShieldName;  // OK - no string.Length call
```

**Pattern 2: `.ToString()` on FixedString in Burst**
```csharp
// ❌ BAD - ToString() is managed
var str = fixedString.ToString();  // BC1016!

// ✅ GOOD - use [BurstDiscard] for logging
[BurstDiscard]
private static void LogString(in FixedString64Bytes str)
{
    UnityEngine.Debug.Log(str.ToString());
}

// OR - just keep as FixedString, don't convert
// Most Unity APIs that need strings can use FixedString directly
```

---

## CreateAssetMenu Warnings

**Warning:** `CreateAssetMenu attribute will be ignored as class is not derived from ScriptableObject`

### Affected Classes (in PureDOTS)

| Class | File |
|-------|------|
| `CultureStoryCatalogAuthoring` | Authoring/Culture/ |
| `LessonCatalogAuthoring` | Authoring/Knowledge/ |
| `SpellCatalogAuthoring` | Authoring/Spells/ |
| `ItemPartCatalogAuthoring` | Authoring/Items/ |
| `EnlightenmentProfileAuthoring` | Authoring/Knowledge/ |
| `BuffCatalogAuthoring` | Authoring/Buffs/ |
| `SchoolComplexityCatalogAuthoring` | Authoring/Spells/ |
| `QualityFormulaAuthoring` | Authoring/Shared/ |
| `SpellSignatureCatalogAuthoring` | Authoring/Spells/ |
| `QualityCurveAuthoring` | Authoring/Shared/ |

### Fix Pattern

These classes likely derive from `MonoBehaviour` but have `[CreateAssetMenu]` which only works for `ScriptableObject`.

**Option A:** If intended as ScriptableObject, change inheritance:
```csharp
// Change from MonoBehaviour to ScriptableObject
public class MyCatalogAuthoring : ScriptableObject { }
```

**Option B:** If intended as authoring component, remove attribute:
```csharp
// Remove [CreateAssetMenu] from MonoBehaviour
// [CreateAssetMenu(...)]  // REMOVE
public class MyCatalogAuthoring : MonoBehaviour { }
```

---

## Missing Type: SpatialPartitionAuthoring

**Error:** `CS0234: 'SpatialPartitionAuthoring' does not exist in namespace 'PureDOTS.Runtime.Spatial'`

**Affected Space4X File:** `Assets/Scripts/Space4x/Editor/Space4XSceneSetupMenu.cs` (lines 198, 232)

**Fix Options:**

1. **Create the type in PureDOTS:**
   ```csharp
   namespace PureDOTS.Runtime.Spatial
   {
       public class SpatialPartitionAuthoring : MonoBehaviour
       {
           // ... implementation
       }
   }
   ```

2. **Or comment out usage in Space4X** until PureDOTS provides it

---

## Action Items

When working on PureDOTS project:

1. [ ] Fix Burst BC1016 errors by using static readonly FixedString constants
2. [ ] Fix CreateAssetMenu warnings - either change to ScriptableObject or remove attribute
3. [ ] Create/export `SpatialPartitionAuthoring` type
4. [ ] Verify Space4X compiles after PureDOTS fixes

---

**Note:** Copy relevant sections to `PureDOTS/Docs/TODO/` when working in that project.


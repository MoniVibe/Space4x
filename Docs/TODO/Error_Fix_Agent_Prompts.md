# Error Fix Agent Prompts - Batch 2025-11-27 (Round 2)

**Created:** 2025-11-27  
**Purpose:** Agent task assignments for fixing Space4X compilation errors  
**Prerequisites:** Read `Docs/Guides/Unity_DOTS_Common_Errors.md` first

---

## ⚠️ CRITICAL: How to Complete Tasks

**"Done" means you have:**
1. ✅ EDITED the actual `.cs` source files using `search_replace` or `write` tools
2. ✅ Verified the changes exist in the files (use `grep` to confirm)
3. ✅ Updated this document with completion status

**"Done" does NOT mean:**
- ❌ Just reading this document
- ❌ Understanding what needs to be done
- ❌ Planning to make changes

---

## Progress Tracking

| Agent | Status | Task Summary |
|-------|--------|--------------|
| 1 | ✅ COMPLETED | ResourceSourceState/Config types defined; mining scenario field names fixed |
| 2 | ✅ COMPLETED | Space4XCombatSystem weapons buffer uses indexed loop (no foreach) |
| 3 | ✅ COMPLETED | Space4XGrudgeSystem buffer writes use local copies (no foreach CS1654) |
| 4 | ✅ COMPLETED | Space4XEconomySystem pricesBuffer fixes |
| 5 | ✅ COMPLETED | SystemAPI helper fixes (ThreatBehaviorSystem; Automation helper uses lookups) |
| 6 | ✅ COMPLETED | Component schema mismatches (FocusIntegration, PatriotismGrudge, Supply) |
| 7 | ✅ COMPLETED | TelemetryMetric Timestamp→AddMetric; FocusGrowth situation fields; Fleet commander field; DevSpawnRegistry description |

**Status Legend:** ⬜ NOT STARTED | 🔄 IN PROGRESS | ✅ COMPLETED | 🔴 CRITICAL

---

## Agent 1: ResourceSourceState/Config Missing Types

**ERRORS:**
```
CS0246: 'ResourceSourceState' could not be found (DevSpawnSystem.cs:536, CombatDemoAuthoring.cs:540)
CS0246: 'ResourceSourceConfig' could not be found (DevSpawnSystem.cs:541, CombatDemoAuthoring.cs:545)
CS0117: 'ResourceSourceState' does not contain 'LastHarvestTick' (MiningScenarioSystem.cs:260)
CS0117: 'LastRecordedTick' does not contain 'Value' (MiningScenarioSystem.cs:278)
CS0117: 'HistoryTier' does not contain 'Value' (MiningScenarioSystem.cs:279)
```

**Files:**
- `Assets/Scripts/Space4x/Systems/Dev/Space4XDevSpawnSystem.cs`
- `Assets/Scripts/Space4x/Authoring/Space4XCombatDemoAuthoring.cs`
- `Assets/Scripts/Space4x/Scenario/Space4XMiningScenarioSystem.cs`

**STEP 1:** Find type definitions:
```bash
grep -rn "public struct ResourceSourceState" Assets/Scripts/Space4x/
grep -rn "public struct ResourceSourceConfig" Assets/Scripts/Space4x/
```

**STEP 2:** Add using or fully qualify:
```csharp
using Space4X.Registry;  // Add at top if types are there
```

**STEP 3:** Fix field names in MiningScenarioSystem.cs:
```bash
grep -A 20 "public struct ResourceSourceState" Assets/Scripts/Space4x/
```
Update `LastHarvestTick` → actual field name (maybe `LastHarvest` or `HarvestTick`).

**VERIFICATION:**
```bash
grep -c "ResourceSourceState\|ResourceSourceConfig" Assets/Scripts/Space4x/Systems/Dev/Space4XDevSpawnSystem.cs
```

---

## Agent 2: Space4XCombatSystem - Foreach Weapons Buffer

**ERRORS:**
```
CS1654: Cannot modify members of 'weapons' (lines 65, 84)
```

**File:** `Assets/Scripts/Space4x/Registry/Space4XCombatSystem.cs`

**FIX:** Convert foreach to indexed loop:
```csharp
// ❌ OLD
foreach (var weapon in weapons)
{
    weapon.CurrentCooldown -= 1;
}

// ✅ NEW
for (int i = 0; i < weapons.Length; i++)
{
    var weapon = weapons[i];
    weapon.CurrentCooldown -= 1;
    weapons[i] = weapon;
}
```

**VERIFICATION:**
```bash
grep -n "foreach.*weapons" Assets/Scripts/Space4x/Registry/Space4XCombatSystem.cs
# Should return NOTHING
```

---

## Agent 3: Space4XGrudgeSystem - 7 Foreach Locations

**ERRORS:**
```
CS1654: Cannot modify 'speciesGrudges' (line 146)
CS1654: Cannot modify 'factionGrudges' (lines 160, 226, 354)
CS1654: Cannot modify 'personalGrudges' (lines 174, 260, 381)
```

**File:** `Assets/Scripts/Space4x/Registry/Space4XGrudgeSystem.cs`

**FIX:** Convert ALL 7 foreach loops:
```csharp
// ❌ OLD
foreach (var grudge in speciesGrudges)
{
    grudge.Intensity *= decayRate;
}

// ✅ NEW
for (int i = 0; i < speciesGrudges.Length; i++)
{
    var grudge = speciesGrudges[i];
    grudge.Intensity *= decayRate;
    speciesGrudges[i] = grudge;
}
```

**VERIFICATION:**
```bash
grep -n "foreach.*Grudges" Assets/Scripts/Space4x/Registry/Space4XGrudgeSystem.cs
# Should return NOTHING
```

---

## Agent 4: Space4XEconomySystem - pricesBuffer Undefined

**ERRORS:**
```
CS0103: 'pricesBuffer' does not exist (lines 265, 293, 428, 430)
```

**File:** `Assets/Scripts/Space4x/Registry/Space4XEconomySystem.cs`

**STEP 1:** Check context around usages:
```bash
grep -B 10 "pricesBuffer" Assets/Scripts/Space4x/Registry/Space4XEconomySystem.cs
```

**STEP 2:** Declare the buffer. Options:
```csharp
// Option A: Get buffer in the loop
var pricesBuffer = SystemAPI.GetBuffer<PriceEntry>(marketEntity);

// Option B: Use BufferLookup
private BufferLookup<PriceEntry> _pricesLookup;

public void OnCreate(ref SystemState state)
{
    _pricesLookup = state.GetBufferLookup<PriceEntry>();
}

public void OnUpdate(ref SystemState state)
{
    _pricesLookup.Update(ref state);
    var pricesBuffer = _pricesLookup[marketEntity];
}
```

**VERIFICATION:**
```bash
grep -c "pricesBuffer" Assets/Scripts/Space4x/Registry/Space4XEconomySystem.cs
```

---

## Agent 5: Space4XAutomationSystem - Static Method with SystemAPI

**ERRORS:**
```
SGSG0002 + EA0006: SystemAPI.HasComponent in static method (line 171)
SGSG0002 + EA0006: SystemAPI.GetComponentRW in static method (line 173)
```

**File:** `Assets/Scripts/Space4x/Registry/Space4XAutomationSystem.cs`

**FIX:** Convert static → instance + add ref SystemState:
```csharp
// ❌ OLD
private static void ProcessAutomation(Entity entity)
{
    if (SystemAPI.HasComponent<AutomationState>(entity))
    {
        var state = SystemAPI.GetComponentRW<AutomationState>(entity);
    }
}

// ✅ NEW
private void ProcessAutomation(ref SystemState systemState, Entity entity)
{
    if (SystemAPI.HasComponent<AutomationState>(entity))
    {
        var state = SystemAPI.GetComponentRW<AutomationState>(entity);
    }
}

// Call site in OnUpdate:
ProcessAutomation(ref state, entity);
```

**VERIFICATION:**
```bash
grep -n "private static" Assets/Scripts/Space4x/Registry/Space4XAutomationSystem.cs
# Should return NOTHING if all converted
```

---

## Agent 6: Component Schema Mismatches (Multiple Files)

**File: `Space4XFocusIntegration.cs`**
```
CS1061: 'TargetSelectionProfile' missing 'MaxTrackedTargets' (221, 227)
CS1061: 'FormationAssignment' missing 'Cohesion' (291)
CS1061: 'DepartmentStats' missing 'StressLevel' (384)
CS1061: 'ProcessingFacility' missing 'ProcessingSpeedMultiplier/QualityMultiplier/EfficiencyMultiplier' (416-426)
CS1061: 'HullIntegrity' missing 'IsRepairing' (454)
```

**File: `Space4XPatriotismGrudgeIntegration.cs`**
```
CS1061: 'TargetPriority' missing 'CurrentTargetEntity' (152)
CS1061: 'TargetPriority' missing 'Score' (171)
CS1061: 'Space4XEngagement' missing 'OpponentEntity' (319)
```

**File: `Space4XSupplySystem.cs`**
```
CS1061: 'MoraleModifier' missing 'SourceType/Value/ExpirationTick' (425-441)
```

**TASK:** For EACH component, find actual fields:
```bash
grep -A 15 "public struct TargetSelectionProfile" Assets/Scripts/Space4x/
grep -A 15 "public struct FormationAssignment" Assets/Scripts/Space4x/
grep -A 15 "public struct DepartmentStats" Assets/Scripts/Space4x/
grep -A 15 "public struct ProcessingFacility" Assets/Scripts/Space4x/
grep -A 15 "public struct TargetPriority" Assets/Scripts/Space4x/
grep -A 15 "public struct MoraleModifier" Assets/Scripts/Space4x/
grep -A 15 "public struct Space4XEngagement" Assets/Scripts/Space4x/
```

Update ALL usages to match actual field names.

**VERIFICATION:**
```bash
grep -n "MaxTrackedTargets\|\.Cohesion\|\.StressLevel" Assets/Scripts/Space4x/
# Should return NOTHING
```

---

## Agent 7: TelemetryMetric + FocusGrowth + Fleet Fixes

**File: `Space4XStatsTelemetrySystem.cs`**
```
CS0117: 'TelemetryMetric' missing 'Timestamp' (9 locations: 103,111,119,127,135,143,158,166,174)
```

**File: `Space4XFocusGrowthSystem.cs`**
```
CS1061: 'SituationState' missing 'CurrentType' (376-378)
CS0117: 'SituationType' missing 'LifeSupportFailure' (377)
CS0117: 'SituationType' missing 'HullBreach' (378)
```

**File: `Space4XCombatDemoAuthoring.cs`**
```
CS0117: 'Space4XFleet' missing 'CommanderEntity' (137)
```

**File: `Space4XDevSpawnRegistry.cs`**
```
CS1061: 'FactionTemplate' missing 'description' (513)
```

**TASK:** Find actual definitions:
```bash
grep -A 15 "public struct TelemetryMetric" Assets/Scripts/Space4x/
grep -A 15 "public struct SituationState" Assets/Scripts/Space4x/
grep -A 15 "public enum SituationType" Assets/Scripts/Space4x/
grep -A 15 "public struct Space4XFleet" Assets/Scripts/Space4x/
grep -A 10 "class FactionTemplate" Assets/Scripts/Space4x/
```

**Common mappings:**
- `Timestamp` → `Tick` or `RecordedTick`
- `CurrentType` → `Type` or `Phase`
- `CommanderEntity` → `Commander` or `Leader`
- `description` → `Description` (capital D)

**VERIFICATION:**
```bash
grep -n "\.Timestamp" Assets/Scripts/Space4x/Registry/Space4XStatsTelemetrySystem.cs
# Should return NOTHING
```

---

## Execution Order

1. **Agent 5** - Fix static method (blocks code gen)
2. **Agent 2 + 3** - Foreach fixes (simple mechanical)
3. **Agent 4** - pricesBuffer declaration
4. **Agent 1** - ResourceSource types
5. **Agent 6 + 7** - Schema mismatches

---

## PureDOTS Errors (External - Not Space4X)

These are in PureDOTS, requests at `PureDOTS/Docs/ExtensionRequests/`:

| Request | Priority |
|---------|----------|
| Burst BC1091 (static constructor) | P0 |
| SpatialPartitionAuthoring | P1 |

---

**Last Updated:** 2025-11-27

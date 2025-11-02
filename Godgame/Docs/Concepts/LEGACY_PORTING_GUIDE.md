# Legacy Truth Sources → DOTS Porting Guide

**Purpose:** Systematic process for converting legacy MonoBehaviour truth sources to DOTS-compatible concepts.

**Legacy Source:** `Docs/Concepts/legacy/` (copied from original repository)  
**Target:** DOTS concepts in `Docs/Concepts/[Category]/`  
**Created:** 2025-10-31

---

## Porting Philosophy

### What We're Doing
✅ **Preserve:** Core contracts, game design intent, player experience goals  
✅ **Adapt:** MonoBehaviour → ECS components, GameObject → Entity  
✅ **Flag:** What's undefined, what needs design decisions  
✅ **Link:** Map to existing truth sources where applicable

### What We're NOT Doing
❌ **Blindly translate** - Don't assume all legacy features are in scope  
❌ **Implement immediately** - Concepts first, code second  
❌ **Ignore DOTS reality** - Check what's actually implemented  
❌ **Copy-paste** - Each legacy doc needs DOTS translation

---

## Porting Workflow

### Step 1: Read Legacy Doc
- Understand the intent
- Identify core mechanics
- Note dependencies on other systems

### Step 2: Check Truth Sources
- Search `Docs/TruthSources_Inventory.md`
- Search codebase for related components
- Determine: ✅ Exists, 🟡 Partial, ❌ Missing

### Step 3: Identify Blockers
- List `<CLARIFICATION NEEDED>` items
- Flag `<UNDEFINED>` dependencies
- Mark `<WIP>` sections

### Step 4: Choose Template
- Feature (specific mechanic)
- Mechanic (how it works)
- System (interconnected)
- Experience (player moment)

### Step 5: Write DOTS Concept
- Fill template with legacy intent
- Add truth source mapping
- Include WIP flags liberally
- Link to existing components

### Step 6: Update Inventory
- Add to concept dashboard
- Cross-reference in truth sources
- Link related concepts

---

## Porting Checklist (Per Legacy Doc)

- [ ] **Read legacy doc** - Understand intent
- [ ] **Check truth sources** - What exists vs needed
- [ ] **Grep codebase** - Find related code
- [ ] **List dependencies** - What other systems required
- [ ] **Choose template** - Feature/Mechanic/System/Experience
- [ ] **Map MonoBehaviour → DOTS** - Component translations
- [ ] **Flag uncertainties** - Use `<WIP>`, `<NEEDS SPEC>`, etc.
- [ ] **Write open questions** - Design decisions needed
- [ ] **Link truth sources** - Reference inventory
- [ ] **Update dashboard** - Add to README

---

## Translation Patterns

### MonoBehaviour → IComponentData

**Legacy:**
```csharp
public class AggregatePile : MonoBehaviour {
    public ResourceType type;
    public int amount;
    public Transform visualRoot;
}
```

**DOTS:**
```csharp
public struct AggregatePile : IComponentData {
    public ushort ResourceTypeIndex;  // Enum → ushort
    public float Amount;              // int → float (DOTS prefers floats)
}

// Visual is separate (hybrid rendering or companion GameObject)
// <NEEDS SPEC: How do we handle visuals in pure DOTS?>
```

### GameObject References → Entity

**Legacy:**
```csharp
public GameObject target;
```

**DOTS:**
```csharp
public Entity TargetEntity;
```

### Events → Buffers or Tags

**Legacy:**
```csharp
public event Action<ResourceType, int> OnTotalsChanged;
```

**DOTS:**
```csharp
// Option A: Event buffer
public struct StorehouseChangedEvent : IBufferElementData {
    public ushort ResourceTypeIndex;
    public int Amount;
}

// Option B: Tag component
public struct StorehouseChangedTag : IComponentData, IEnableableComponent { }
```

### Singleton Services → Entity Singletons

**Legacy:**
```csharp
TimeEngine.Instance.Pause(true);
```

**DOTS:**
```csharp
var timeState = SystemAPI.GetSingleton<TimeState>();
// Modify timeState.TimeScale = 0
```

### FindObjectOfType → Entity Queries

**Legacy:**
```csharp
var storehouse = FindObjectOfType<Storehouse>();
```

**DOTS:**
```csharp
var storehouseEntity = SystemAPI.GetSingletonEntity<StorehouseRegistry>();
// Or query:
foreach (var (storehouse, entity) in SystemAPI.Query<RefRO<GodgameStorehouse>>().WithEntityAccess()) {
    // ...
}
```

---

## Priority Matrix

| Legacy Doc | DOTS Component Exists? | Dependencies Clear? | Priority | Action |
|------------|------------------------|---------------------|----------|--------|
| Hand_StateMachine.md | ✅ Partial | ✅ Yes | HIGH | Complete missing states |
| RMBtruthsource.md | ✅ Partial | ✅ Yes | HIGH | Complete router |
| Slingshot_Contract.md | ✅ Partial | ✅ Yes | HIGH | Add projectile spawn |
| Aggregate_Resources.md | ❌ Missing | ✅ Yes | HIGH | Create full system |
| Storehouse_API.md | ✅ Exists | ✅ Yes | MEDIUM | Add API wrapper |
| VillagerTruth.md | ✅ Exists | ✅ Yes | DONE | Already matches |
| VillagerState.md | ✅ Exists | ✅ Yes | MEDIUM | Add interrupt component |
| Villagers_Jobs.md | ✅ Exists | ✅ Yes | DONE | Already implemented |
| TimeTruth.md | ✅ Exists | ✅ Yes | MEDIUM | Add input bindings |
| Miracles/* | ❌ Missing | ❌ Unknown | DEFER | Design first |
| Bands/* | ❌ Missing | ❌ Unknown | DEFER | Design first |

---

## Porting Status by Category

### Core Contracts (Reference Only)
- `generaltruth.md` - ℹ️ Game overview, keep as reference
- `Readbefore.md` - ℹ️ Assembly rules (not applicable to DOTS packages)
- `Terminology_Glossary.md` - ✅ Use terms in DOTS code
- `Coding_Standards_for_Agents.md` - 🔄 Adapt to DOTS standards

### Interaction (HIGH PRIORITY) 🔥
- `Hand_StateMachine.md` → ✅ Partially ported to `Interaction/Slingshot_*.md`
- `RMBtruthsource.md` → ✅ Ported to `Interaction/RMB_Priority.md`
- `Slingshot_Contract.md` → ✅ Ported to `Interaction/Slingshot_Charge_Mechanic.md`
- `Interaction_Priority.md` → ✅ Ported (priority constants)
- `Input_Actions.md` → <NEEDS PORT>

### Resources (HIGH PRIORITY) 🔥
- `Aggregate_Resources.md` → ✅ Ported to `Resources/Aggregate_Piles.md`
- `Storehouse_API.md` → 🔄 Need API wrapper concept doc

### Villagers (MOSTLY DONE) ✅
- `VillagerTruth.md` → ✅ Already implemented in PureDOTS
- `VillagerState.md` → ✅ Already implemented
- `Villagers_Jobs.md` → ✅ Already implemented

### Time (MOSTLY DONE) ✅
- `TimeTruth.md` → ✅ PureDOTS TimeState exists
- `TimeEngine_Contract.md` → ✅ PureDOTS handles
- `Input_TimeControls.md` → <NEEDS PORT> (input bindings)
- `Timeline_DataModel.md` → ℹ️ Reference only
- `TimeDeterminism.md` → ℹ️ DOTS is deterministic
- `Testing_Time.md` → ℹ️ Test framework reference

### Miracles (NEEDS DESIGN) 🔶
- No specific miracle docs in legacy
- Reference: `generaltruth.md` lists miracle types
- Status: Created `Miracles/Miracle_System_Vision.md` with questions

### Utility (REFERENCE) ℹ️
- `Layers_Tags_Physics.md` → <NEEDS REVIEW> for DOTS physics
- `Prefabs_Scene_Conventions.md` → 🔄 Adapt for SubScenes
- `ScriptExecutionOrder.md` → 🔄 DOTS system ordering
- `Unitytips.md` → ℹ️ General reference
- `UX_Microcopy.md` → ℹ️ UI text reference

### Advanced (FUTURE) 📋
- `Events_Bus.md` → ❌ Use telemetry buffers instead
- `FeatureFlags.md` → <NEEDS PORT>
- `Rewindable_Systems.md` → ✅ PureDOTS handles
- `SaveSchema_v1.md` → 📋 Future
- `Snapshot_Schema.md` → ✅ PureDOTS handles
- `TimeOfDay.md` → 📋 Future (environment system)

---

## Template Selection Guide

### Use **Feature Template** for:
- Individual miracles (Fire, Water, Heal)
- Specific buildings (Temple, Storehouse, House)
- Discrete mechanics (Slingshot, Pickup, Drop)

### Use **Mechanic Template** for:
- How systems work (RMB routing, charge curves, pathfinding)
- Algorithms (formation maintenance, morale calculation)
- Input handling (gesture recognition, time controls)

### Use **System Template** for:
- Interconnected gameplay (Prayer economy, construction, combat)
- Large-scale simulation (weather, vegetation, economy)
- Multi-component flows (villager jobs, resource chains)

### Use **Experience Template** for:
- Emotional moments (first miracle, village saved, defeat)
- Tutorial beats (learning hand, discovering power)
- Narrative peaks (boss encounters, victories)

---

## Common Translation Issues

### Issue 1: Visual Components
**Legacy:** Direct GameObject children for visuals  
**DOTS:** Hybrid rendering or companion GameObjects  
**Solution:** `<NEEDS SPEC: Visual strategy?>` flag, defer to implementation

### Issue 2: Events and Callbacks
**Legacy:** C# events, delegates, UnityEvents  
**DOTS:** DynamicBuffers for event streams, tags, reactive queries  
**Solution:** Map events to buffer elements or enableable components

### Issue 3: Services and Singletons
**Legacy:** Static classes, service locators  
**DOTS:** Entity singletons accessed via `SystemAPI.GetSingleton<T>()`  
**Solution:** Replace service calls with singleton queries

### Issue 4: Physics and Raycasts
**Legacy:** Unity Physics, OnTriggerEnter callbacks  
**DOTS:** Unity Physics (DOTS version) or hybrid colliders  
**Solution:** `<NEEDS SPEC: Physics strategy?>` - requires Unity Physics package

### Issue 5: Managed Collections
**Legacy:** List<T>, Dictionary<K,V>  
**DOTS:** NativeArray, NativeList, NativeHashMap  
**Solution:** Replace with native containers in system code

---

## Quick Port Template

```markdown
# [Feature Name] (Legacy Port)

**Status:** Draft - <WIP: Porting from legacy>  
**Legacy Source:** `Docs/Concepts/legacy/[filename].md`  
**Created:** [Date]

**⚠️ CURRENT STATE:**
- Legacy: [What legacy had]
- DOTS: [What we have now]
- Gap: [What's missing]

**⚠️ BLOCKERS:**
- [Dependency 1] <UNDEFINED>
- [Dependency 2] <NEEDS SPEC>

---

## Legacy Intent

[Copy key sections from legacy doc - Purpose, Contracts, etc.]

---

## DOTS Translation

**Legacy Components:**
```
[MonoBehaviour code]
```

**DOTS Equivalent:**
```csharp
[IComponentData structs]
<WIP: Fields pending approval>
```

**Implementation Status:**
- ✅ [What exists]
- 🟡 [What's partial]
- ❌ [What's missing]

---

## Open Questions

1. [Critical question]
2. [Design decision needed]

---

**NEXT STEP:** [Specific action item]
```

---

## Porting Priorities (Recommended Order)

### Week 1: High-Value, Low-Dependency
1. ✅ **DONE:** Hand state machine basics
2. ✅ **DONE:** RMB priority routing
3. ✅ **DONE:** Slingshot mechanics (partial)
4. **TODO:** Aggregate piles (full implementation)
5. **TODO:** Storehouse API wrapper

### Week 2: Input & Controls
6. **TODO:** Input actions comprehensive port
7. **TODO:** Time input controls
8. **TODO:** Camera controls refinement

### Week 3: Villager Systems (If Needed)
9. ✅ **DONE:** Villager jobs (already in DOTS)
10. ✅ **DONE:** Villager state machine (already in DOTS)
11. **TODO:** Villager interrupt handling

### Week 4-5: Complex Systems (Design First!)
12. **TODO:** Miracle system (DESIGN FIRST!)
13. **TODO:** Bands/combat (DESIGN FIRST!)
14. **TODO:** Construction system (DESIGN FIRST!)

### Week 6: Polish & Reference
15. **TODO:** UX microcopy port
16. **TODO:** Testing patterns
17. **TODO:** Feature flags system

---

## Success Metrics

**Porting is successful when:**
- [ ] All high-value legacy contracts have DOTS concept docs
- [ ] All concept docs have truth source mappings
- [ ] All blockers/questions clearly flagged
- [ ] No assumptions about unimplemented systems
- [ ] Implementation paths clear (what to build, what to defer)

---

**Next Step:** Systematically port each legacy doc using this guide!


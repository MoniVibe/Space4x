# Legacy Truth Sources - DOTS Porting Status

**Last Updated:** 2025-10-31  
**Total Legacy Docs:** 30  
**Ported:** 9  
**In Progress:** 0  
**Pending:** 21

---

## Porting Status

### ✅ Ported (9)

| Legacy Doc | DOTS Concept | Template | Status | Notes |
|------------|--------------|----------|--------|-------|
| `Hand_StateMachine.md` | `Interaction/Slingshot_Throw.md` | Feature | ✅ Done | Partial impl found |
| `RMBtruthsource.md` | `Interaction/RMB_Priority.md` | Mechanic | ✅ Done | Priority system |
| `Slingshot_Contract.md` | `Interaction/Slingshot_Charge_Mechanic.md` | Mechanic | ✅ Done | Charge mechanics |
| `Aggregate_Resources.md` | `Resources/Aggregate_Piles.md` | System | ✅ Done | Pile system |
| `VillagerTruth.md` | N/A | N/A | ✅ Skip | Already in DOTS |
| `VillagerState.md` | N/A | N/A | ✅ Skip | Already in DOTS |
| `Villagers_Jobs.md` | N/A | N/A | ✅ Skip | Already in DOTS |
| `Storehouse_API.md` | `Buildings/Storehouse_API.md` | Mechanic | ✅ Done | API wrapper concept |
| `Input_TimeControls.md` | `UI_UX/Time_Controls_Input.md` | Mechanic | ✅ Done | Input bindings |

### 🔄 In Progress (0)

*None currently*

### 📋 High Priority Pending (6)

| Legacy Doc | Target Concept | Blocker | Owner |
|------------|----------------|---------|-------|
| `Storehouse_API.md` | `Buildings/Storehouse_API.md` | None | Ready |
| `Input_Actions.md` | `Interaction/Input_System.md` | None | Ready |
| `Input_TimeControls.md` | `UI_UX/Time_Controls.md` | None | Ready |
| `Layers_Tags_Physics.md` | `Meta/DOTS_Layers_Physics.md` | None | Ready |
| `Prefabs_Scene_Conventions.md` | `Meta/SubScene_Conventions.md` | None | Ready |
| `ScriptExecutionOrder.md` | `Meta/System_Update_Order.md` | None | Ready |

### 🔶 Medium Priority Pending (8)

| Legacy Doc | Target Concept | Blocker | Owner |
|------------|----------------|---------|-------|
| `Cameraimplement.md` | `UI_UX/Camera_Feel.md` | Verify current impl | Ready |
| `TimeOfDay.md` | `World/Day_Night_Cycle.md` | Environment system | Defer |
| `FeatureFlags.md` | `Meta/Feature_Toggles.md` | None | Ready |
| `UX_Microcopy.md` | `UI_UX/Microcopy_Guide.md` | None | Ready |
| `Terminology_Glossary.md` | `Meta/Terminology.md` | None | Ready |
| `Testing_Time.md` | `Meta/Testing_Framework.md` | None | Ready |
| `Unitytips.md` | `Meta/DOTS_Best_Practices.md` | None | Ready |
| `Coding_Standards_for_Agents.md` | Update `AGENTS.md` | None | Ready |

### ⏸️ Low Priority / Reference (9)

| Legacy Doc | Action | Reason |
|------------|--------|--------|
| `TimeTruth.md` | ℹ️ Reference | PureDOTS TimeState exists |
| `TimeEngine_Contract.md` | ℹ️ Reference | PureDOTS handles |
| `Timeline_DataModel.md` | ℹ️ Reference | PureDOTS snapshot system |
| `TimeDeterminism.md` | ℹ️ Reference | DOTS is deterministic by design |
| `Rewindable_Systems.md` | ℹ️ Reference | PureDOTS continuity |
| `Snapshot_Schema.md` | ℹ️ Reference | PureDOTS handles |
| `SaveSchema_v1.md` | 📋 Future | Post-MVP feature |
| `Events_Bus.md` | ❌ Skip | Use telemetry buffers instead |
| `Readbefore.md` | ❌ Skip | Assembly rules not applicable |

---

## Porting Queue (Recommended Order)

### Batch 1: Interaction Polish (Next Up) 🎯
1. **Storehouse_API.md** → Create API wrapper concept
2. **Input_Actions.md** → Document full input system
3. **Layers_Tags_Physics.md** → DOTS physics layers

### Batch 2: Meta/Framework (Quick Wins) 📝
4. **Prefabs_Scene_Conventions.md** → SubScene best practices
5. **ScriptExecutionOrder.md** → System update order
6. **Terminology_Glossary.md** → DOTS terminology
7. **FeatureFlags.md** → Feature toggle system

### Batch 3: UX & Polish (Medium) 🎨
8. **Input_TimeControls.md** → Time control bindings
9. **UX_Microcopy.md** → UI text guide
10. **Cameraimplement.md** → Camera feel refinement

### Batch 4: Testing & Quality (Important) ✅
11. **Testing_Time.md** → Test framework setup
12. **Unitytips.md** → DOTS best practices
13. **Coding_Standards_for_Agents.md** → Update AGENTS.md

### Batch 5: Future Systems (Post-MVP) 🔮
14. **TimeOfDay.md** → Day/night cycle (needs environment)
15. **SaveSchema_v1.md** → Save system (post-launch)

---

## Port Completion Criteria

A legacy doc is "fully ported" when:
- ✅ DOTS concept doc created
- ✅ Truth sources checked and mapped
- ✅ All `<UNDEFINED>` dependencies flagged
- ✅ Open questions listed
- ✅ Implementation path clear OR marked as blocked
- ✅ Linked in concept README
- ✅ Cross-referenced in related docs

---

## Template Usage Stats

| Template | Times Used | Fit Quality |
|----------|------------|-------------|
| Feature | 2 | ✅ Good (Slingshot Throw, individual miracles) |
| Mechanic | 3 | ✅ Excellent (RMB Priority, Slingshot Charge, Aggregate Piles) |
| System | 3 | ✅ Good (Prayer Power, Needs Construction, Bands) |
| Experience | 1 | ✅ Good (First Miracle) |

**Finding:** Mechanic template most versatile for legacy ports!

---

## Next Actions

1. **Port Batch 1** (Storehouse API, Input Actions, Layers/Physics) - ~2 hours
2. **Review ported docs** - Check for over-assumptions
3. **Update truth sources inventory** - Add missing components from legacy
4. **Create design decision log** - Track all `<CLARIFICATION NEEDED>` items

---

**For Humans:** Use this tracker to see porting progress. Focus Batch 1 next (high value, no blockers).

**For AI Agents:** Follow porting workflow in `LEGACY_PORTING_GUIDE.md`. Check this status doc before porting to avoid duplicates.


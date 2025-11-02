# Legacy → DOTS Porting Summary

**Status:** Active - Game Design Conceptualization Phase  
**Created:** 2025-10-31  
**Legacy Source:** `Docs/Concepts/legacy/` (30 docs)  
**Target:** DOTS concepts in `Docs/Concepts/[Category]/`

**⚠️ CURRENT PHASE: DESIGN ONLY**
- Focus: What the game should be, not how to build it
- No code implementation yet
- Fleshing out ideas before committing to code
- Truth source checking helps avoid impossible designs

---

## Quick Stats

```
Legacy Docs: 30 total
├── ✅ Ported: 9 (30%)
├── 🔄 In Progress: 0
├── 📋 Pending: 21 (70%)
└── ❌ Skip: Included in ported count

Current Concepts: 11 total
├── From legacy: 9
├── New (DOTS-specific): 2
└── Categories covered: 7/11
```

---

## Infrastructure Created

### 1. **Porting Guide** (`LEGACY_PORTING_GUIDE.md`)
- Step-by-step workflow
- Translation patterns (MonoBehaviour → DOTS)
- Template selection guide
- Common issues and solutions

### 2. **Port Status Tracker** (`LEGACY_PORT_STATUS.md`)
- Complete status table
- Priority matrix
- Blocking dependencies
- Assignment ready queue

### 3. **WIP Flags System** (`WIP_FLAGS.md`)
- Standard uncertainty markers
- `<WIP>`, `<NEEDS SPEC>`, `<UNDEFINED>`, etc.
- Usage examples
- Quality checklist

---

## Porting Progress by Category

### Interaction ✅ (90% Done)
- ✅ Hand State Machine → Slingshot concepts
- ✅ RMB Priority → Priority routing
- ✅ Slingshot → Charge mechanics
- 📋 Input Actions (pending)

### Resources ✅ (100% for MVP)
- ✅ Aggregate Piles → Pile system
- ✅ Storehouse API → API wrapper

### Buildings 🟡 (50%)
- ✅ Storehouse API
- 📋 Construction system (needs design)

### Villagers ✅ (Done - Already in DOTS)
- ✅ Truth, State, Jobs already implemented
- Skip porting, truth sources exist

### Time ✅ (80%)
- ✅ Truth sources exist (PureDOTS)
- ✅ Input controls concept created
- 📋 Time HUD (pending)

### Miracles 🟡 (Design Phase)
- ✅ Vision doc created with questions
- ⏸️ Blocked on design decisions

### Combat ⏸️ (Deferred)
- Bands vision doc created
- Blocked on combat system scope decision

### UI/UX 🟡 (Started)
- ✅ Time Controls
- 📋 Camera, HUD, Microcopy pending

### Meta 📋 (Pending)
- High-value docs ready to port
- No blockers

---

## Key Findings

### What Works Well ✅
1. **Mechanic template** perfect for legacy contracts (clear, structured)
2. **WIP flags** prevent over-assumption
3. **Truth source checking** reveals actual state
4. **Honesty format** creates actionable design docs

### Surprises 🎉
1. **Slingshot already 60% implemented** - charge logic exists!
2. **Storehouse fully functional** - just needs API wrapper
3. **Villager systems already match legacy** - good DOTS design
4. **Time system complete** - PureDOTS handles everything

### Gaps Identified ⚠️
1. **Miracle system:** Design decisions needed before porting
2. **Combat/Bands:** Scope unclear, defer until decided
3. **Visual feedback:** Many mechanics missing VFX/SFX
4. **Event systems:** Need DOTS-native event pattern

---

## Next Porting Batch (Ready to Port)

### Batch 1: High-Value, No Blockers (Recommended Next)
1. **Input_Actions.md** → Full input system documentation
2. **Layers_Tags_Physics.md** → DOTS physics setup
3. **Prefabs_Scene_Conventions.md** → SubScene best practices
4. **ScriptExecutionOrder.md** → System update ordering
5. **Terminology_Glossary.md** → DOTS terminology guide

**Estimated Time:** 2-3 hours  
**Value:** High (framework docs)  
**Blockers:** None

### Batch 2: UX Polish
6. **Cameraimplement.md** → Camera feel refinement
7. **UX_Microcopy.md** → UI text guide
8. **FeatureFlags.md** → Toggle system

**Estimated Time:** 1-2 hours  
**Value:** Medium (polish)  
**Blockers:** None

### Batch 3: Testing & Quality
9. **Testing_Time.md** → Test framework
10. **Unitytips.md** → DOTS best practices
11. **Coding_Standards_for_Agents.md** → Update AGENTS.md

**Estimated Time:** 1-2 hours  
**Value:** Medium (quality)  
**Blockers:** None

---

## Design Decisions Blocking Ports

### Critical Decisions
1. **Miracle system:** Resource model (prayer vs cooldown)
2. **Combat scope:** In MVP or post-launch?
3. **Alignment system:** Good/evil mechanics or skip?
4. **Construction:** Player-placed or auto-generated?

### Important Decisions
5. **Visual strategy:** Hybrid rendering or pure DOTS?
6. **Event system:** Buffers, tags, or reactive queries?
7. **Physics:** Unity Physics (DOTS) or hybrid colliders?

**Recommendation:** Schedule design workshop to answer 1-4 before continuing ports

---

## Quality Metrics

### Port Quality Checklist
- ✅ Truth sources verified (9/9 ported docs)
- ✅ WIP flags used (9/9 docs)
- ✅ Open questions listed (9/9 docs)
- ✅ Legacy source linked (9/9 docs)
- ✅ Implementation path clear (9/9 docs)

**Average WIP flags per doc:** 4-8 (good - shows honesty!)

---

## Repository Structure

```
Docs/Concepts/
├── legacy/                      ← Original 30 legacy docs
├── _Templates/                  ← 4 DOTS templates
├── Core/                        ← 1 concept
├── Villagers/                   ← 1 concept (Bands)
├── Resources/                   ← 1 concept
├── Buildings/                   ← 2 concepts
├── Interaction/                 ← 3 concepts
├── Miracles/                    ← 1 concept
├── Experiences/                 ← 1 concept
├── UI_UX/                       ← 1 concept
│
├── README.md                    ← Main index
├── QUICK_START.md               ← 5-min guide
├── WIP_FLAGS.md                 ← Flag standards
├── LEGACY_PORTING_GUIDE.md      ← How to port
├── LEGACY_PORT_STATUS.md        ← What's ported
└── PORTING_SUMMARY.md          ← This file
```

---

## Success Criteria

**Phase 1 Complete When:**
- [x] Infrastructure created (guides, trackers, templates)
- [x] WIP flag system established
- [x] Example ports demonstrate pattern (9 done)
- [ ] High-priority ports complete (Batch 1)
- [ ] Design decisions documented
- [ ] Truth sources inventory updated

**Phase 2 Complete When:**
- [ ] All non-blocked legacy docs ported
- [ ] All ported concepts have truth source mappings
- [ ] Design decision log tracks blockers
- [ ] Concept dashboard shows full coverage

---

## For Humans 👤

**Current State:** Porting infrastructure complete, 30% of legacy ported  
**Next Action:** Port Batch 1 (5 framework docs, ~2-3 hours)  
**Blocker:** None for next batch  
**Decision Needed:** Miracle/combat/alignment system scope (blocks ~6 docs)

---

## For AI Agents 🤖

**Instructions:**
1. Read `LEGACY_PORTING_GUIDE.md` for workflow
2. Check `LEGACY_PORT_STATUS.md` for queue
3. Pick next doc from "High Priority Pending"
4. Follow porting checklist
5. Use WIP flags liberally
6. Update status tracker when done

**Pattern:** See `Storehouse_API.md` or `Time_Controls_Input.md` for examples

---

**Last Updated:** 2025-10-31  
**Progress:** On track, systematic approach working well


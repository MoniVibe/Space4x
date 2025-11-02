# Godgame Conceptualization Repository

**Purpose:** Living collection of game design ideas, mechanics, and features for the Godgame project.

This repository captures **what the game should be** while the Truth Sources documents define **how it's implemented**.

---

## 📁 Repository Structure

```
Docs/Concepts/
├── README.md                          # This file - index and usage guide
├── _Templates/                        # Design document templates
│   ├── Feature.md                     # Feature concept template
│   ├── Mechanic.md                    # Game mechanic template
│   ├── System.md                      # System design template
│   └── Experience.md                  # Player experience template
│
├── Core/                              # Core game concepts
│   ├── GodHand.md                     # Divine hand interaction
│   ├── Prayer_Power.md                # Prayer power economy
│   └── Influence_Ring.md              # Sphere of influence
│
├── Villagers/                         # Villager-related concepts
│   ├── Villager_Lifecycle.md         # Birth, aging, death
│   ├── Villager_Needs.md             # Food, shelter, happiness
│   ├── Villager_Professions.md       # Job types and progression
│   └── Villager_Personality.md       # AI personality traits
│
├── Resources/                         # Resource concepts
│   ├── Resource_Types.md             # Wood, ore, food, etc.
│   ├── Resource_Chains.md            # Production chains
│   └── Resource_Balance.md           # Economy balancing
│
├── Buildings/                         # Building concepts
│   ├── Building_Types.md             # Town center, temple, houses, etc.
│   ├── Building_Progression.md       # Upgrade paths
│   └── Building_Placement.md         # Placement rules and aesthetics
│
├── Miracles/                          # Miracle concepts
│   ├── Miracle_Types.md              # Fire, water, heal, etc.
│   ├── Miracle_Costs.md              # Prayer power costs
│   ├── Epic_Miracles.md              # Earthquake, volcano, etc.
│   └── Miracle_Gestures.md           # Hand gesture design
│
├── Combat/                            # Combat concepts
│   ├── Unit_Types.md                 # Swordsmen, archers, etc.
│   ├── Formations.md                 # Band formations
│   ├── Siege_Warfare.md              # Walls and siege mechanics
│   └── Combat_Balance.md             # Damage, health, rock-paper-scissors
│
├── Creature/                          # Creature concepts (future)
│   ├── Creature_Species.md           # Ape, cow, tiger, etc.
│   ├── Creature_Learning.md          # AI learning mechanics
│   ├── Creature_Miracles.md          # Creature casting miracles
│   └── Creature_Personality.md       # Good/evil alignment
│
├── World/                             # World simulation concepts
│   ├── Climate_Weather.md            # Weather systems
│   ├── Vegetation_Growth.md          # Plant lifecycle
│   ├── Day_Night_Cycle.md            # Time of day
│   └── Seasons.md                    # Seasonal changes
│
├── Progression/                       # Player progression concepts
│   ├── Tribute_System.md             # Unlocking buildings
│   ├── Alignment_System.md           # Good vs evil
│   ├── Impressiveness.md             # City attractiveness
│   └── Victory_Conditions.md         # How to win
│
├── UI_UX/                             # Interface concepts
│   ├── Camera_Controls.md            # Camera modes and feel
│   ├── Hand_Feedback.md              # Visual feedback for hand
│   ├── HUD_Design.md                 # Minimal HUD philosophy
│   └── Cursor_States.md              # Context-sensitive cursors
│
└── Meta/                              # Meta-game concepts
    ├── Tutorial_Flow.md              # Teaching the player
    ├── Save_System.md                # Saving and loading
    ├── Difficulty_Scaling.md         # Challenge progression
    └── Accessibility.md              # Accessibility features
```

---

## 📝 Document Types

### 🎯 Feature Concept
**When to use:** Defining a specific game feature  
**Template:** `_Templates/Feature.md`  
**Examples:** Prayer power, creature learning, miracle gestures

**⚠️ Use WIP flags liberally!** See `Docs/Concepts/WIP_FLAGS.md` for flag types.

### ⚙️ Mechanic Concept
**When to use:** Defining how a system works  
**Template:** `_Templates/Mechanic.md`  
**Examples:** Resource gathering, building placement, combat resolution

### 🏗️ System Concept
**When to use:** Defining interconnected gameplay systems  
**Template:** `_Templates/System.md`  
**Examples:** Economy, alignment, progression

### 🎮 Experience Concept
**When to use:** Defining player feelings and moments  
**Template:** `_Templates/Experience.md`  
**Examples:** First miracle, city under siege, creature bond

---

## 🔄 Relationship to Truth Sources

```
CONCEPTUALIZATION                    TRUTH SOURCES
(What & Why)                         (How)
─────────────────────────────────────────────────────────────
"Villagers should feel alive       → VillagerNeeds component
 with personality and needs"         VillagerMood component
                                      VillagerPersonality (future)

"Divine hand should feel           → Hand component
 powerful and responsive"            InputState component
                                      HandHistory for gestures

"Resources should flow              → StorehouseInventory
 naturally through economy"          AggregatePile (future)
                                      ResourceChains (future)
```

**Flow:**
1. **Concept** - Capture idea in Concepts/
2. **Design** - Refine into specific mechanics
3. **Contract** - Define as Truth Source contract
4. **Implement** - Build DOTS components/systems
5. **Test** - Validate against concept goals
6. **Iterate** - Update concept based on playtesting

---

## ✍️ How to Add a Concept

### Quick Add (Simple Ideas)
1. Choose appropriate category folder
2. Create markdown file with descriptive name
3. Use free-form text or bullet points
4. Tag with `Status: Draft` at top

### Formal Add (Detailed Design)
1. Copy relevant template from `_Templates/`
2. Fill in all sections
3. Link to related concepts
4. Tag with `Status: In Development`

### Example: Adding a new miracle concept

```bash
# Copy template
cp Docs/Concepts/_Templates/Feature.md Docs/Concepts/Miracles/Tornado_Miracle.md

# Edit file, fill in:
# - Name: Tornado
# - Category: Miracle - Offensive
# - Summary: Swirling vortex that picks up units and objects
# - Inspiration: Black & White 2 tornado + real tornado physics
# - Gameplay Goals: Area denial, scatter enemy formations
# - Costs: 5000 prayer power, 30s cooldown
# - Visual/Audio: Swirling wind particles, howling sound
# - Implementation Notes: Physics force field, ragdoll units
# - Open Questions: Should it destroy buildings?
```

---

## 🏷️ Status Tags

Use at the top of concept documents:

- `Status: Draft` - Initial brain dump, not refined
- `Status: In Review` - Ready for discussion/feedback
- `Status: Approved` - Greenlit for implementation
- `Status: In Development` - Currently being implemented
- `Status: Implemented` - Live in game
- `Status: On Hold` - Deferred for later
- `Status: Archived` - Deprecated/cut from scope

---

## 🎨 Design Principles (Reference)

These should guide all concepts:

### Core Pillars
1. **God Fantasy** - Feel powerful, omnipresent, worshipped
2. **Emergent Stories** - Systems create unique narratives
3. **Meaningful Choice** - Good vs evil, strategy vs chaos
4. **Tactile Interaction** - Direct manipulation feels satisfying
5. **Living World** - Villagers/creatures feel alive and reactive

### Constraints
1. **Performance** - Must run 1000+ villagers smoothly
2. **Scope** - MVP first, creature/multiplayer later
3. **Clarity** - Near-HUD-less UI, visual feedback over text
4. **Accessibility** - Readable UI, colorblind support
5. **Moddability** - Data-driven design for future modding

---

## 📊 Concept Status Dashboard

*Updated manually as concepts are added*

| Category | Draft | In Review | Approved | Implemented | Total |
|----------|-------|-----------|----------|-------------|-------|
| Core | 1 | 0 | 0 | 0 | 1 |
| Villagers | 2 | 0 | 0 | 0 | 2 |
| Resources | 0 | 0 | 1 | 0 | 1 |
| Buildings | 1 | 0 | 1 | 0 | 2 |
| Interaction | 0 | 0 | 3 | 0 | 3 |
| Experiences | 0 | 0 | 1 | 0 | 1 |
| Miracles | 1 | 0 | 0 | 0 | 1 |
| Combat | 0 | 0 | 0 | 0 | 0 |
| Creature | 0 | 0 | 0 | 0 | 0 |
| World | 1 | 0 | 0 | 0 | 1 |
| Progression | 1 | 0 | 0 | 0 | 1 |
| UI/UX | 0 | 0 | 1 | 0 | 1 |
| Meta | 2 | 0 | 0 | 0 | 2 |

---

## 🔗 Related Documentation

- **Truth Sources** - `Docs/TruthSources_Inventory.md` - Technical implementation
- **Architecture** - `Docs/TruthSources_Architecture.md` - DOTS patterns  
- **Legacy Salvage** - `Docs/Legacy_TruthSources_Salvage.md` - Preserved contracts
- **Legacy Porting Guide** - `Docs/Concepts/LEGACY_PORTING_GUIDE.md` - How to port
- **Legacy Port Status** - `Docs/Concepts/LEGACY_PORT_STATUS.md` - Progress tracker
- **Porting Summary** - `Docs/Concepts/PORTING_SUMMARY.md` - Overview
- **WIP Flags** - `Docs/Concepts/WIP_FLAGS.md` - Uncertainty markers
- **Integration TODO** - `Docs/TODO/Godgame_PureDOTS_Integration_TODO.md` - Work tracking

---

## 💡 Tips for Writing Good Concepts

### DO ✅
- **Be specific** - "Swordsmen deal 15 damage" not "strong units"
- **Use WIP flags** - Mark uncertain sections `<WIP>`, `<NEEDS SPEC>`, etc.
- **Check truth sources** - Reference what exists ✅ vs what's needed ❌
- **Ask questions** - Use `<CLARIFICATION NEEDED:>` for design decisions
- **Link concepts** - How does this interact with other systems?

### DON'T ❌
- **Assume systems exist** - Check truth sources first!
- **State specifics as facts** - Use `<FOR REVIEW>` if uncertain
- **Design in isolation** - Consider impact on other systems
- **Over-commit** - It's okay to have multiple options marked `<WIP>`
- **Ignore existing code** - Check what's implemented before designing

---

## 📞 For AI Agents

When adding concepts:
1. **Read related concepts first** to avoid duplication
2. **Use templates** for consistent structure
3. **Tag appropriately** with status
4. **Link bidirectionally** (update related docs)
5. **Update dashboard** in this README
6. **Cross-reference** truth sources where applicable

When implementing concepts:
1. **Check concept status** (only implement Approved)
2. **Create truth source** contract first
3. **Link truth source** back to concept doc
4. **Update concept status** to "In Development" → "Implemented"
5. **Document deviations** if implementation differs from concept

---

## 🚀 Quick Start: Your First Concept

```bash
# 1. Navigate to concepts
cd Docs/Concepts

# 2. Choose category (e.g., Miracles)
cd Miracles

# 3. Create file
# Name format: PascalCase_With_Underscores.md
touch Heal_Miracle.md

# 4. Add header
echo "# Heal Miracle" > Heal_Miracle.md
echo "" >> Heal_Miracle.md
echo "**Status:** Draft" >> Heal_Miracle.md
echo "**Category:** Miracle - Support" >> Heal_Miracle.md
echo "**Created:** $(date +%Y-%m-%d)" >> Heal_Miracle.md

# 5. Edit and fill in details
# 6. Commit when ready for review
```

---

**Last Updated:** 2025-10-31  
**Maintainer:** Godgame Development Team  
**Total Concepts:** 0 (Initialize with your ideas!)

---

**Remember:** This repository is for **dreaming big**. Truth sources are for **building real**. Concepts can be wild, ambitious, and experimental. Implementation will ground them in reality.

**⚠️ CURRENT PHASE:** Pure conceptualization - fleshing out game ideas, NOT implementing code yet. Feel free to explore wild ideas, ask big questions, and iterate on design without worrying about technical constraints.


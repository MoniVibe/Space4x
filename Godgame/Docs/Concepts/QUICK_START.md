# Conceptualization Repository - Quick Start

**5-Minute Guide to Adding Your First Game Concept**

---

## What is This?

A structured place to capture game design ideas before implementing them. Think of it as your **game design notebook** that:
- ✅ Captures "what the game should be" (not "how to build it")
- ✅ Organizes ideas by category (Villagers, Miracles, Combat, etc.)
- ✅ Uses templates for consistency
- ✅ Links to implementation docs (Truth Sources)

---

## When to Add a Concept

**Add a concept when you think:**
- "Wouldn't it be cool if..."
- "The player should feel..."
- "This mechanic needs..."
- "What if we had a system where..."

**Don't overthink it!** Start with a simple markdown file. You can always refine later.

---

## Quick Add (3 steps)

### 1. Pick a Category

Navigate to the right folder:
```
Docs/Concepts/
├── Core/          ← God hand, prayer power, influence
├── Villagers/     ← Professions, needs, personality
├── Resources/     ← Wood, ore, economy
├── Buildings/     ← Houses, temples, walls
├── Miracles/      ← Fire, water, earthquake
├── Combat/        ← Units, formations, siege
├── Creature/      ← (Future) AI pet/companion
├── World/         ← Weather, vegetation, day/night
├── Progression/   ← Unlocks, alignment, victory
├── UI_UX/         ← Camera, feedback, HUD
└── Meta/          ← Tutorial, saves, accessibility
```

### 2. Create a File

Name it descriptively with underscores:
```
Tornado_Miracle.md
Villager_Aging.md
Storehouse_UI.md
```

### 3. Start Writing

**Minimum viable concept:**
```markdown
# Tornado Miracle

**Status:** Draft  
**Category:** Miracle - Offensive  
**Created:** 2025-10-31

## Summary
A swirling vortex that picks up units and scatters them. Inspired by Black & White 2.

## Core Mechanic
- Player draws a circle gesture
- Tornado spawns, moves forward slowly
- Picks up units/objects in path, tosses them around
- Lasts 10 seconds
- Cost: 5000 prayer power

## Why This is Cool
- Spectacular visual (swirling debris)
- Tactical: breaks enemy formations
- Comedic: villagers flying through air

## Open Questions
- Should it destroy buildings?
- How fast should it move?
```

**That's it!** You've added a concept. Refine it later using templates.

---

## Using Templates (Optional)

For more detailed concepts, copy a template:

### Feature Template
Best for: Specific game features (miracles, buildings, mechanics)

```bash
# Copy template
cp Docs/Concepts/_Templates/Feature.md Docs/Concepts/Miracles/Tornado_Miracle.md

# Edit file, fill in sections
# Focus on: Summary, Core Mechanic, Player Experience Goals
```

### Mechanic Template
Best for: How systems work (gathering, combat resolution, pathfinding)

### System Template
Best for: Interconnected systems (economy, progression, alignment)

### Experience Template
Best for: Player emotions and moments (first miracle cast, village under siege)

---

## Example: Prayer Power System

See `Docs/Concepts/Core/Prayer_Power.md` for a full example of a system concept including:
- Economy formulas
- Generation rates
- Miracle costs
- Balancing strategies
- Iteration plan (MVP → Enhanced → Complete)

---

## Linking to Implementation

When a concept is approved and ready to implement:

1. **Create Truth Source contract** in `Docs/TruthSources_Inventory.md`
2. **Link concept → truth source** at bottom of concept doc
3. **Update concept status** to "In Development"
4. **Build DOTS components** based on truth source
5. **Update status** to "Implemented" when live

**Example link in concept doc:**
```markdown
## Implementation Notes

**Truth Sources Required:**
- `Docs/TruthSources_Inventory.md#miracles`

**Components Needed:**
- `PrayerPowerPool` (singleton)
- `PrayerGenerator` (per villager)
- `MiracleCost` (per miracle)
```

---

## Status Tags

At the top of each concept, use:

- `Status: Draft` - Just an idea, not refined
- `Status: In Review` - Ready for feedback
- `Status: Approved` - Greenlit for implementation
- `Status: In Development` - Currently being built
- `Status: Implemented` - Live in the game
- `Status: On Hold` - Maybe later
- `Status: Archived` - Cut from scope

---

## Tips for Good Concepts

### DO ✅
- **Be specific:** "Swordsmen deal 15 damage" not "strong units"
- **Include examples:** Concrete scenarios help
- **State goals:** WHY does this feature exist?
- **Reference inspirations:** "Like X in Game Y"
- **Consider interactions:** How does this affect other systems?

### DON'T ❌
- **Don't write code:** Focus on "what" not "how"
- **Don't overspecify:** Leave implementation flexibility
- **Don't write novels:** Be concise, use bullets
- **Don't design in isolation:** Think about the whole game

---

## Common Workflows

### Brainstorm Session
1. Create multiple draft concepts quickly
2. Don't worry about templates or details
3. Tag all as `Status: Draft`
4. Review together later, pick favorites
5. Refine promising ones with templates

### Feature Planning
1. Start with high-level system concept
2. Break down into feature concepts
3. Detail mechanics
4. Design player experiences
5. Prioritize for implementation

### Playtest Feedback
1. Capture issues as concepts
2. Tag `Status: In Review`
3. Discuss solutions
4. Update existing concepts or create new ones
5. Tag approved fixes `Status: Approved`

---

## Next Steps

**Right now:**
1. Think of one game idea you're excited about
2. Navigate to the right category folder
3. Create a markdown file
4. Write 3-5 sentences about it
5. Done!

**As you iterate:**
- Refine with templates
- Link related concepts
- Update status tags
- Cross-reference truth sources
- Keep dashboard updated

---

## Questions?

Check the full README: `Docs/Concepts/README.md`

Or just start writing! Concepts are meant to be **living documents** that evolve with the game.

---

**Remember:** There are no bad concepts, only unrefined ones. Write it down, refine it later!


# Guild Curriculum System - Quick Reference

**Concept**: Guilds teach thematic collections of knowledge regardless of school/category. A "Guild of Wrath" teaches all offensive abilities from any source.

## Core Principle

```
Traditional (School-Based):
  Fire Guild     → Only fire spells
  Combat Guild   → Only combat techniques
  Craft Guild    → Only crafting recipes

Thematic (New System):
  Guild of Wrath → All offensive (fire spells, combat, dark magic)
  Aegis Order    → All defensive (shields, healing, holy magic)
  Merchants      → All trade (negotiation, appraisal, transport)
```

## Key Components

### 1. Theme Tags (on Lessons/Spells)

```csharp
KnowledgeThemeTags:
  Themes:         GuildTheme.Offensive | GuildTheme.FireFocus
  Purpose:        AbilityPurpose.DealDamage
  DamageType:     DamageType.Fire
  PrimarySchool:  SpellSchool.Evocation
```

### 2. Guild Curriculum (on Guild Entities)

```csharp
GuildCurriculum:
  PrimaryTheme:   Offensive | FireFocus
  SecondaryTheme: MonsterHunting
  TeachingEfficiency: 1.2
  AcceptsPublicStudents: false
```

### 3. Curriculum Entries (Auto-Generated Buffer)

```csharp
GuildCurriculumEntry[]:
  - "fireball" (Relevance: 100%, Rank: 0)
  - "weapon_mastery" (Relevance: 95%, Rank: 1)
  - "berserker_rage" (Relevance: 100%, Rank: 0)
  - "wrath_incarnate" (Relevance: 100%, Rank: 2, Signature)
```

### 4. Member Bonuses (by Rank)

```csharp
GuildSpecializationBonus:
  Member:   +15% effectiveness
  Officer:  +25% effectiveness, +10% speed
  Master:   +40% effectiveness, +20% speed, +15% crit, -15% cost
```

## Example Guilds

| Guild | Teaches | From Schools | Theme |
|-------|---------|--------------|-------|
| **Guild of Wrath** | All offensive abilities | Fire magic, Combat, Dark magic | Maximum damage |
| **Aegis Order** | All defensive abilities | Holy magic, Shields, Healing | Total protection |
| **Shadow Blades** | Stealth + assassination | Poison, Illusion, Combat | Unseen death |
| **Arcane Society** | Pure magic research | ALL magic schools | Understanding magic |
| **Merchants' League** | Trade + diplomacy | Social, Exploration, Utility | Wealth generation |
| **Master Smiths** | All metalworking | Weapon, Armor, Enchanting | Legendary crafting |

## Theme Flags Reference

### Combat
- `Offensive` - Damage dealing
- `Defensive` - Protection/mitigation
- `Stealth` - Hide/assassinate
- `MonsterHunting` - Boss combat
- `Tactical` - Coordination

### Magic
- `Elemental` - Fire/ice/lightning/earth
- `Necromancy` - Death/undead
- `Divine` - Holy/light
- `Arcane` - Pure magic theory
- `Temporal` - Time manipulation
- `Illusion` - Deception

### Craft
- `Smithing` - Metalwork
- `Alchemy` - Potions/chemicals
- `Enchanting` - Item enhancement
- `Construction` - Building
- `Cooking` - Food preparation

### Knowledge
- `Trade` - Commerce
- `Healing` - Medicine
- `Survival` - Nature/wilderness
- `Exploration` - Discovery
- `ForbiddenKnowledge` - Dark lore

### Social
- `Diplomacy` - Negotiation
- `Espionage` - Spying
- `Performance` - Arts/entertainment
- `Leadership` - Command/morale

## Usage Pattern

### 1. Tag Your Content

```csharp
// Fireball Spell
Themes: Offensive | Elemental | FireFocus
Purpose: DealDamage
DamageType: Fire

// Backstab Technique
Themes: Stealth | Offensive
Purpose: DealDamage
```

### 2. Define Guild

```csharp
// Guild of Wrath
PrimaryTheme: Offensive | FireFocus | DarkFocus
SecondaryTheme: MonsterHunting
```

### 3. System Auto-Matches

```
Guild of Wrath curriculum:
  ✓ Fireball (Fire + Offensive = 100% match)
  ✓ Shadow Bolt (Dark + Offensive = 100% match)
  ✓ Backstab (Offensive = 80% match)
  ✓ Berserker Rage (Offensive = 100% match)
  ✗ Heal (no match)
  ✗ Lockpicking (no match)
```

### 4. Members Learn & Get Bonuses

```
Villager joins Guild of Wrath as Member:
  → Gains +15% damage with offensive abilities
  → Can learn Fireball, Shadow Bolt, Backstab
  → Cannot learn Heal or Lockpicking (wrong theme)

Villager promoted to Officer:
  → Gains +25% damage, +10% speed
  → Unlocks advanced curriculum

Villager promoted to Master:
  → Gains +40% damage, +20% speed, +15% crit
  → Unlocks signature techniques ("Wrath Incarnate")
```

## Files Created

1. **[GuildCurriculumSystem.md](GuildCurriculumSystem.md)** - Full design specification
2. **[GuildCurriculumComponents.cs](../../Packages/com.moni.puredots/Runtime/Runtime/Aggregates/GuildCurriculumComponents.cs)** - Component definitions
3. **[GuildCurriculumExamples.md](GuildCurriculumExamples.md)** - 7 example guilds with full specs
4. **[GuildCurriculumAuthoring.md](GuildCurriculumAuthoring.md)** - How to create guilds in Unity
5. **[GuildCurriculumIntegration.md](GuildCurriculumIntegration.md)** - Integration with existing systems

## Benefits

✅ **Thematic Coherence**: Guilds reflect philosophy, not just profession
✅ **Cross-School Learning**: Fire from any school if it's offensive
✅ **Automatic Curation**: Tag once, guilds auto-include
✅ **Player Choice**: Join different guilds for different approaches
✅ **Emergent Gameplay**: Guild rivalries = philosophical conflicts
✅ **Easy Extension**: Add new themes/guilds without reconfiguring content

## Next Steps for Implementation

1. **Tag existing lessons/spells** with theme metadata
2. **Create GuildProfileAuthoring assets** for each guild type
3. **Implement GuildCurriculumBuilder system** to auto-generate entries
4. **Implement GuildTeachingSystem** for member instruction
5. **Implement GuildBonusApplicationSystem** to apply rank bonuses
6. **Create editor preview tools** to visualize curriculum
7. **Test with game projects** (Godgame, Space4X)

## Philosophy Examples

### Guild of Wrath
*"The best defense is overwhelming offense. We don't dodge, we don't block - we destroy before we can be destroyed."*

Teaches: All damage abilities (fire spells, combat skills, dark magic)

### Aegis Order
*"We are the bulwark against darkness. Our shields protect the innocent, our healing sustains the righteous."*

Teaches: All defensive abilities (shields, healing, holy magic)

### Shadow Blades
*"We are the unseen hand. No lock can keep us out, no shadow hide our prey."*

Teaches: Stealth + assassination (invisibility, poison, backstab)

### Arcane Society
*"Magic is not fire or ice, light or dark - it is the fundamental force of reality."*

Teaches: Pure magic research from ALL schools

### Merchants' League
*"Gold opens all doors. We are the lifeblood of civilization."*

Teaches: Trade, negotiation, appraisal, route finding

---

**This system captures game concepts through thematic organization rather than rigid categorization.**

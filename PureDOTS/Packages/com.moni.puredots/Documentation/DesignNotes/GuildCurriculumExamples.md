# Guild Curriculum Examples

This document provides concrete examples of guilds using the thematic curriculum system. Each guild teaches a mix of abilities from different schools and categories based on conceptual themes.

## Combat Guilds

### Guild of Wrath (Offensive Specialists)

**Theme**: Pure offense - maximum damage output regardless of method

```
Guild Configuration:
- Type: Heroes
- PrimaryTheme: Offensive | FireFocus | DarkFocus
- SecondaryTheme: MonsterHunting | Tactical
- TeachingEfficiency: 1.2
- AdvancedRankRequirement: 3 (Officer)
- AcceptsPublicStudents: false
- HasSignatureTechniques: true

Specialization Bonuses (Member Rank):
- SpeedBonus: 1.0
- EffectivenessBonus: 1.15 (+15% damage)
- CriticalBonus: 0.05 (+5% crit chance)

Specialization Bonuses (Officer Rank):
- SpeedBonus: 1.1
- EffectivenessBonus: 1.25 (+25% damage)
- CriticalBonus: 0.10 (+10% crit chance)

Specialization Bonuses (Master Rank):
- SpeedBonus: 1.2
- EffectivenessBonus: 1.40 (+40% damage)
- CriticalBonus: 0.15 (+15% crit chance)
- CostReduction: 0.15 (-15% resource cost)
```

**Curriculum** (auto-generated from theme filters):

**Combat Lessons**:
- "Berserker Rage" (Novice) - Damage boost at HP cost
- "Weapon Mastery: Greatswords" (Apprentice) - Two-handed weapon expertise
- "Armor Penetration" (Journeyman) - Bypass enemy defenses
- "Execute" (Expert) - Finish weakened enemies
- "Bloodlust" (Master) - Damage increases with consecutive kills
- **SIGNATURE**: "Wrath Incarnate" (Master) - Transform into pure fury for 30s

**Fire Magic** (from any school):
- "Fireball" (Novice) - Basic fire projectile
- "Flame Strike" (Apprentice) - Melee fire damage
- "Immolation" (Journeyman) - Self-immolation aura
- "Meteor" (Expert) - Massive AoE fire
- **SIGNATURE**: "Infernal Wrath" (Master) - Permanent fire aura that grows with kills

**Dark Magic** (from any school):
- "Shadow Bolt" (Novice) - Dark projectile
- "Fear" (Apprentice) - Terrorize enemies
- "Life Drain" (Journeyman) - Steal HP
- "Curse of Weakness" (Expert) - Reduce enemy damage
- "Shadow Step" (Expert) - Teleport through shadows to enemies
- **SIGNATURE**: "Devouring Darkness" (Master) - Convert kills into shadow minions

**Hybrid Offensive**:
- "Explosive Rage" (Expert) - Fire + Combat combo
- "Vengeance Strike" (Expert) - Dark + Combat combo
- "Hellfire Blade" (Master) - Weapon imbue with fire+dark

**Monster Hunting**:
- "Weak Point Detection" (Apprentice) - Identify boss vulnerabilities
- "Giant Slaying" (Journeyman) - Bonus damage vs large creatures
- "Boss Trophy Collection" (Expert) - Harvest rare materials from bosses

**Philosophy**: "The best defense is overwhelming offense. We don't dodge, we don't block - we destroy before we can be destroyed. Fire purifies, darkness consumes, and steel ends all arguments."

---

### Aegis Order (Defensive Specialists)

**Theme**: Total protection - shields, healing, damage mitigation

```
Guild Configuration:
- Type: HolyOrder
- PrimaryTheme: Defensive | Divine | Healing
- SecondaryTheme: Protection
- TeachingEfficiency: 1.0
- AdvancedRankRequirement: 2
- AcceptsPublicStudents: true
- PublicLessonCost: 100 gold
- RequiredEnlightenment: 30

Specialization Bonuses (Member):
- EffectivenessBonus: 1.2 (+20% shield/heal strength)
- CostReduction: 0.1 (-10% mana cost)

Specialization Bonuses (Officer):
- EffectivenessBonus: 1.35 (+35% shield/heal strength)
- CostReduction: 0.15 (-15% mana cost)
- SpeedBonus: 1.1 (faster cast for protection)

Specialization Bonuses (Master):
- EffectivenessBonus: 1.5 (+50% shield/heal strength)
- CostReduction: 0.25 (-25% mana cost)
- SpeedBonus: 1.2
```

**Curriculum**:

**Shield Techniques**:
- "Shield Bash" (Novice) - Defensive counter-attack
- "Shield Wall" (Apprentice) - Reduce incoming damage
- "Guardian Stance" (Journeyman) - Intercept attacks on allies
- "Unbreakable" (Expert) - Temporary invulnerability
- **SIGNATURE**: "Aegis of the Faithful" (Master) - Party-wide damage immunity for 10s

**Holy Magic**:
- "Lesser Heal" (Novice)
- "Smite" (Novice) - Holy damage (for evil entities)
- "Blessing" (Apprentice) - Defense buff
- "Holy Ward" (Journeyman) - Damage absorption shield
- "Greater Heal" (Expert)
- "Resurrection" (Master)
- **SIGNATURE**: "Divine Intervention" (Master) - Prevent death once per day

**Protective Crafting**:
- "Armorsmithing" (Apprentice)
- "Shield Crafting" (Apprentice)
- "Armor Reinforcement" (Journeyman)
- "Blessed Armor Creation" (Expert) - Holy resistance

**Support Magic** (from any school):
- "Damage Reduction Aura" (Journeyman)
- "Regeneration" (Expert)
- "Crowd Protection" (Expert) - AoE damage reduction
- "Sanctuary" (Master) - Create safe zone

**Philosophy**: "We are the bulwark against darkness. Our shields protect the innocent, our healing sustains the righteous, and our light banishes evil. We stand between harm and humanity."

---

### Shadow Blades (Stealth & Assassination)

**Theme**: Unseen death - stealth, poison, critical strikes

```
Guild Configuration:
- Type: Assassins
- PrimaryTheme: Stealth | Offensive | PoisonFocus
- SecondaryTheme: Illusion | Espionage
- TeachingEfficiency: 0.9 (requires practice, not just theory)
- AdvancedRankRequirement: 2
- AcceptsPublicStudents: false
- HasSignatureTechniques: true
- RequiredEnlightenment: 0 (accepts any)

Specialization Bonuses (Member):
- CriticalBonus: 0.15 (+15% crit from stealth)
- SpeedBonus: 1.1 (faster movement)

Specialization Bonuses (Officer):
- CriticalBonus: 0.25 (+25% crit from stealth)
- SpeedBonus: 1.2
- EffectivenessBonus: 1.3 (+30% backstab damage)

Specialization Bonuses (Master):
- CriticalBonus: 0.40 (+40% crit from stealth)
- SpeedBonus: 1.3
- EffectivenessBonus: 1.6 (+60% backstab damage)
- CostReduction: 0.2
```

**Curriculum**:

**Stealth Techniques**:
- "Sneak" (Novice)
- "Hide in Shadows" (Apprentice)
- "Silent Movement" (Journeyman)
- "Invisibility" (Expert)
- "Shadow Meld" (Master) - Become untargetable
- **SIGNATURE**: "Death's Whisper" (Master) - Instant kill from stealth on targets <50% HP

**Assassination**:
- "Backstab" (Novice) - Critical from behind
- "Vital Strike" (Apprentice) - Target weak points
- "Throat Cut" (Journeyman) - Bleed + silence
- "Assassinate" (Expert) - Massive damage from stealth
- "Death Mark" (Expert) - Mark target for tracking + bonus damage

**Poison Crafting & Application**:
- "Basic Poison Crafting" (Novice)
- "Weapon Coating" (Apprentice)
- "Paralytic Toxin" (Journeyman)
- "Deadly Poison" (Expert)
- "Toxin Immunity" (Expert)
- **SIGNATURE**: "Shadowbane Venom" (Master) - Corrupting poison that spreads to nearby enemies

**Illusion Magic**:
- "Distraction" (Novice)
- "Disguise" (Apprentice)
- "Phantasmal Decoy" (Journeyman)
- "Mass Invisibility" (Expert)

**Espionage**:
- "Lockpicking" (Novice)
- "Trap Detection" (Apprentice)
- "Forgery" (Journeyman)
- "Interrogation" (Expert)
- "Code Breaking" (Expert)

**Philosophy**: "We are the unseen hand. No lock can keep us out, no shadow hide our prey. Death comes on silent feet."

---

## Magic Guilds

### Arcane Society (Pure Magic Research)

**Theme**: Understanding magic itself - all schools, all techniques

```
Guild Configuration:
- Type: Scholars
- PrimaryTheme: Arcane | Elemental | Temporal
- SecondaryTheme: Transmutation | Divination
- TeachingEfficiency: 0.8 (complex material)
- AdvancedRankRequirement: 4 (requires Mastery)
- AcceptsPublicStudents: false
- RequiredEnlightenment: 50
- HasSignatureTechniques: true

Specialization Bonuses (Member):
- LearningSpeedBonus: 1.3 (+30% spell research speed)
- CostReduction: 0.1

Specialization Bonuses (Officer):
- LearningSpeedBonus: 1.5
- CostReduction: 0.2
- EffectivenessBonus: 1.15

Specialization Bonuses (Master):
- LearningSpeedBonus: 2.0 (+100% spell research speed)
- CostReduction: 0.3
- EffectivenessBonus: 1.3
- SpeedBonus: 1.2
```

**Curriculum**:

**Spell Theory**:
- "Mana Manipulation" (Novice)
- "Spell Formula Analysis" (Apprentice)
- "Hybrid Spell Creation" (Journeyman)
- "School Founding" (Expert)
- "Metamagic" (Master) - Modify spell properties on the fly
- **SIGNATURE**: "Arcane Mastery" (Master) - Cast any spell from any school at -50% cost

**Elemental Mastery** (all elements from all schools):
- Fire spells (Novice to Master)
- Ice spells (Novice to Master)
- Lightning spells (Novice to Master)
- Earth spells (Novice to Master)
- "Elemental Fusion" (Expert) - Combine elements
- **SIGNATURE**: "Prismatic Cascade" (Master) - Cast all 4 elements simultaneously

**Time Magic**:
- "Haste" (Apprentice)
- "Slow" (Apprentice)
- "Time Stop" (Master)
- **SIGNATURE**: "Temporal Echo" (Master) - Duplicate yourself from past/future

**Transmutation**:
- "Lesser Transmutation" (Journeyman)
- "Polymorph" (Expert)
- "Stone to Flesh" (Expert)
- "Mass Transmutation" (Master)

**Ritual Magic**:
- "Basic Rituals" (Apprentice)
- "Power Circle Creation" (Journeyman)
- "Grand Ritual Casting" (Master)
- **SIGNATURE**: "Arcane Apotheosis" (Master) - Temporarily become pure magic

**Philosophy**: "Magic is not fire or ice, light or dark - it is the fundamental force of reality. We study all schools because all schools are one."

---

### Elementalists' Conclave (Elemental Specialists)

**Theme**: Master the raw forces of nature

```
Guild Configuration:
- Type: Mages
- PrimaryTheme: Elemental | FireFocus | IceFocus | LightningFocus
- SecondaryTheme: Offensive | Defensive
- TeachingEfficiency: 1.1
- AdvancedRankRequirement: 2
- AcceptsPublicStudents: true
- PublicLessonCost: 75 gold

Specialization Bonuses (Member):
- EffectivenessBonus: 1.2 (+20% elemental damage)
- ElementalResistance: +20%

Specialization Bonuses (Officer):
- EffectivenessBonus: 1.35
- CostReduction: 0.15
- ElementalResistance: +35%

Specialization Bonuses (Master):
- EffectivenessBonus: 1.5
- CostReduction: 0.25
- ElementalResistance: +50%
- SpeedBonus: 1.15
```

**Curriculum**:

Members choose an elemental specialization, then can branch into others:

**Fire Path**:
- "Ember" (Novice)
- "Fireball" (Apprentice)
- "Fire Wall" (Journeyman)
- "Inferno" (Expert)
- "Summon Fire Elemental" (Master)

**Ice Path**:
- "Frost" (Novice)
- "Ice Spike" (Apprentice)
- "Frozen Prison" (Journeyman)
- "Blizzard" (Expert)
- "Summon Ice Elemental" (Master)

**Lightning Path**:
- "Shock" (Novice)
- "Lightning Bolt" (Apprentice)
- "Chain Lightning" (Journeyman)
- "Storm Call" (Expert)
- "Summon Lightning Elemental" (Master)

**Earth Path**:
- "Stone Fist" (Novice)
- "Earth Spike" (Apprentice)
- "Stone Armor" (Journeyman)
- "Earthquake" (Expert)
- "Summon Earth Elemental" (Master)

**Cross-Element** (requires Journeyman in 2+ elements):
- "Steam Blast" (Fire + Ice)
- "Molten Earth" (Fire + Earth)
- "Thunder Storm" (Lightning + Ice)
- "Lava Flow" (Fire + Earth)
- **SIGNATURE**: "Elemental Convergence" (Master) - Summon all 4 elementals

**Philosophy**: "The elements are the building blocks of the world. Master them, and you master reality itself."

---

## Trade & Craft Guilds

### Merchants' League (Commerce & Trade)

**Theme**: Wealth through trade, negotiation, and market mastery

```
Guild Configuration:
- Type: Merchants
- PrimaryTheme: Trade | Diplomacy | Exploration
- SecondaryTheme: Social | ResourceManagement
- TeachingEfficiency: 1.0
- AdvancedRankRequirement: 2
- AcceptsPublicStudents: true
- PublicLessonCost: 50 gold

Specialization Bonuses (Member):
- +15% buy/sell prices
- +10% travel speed on trade routes

Specialization Bonuses (Officer):
- +25% buy/sell prices
- +20% travel speed
- +10% gold income from all sources

Specialization Bonuses (Master):
- +40% buy/sell prices
- +30% travel speed
- +25% gold income
- -20% caravan operating costs
```

**Curriculum**:

**Trading Skills**:
- "Appraisal" (Novice) - Identify item value
- "Negotiation" (Apprentice) - Better prices
- "Market Analysis" (Journeyman) - Predict price trends
- "Contract Law" (Expert) - Create binding agreements
- "Currency Exchange" (Expert) - Trade foreign currencies
- **SIGNATURE**: "Golden Touch" (Master) - Everything you sell is worth +50% for 1 hour

**Caravan Management**:
- "Route Planning" (Apprentice)
- "Guard Hiring" (Journeyman)
- "Supply Management" (Journeyman)
- "Trade Empire" (Master) - Manage multiple caravans

**Diplomacy**:
- "Persuasion" (Novice)
- "Cultural Knowledge" (Apprentice)
- "Bribery" (Journeyman)
- "Alliance Formation" (Expert)

**Utility Magic** (trade-focused):
- "Identify" (Novice) - Detect magic items
- "Feather Fall" (Apprentice) - Protect cargo
- "Dimension Door" (Expert) - Emergency escape
- "Teleportation Circle" (Master) - Long-distance trade

**Philosophy**: "Gold opens all doors. We are the lifeblood of civilization, connecting producers to consumers across the world."

---

### Master Smiths (All Metalworking)

**Theme**: Forge the finest equipment in any metal

```
Guild Configuration:
- Type: Artisans
- PrimaryTheme: Smithing | Enchanting
- SecondaryTheme: Craft | Mining
- TeachingEfficiency: 1.0
- AdvancedRankRequirement: 3
- AcceptsPublicStudents: true
- PublicLessonCost: 100 gold

Specialization Bonuses (Member):
- +20% crafting quality
- -10% material waste

Specialization Bonuses (Officer):
- +35% crafting quality
- -20% material waste
- +15% crafting speed

Specialization Bonuses (Master):
- +60% crafting quality
- -30% material waste
- +30% crafting speed
- Can craft Legendary items
```

**Curriculum**:

**Weapon Smithing**:
- "Basic Weapon Forging" (Novice)
- "Advanced Weapon Forging" (Apprentice)
- "Masterwork Weapons" (Journeyman)
- "Exotic Weapon Crafting" (Expert)
- "Legendary Weapon Forging" (Master)
- **SIGNATURE**: "Runeforging" (Master) - Inscribe permanent runes on weapons

**Armor Smithing**:
- "Basic Armor Forging" (Novice)
- "Advanced Armor Forging" (Apprentice)
- "Masterwork Armor" (Journeyman)
- "Exotic Armor Crafting" (Expert)
- "Legendary Armor Forging" (Master)

**Material Mastery**:
- "Iron Working" (Novice)
- "Steel Working" (Apprentice)
- "Mithril Working" (Journeyman)
- "Adamantine Working" (Expert)
- "Dragon Scale Working" (Master)

**Enchanting** (craft-focused):
- "Minor Enchantment" (Apprentice)
- "Weapon Enchanting" (Journeyman)
- "Armor Enchanting" (Journeyman)
- "Major Enchantment" (Expert)
- **SIGNATURE**: "Soulforging" (Master) - Bind souls into equipment for unique properties

**Mining**:
- "Ore Detection" (Novice)
- "Efficient Mining" (Apprentice)
- "Vein Tracking" (Journeyman)

**Philosophy**: "Every strike of the hammer is a note in the song of creation. We shape metal into legend."

---

## Summary Table

| Guild | Type | Primary Theme | Accepts Public | Signature Focus |
|-------|------|---------------|----------------|-----------------|
| Guild of Wrath | Heroes | Offensive/Fire/Dark | No | Maximum damage |
| Aegis Order | HolyOrder | Defensive/Divine/Healing | Yes | Total protection |
| Shadow Blades | Assassins | Stealth/Poison | No | Unseen death |
| Arcane Society | Scholars | Arcane/All Schools | No | Magic research |
| Elementalists | Mages | Elemental/All Elements | Yes | Elemental mastery |
| Merchants' League | Merchants | Trade/Diplomacy | Yes | Wealth generation |
| Master Smiths | Artisans | Smithing/Enchanting | Yes | Legendary crafting |

## Cross-Guild Scenarios

### Rival Philosophies
- Guild of Wrath vs Aegis Order: "Overwhelming offense" vs "Unbreakable defense"
- Shadow Blades vs Aegis Order: "Unseen assassination" vs "Protective light"
- Arcane Society vs Elementalists: "Pure theory" vs "Practical application"

### Complementary Guilds
- Guild of Wrath + Master Smiths: Warriors need weapons
- Shadow Blades + Merchants: Assassins for hire through intermediaries
- Aegis Order + Merchants: Caravan guards

### Teaching Exchanges
- Arcane Society teaches Elementalists: Advanced spell theory
- Shadow Blades teaches Guild of Wrath: Stealth approaches to combat
- Master Smiths teaches All Combat Guilds: Equipment maintenance

These examples show how guilds can teach cross-school, cross-category knowledge based on thematic coherence rather than rigid boundaries.

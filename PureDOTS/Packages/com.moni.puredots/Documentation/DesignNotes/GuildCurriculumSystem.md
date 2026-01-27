# Guild Curriculum & Thematic Teaching System

## Concept

Guilds are **thematic organizations** that teach knowledge based on **conceptual alignment** rather than strict school or category boundaries. A "Guild of Wrath" teaches all offensive techniques regardless of whether they're fire magic, combat skills, or dark rituals. A "Merchants' Guild" teaches trade, appraisal, negotiation, and efficient transport.

This creates:
- **Rich guild identity** beyond simple profession categories
- **Cross-domain expertise** where guilds specialize in themes spanning multiple schools
- **Player choice** in learning paths (join different guilds for different approaches)
- **Emergent gameplay** where guild rivalries reflect philosophical differences

## Guild Themes (Archetypes)

### Combat-Focused Guilds
- **Guild of Wrath**: All offensive abilities (fire spells, weapon mastery, berserker rage, poison crafting)
- **Aegis Order**: All defensive abilities (shield techniques, healing, ward spells, armor crafting)
- **Shadow Blades**: Stealth and assassination (invisibility, backstab, poison, lockpicking)
- **Titan Slayers**: Boss hunting and monster lore (trap crafting, weak point detection, coordination tactics)

### Knowledge & Magic Guilds
- **Arcane Society**: Pure magical research across all schools (spell creation, mana efficiency, ritual casting)
- **Elementalists**: Elemental mastery (fire, ice, lightning, earth - any school that uses elements)
- **Necromantic Order**: Death magic and necromancy (summoning undead, life drain, corpse crafting)
- **Celestial Guardians**: Holy and light magic (healing, smiting, divine shields, exorcism)
- **Chronomancers**: Time manipulation (haste, slow, temporal rewind, aging effects)

### Trade & Craft Guilds
- **Merchants' League**: Trade and commerce (appraisal, negotiation, route finding, market analysis)
- **Master Smiths**: All smithing disciplines (weapons, armor, tools, enchanted items)
- **Alchemists' Circle**: Potion and chemical craft (healing potions, bombs, transmutation, poison)
- **Architects' Guild**: Construction and engineering (building speed, structural integrity, siege weapons)
- **Enchanters' Consortium**: Item enhancement (weapon enchanting, armor buffing, artifact creation)

### Survival & Utility Guilds
- **Wilderness Rangers**: Survival and nature (foraging, tracking, animal handling, herb lore)
- **Scholars of the Veil**: Forbidden knowledge (curse crafting, demon summoning, dark rituals)
- **Explorer's Society**: Discovery and navigation (cartography, terrain reading, ruin identification)
- **Healers' Covenant**: All healing arts (medicine, surgery, cleansing, regeneration)

### Social & Political Guilds
- **Diplomats' Circle**: Negotiation and influence (persuasion, deception, cultural knowledge)
- **Spymasters' Network**: Intelligence gathering (surveillance, code breaking, interrogation)
- **Bardic College**: Performance and morale (music, storytelling, inspiration, crowd control)

## Technical Architecture

### Guild Curriculum Components

```csharp
/// <summary>
/// Defines what a guild teaches and how.
/// Guilds teach based on thematic filters rather than strict categories.
/// </summary>
public struct GuildCurriculum : IComponentData
{
    /// <summary>
    /// Thematic focus of the guild (Offensive, Defensive, Trade, etc.)
    /// Multiple themes can be combined using flags.
    /// </summary>
    public GuildTheme PrimaryTheme;
    public GuildTheme SecondaryTheme;

    /// <summary>
    /// Teaching efficiency modifier (1.0 = normal, higher = faster learning)
    /// </summary>
    public float TeachingEfficiency;

    /// <summary>
    /// Minimum rank required to access advanced curriculum
    /// </summary>
    public byte AdvancedRankRequirement;

    /// <summary>
    /// Whether guild accepts non-members for paid lessons
    /// </summary>
    public bool AcceptsPublicStudents;

    /// <summary>
    /// Gold cost per lesson hour for non-members
    /// </summary>
    public float PublicLessonCost;
}

/// <summary>
/// Guild theme flags - multiple can be combined
/// </summary>
[Flags]
public enum GuildTheme : uint
{
    None = 0,

    // Combat themes
    Offensive = 1 << 0,        // All damage-dealing abilities
    Defensive = 1 << 1,        // All defensive/protection abilities
    Stealth = 1 << 2,          // Stealth and assassination
    MonsterHunting = 1 << 3,   // Boss/monster specialized

    // Magic themes
    Elemental = 1 << 4,        // Fire/ice/lightning/earth
    Necromancy = 1 << 5,       // Death and undead
    Divine = 1 << 6,           // Holy/light magic
    Arcane = 1 << 7,           // Pure magical research
    Temporal = 1 << 8,         // Time manipulation
    Illusion = 1 << 9,         // Deception and illusions

    // Craft themes
    Smithing = 1 << 10,        // All metalworking
    Alchemy = 1 << 11,         // Potions and chemicals
    Enchanting = 1 << 12,      // Item enhancement
    Construction = 1 << 13,    // Building and engineering
    Cooking = 1 << 14,         // Food preparation

    // Knowledge themes
    Trade = 1 << 15,           // Commerce and markets
    Healing = 1 << 16,         // All healing arts
    Survival = 1 << 17,        // Nature and wilderness
    Exploration = 1 << 18,     // Discovery and mapping
    ForbiddenKnowledge = 1 << 19, // Dark/dangerous lore

    // Social themes
    Diplomacy = 1 << 20,       // Negotiation and influence
    Espionage = 1 << 21,       // Spying and intelligence
    Performance = 1 << 22,     // Arts and entertainment
    Leadership = 1 << 23,      // Command and coordination

    // Damage type themes
    FireFocus = 1 << 24,       // Fire damage specialists
    IceFocus = 1 << 25,        // Ice damage specialists
    LightningFocus = 1 << 26,  // Lightning specialists
    PoisonFocus = 1 << 27,     // Poison specialists
    HolyFocus = 1 << 28,       // Holy damage specialists
    DarkFocus = 1 << 29,       // Dark/shadow damage
}

/// <summary>
/// Lessons/spells that match guild curriculum filters.
/// Generated at runtime by analyzing lesson/spell tags.
/// </summary>
public struct GuildCurriculumEntry : IBufferElementData
{
    public enum EntryType : byte
    {
        Lesson,
        Spell,
        Recipe,
        Technique  // Combat maneuvers
    }

    public EntryType Type;

    /// <summary>
    /// ID of the lesson/spell/recipe
    /// </summary>
    public FixedString64Bytes EntryId;

    /// <summary>
    /// Minimum guild rank required to learn
    /// </summary>
    public byte MinimumRank;

    /// <summary>
    /// How well this entry matches guild theme (0-100)
    /// Higher = more central to guild identity
    /// </summary>
    public byte ThemeRelevance;

    /// <summary>
    /// Whether this is a signature technique (exclusive to this guild)
    /// </summary>
    public bool IsSignature;
}

/// <summary>
/// Member's progress through guild curriculum.
/// Tracks which lessons have been learned from this guild.
/// </summary>
public struct GuildStudentProgress : IBufferElementData
{
    public FixedString64Bytes LessonId;
    public MasteryTier CurrentTier;
    public float Progress;           // 0-1 toward next tier
    public uint StartedLearningTick;
    public uint LastPracticeTick;

    /// <summary>
    /// Which guild member taught this (for tracking lineages)
    /// </summary>
    public Entity TeacherEntity;
}

/// <summary>
/// Teaching session between guild member (teacher) and student.
/// </summary>
public struct GuildTeachingSession : IComponentData
{
    public Entity TeacherEntity;
    public Entity StudentEntity;
    public Entity GuildEntity;

    public FixedString64Bytes LessonId;

    public uint SessionStartTick;
    public uint SessionDuration;     // Ticks

    /// <summary>
    /// Teaching quality multiplier based on teacher mastery
    /// </summary>
    public float TeachingQuality;

    /// <summary>
    /// Whether student pays tuition
    /// </summary>
    public bool IsPaidLesson;
    public float TuitionPaid;
}

/// <summary>
/// Guild specialization bonuses applied to members.
/// These stack with individual mastery bonuses.
/// </summary>
public struct GuildSpecializationBonus : IComponentData
{
    /// <summary>
    /// Theme being specialized in
    /// </summary>
    public GuildTheme SpecializedTheme;

    /// <summary>
    /// Cast speed / action speed bonus (1.0 = normal, 1.2 = 20% faster)
    /// </summary>
    public float SpeedBonus;

    /// <summary>
    /// Effectiveness bonus for themed abilities (1.0 = normal, 1.3 = 30% stronger)
    /// </summary>
    public float EffectivenessBonus;

    /// <summary>
    /// Resource cost reduction (0.1 = 10% cheaper mana/stamina)
    /// </summary>
    public float CostReduction;

    /// <summary>
    /// Critical chance bonus for themed abilities (0.05 = +5%)
    /// </summary>
    public float CriticalBonus;
}
```

## Lesson/Spell Tagging System

To support thematic filtering, lessons and spells need theme tags:

```csharp
/// <summary>
/// Theme tags for a lesson/spell.
/// Used by guilds to determine curriculum membership.
/// </summary>
public struct KnowledgeThemeTags : IComponentData
{
    /// <summary>
    /// Primary theme flags
    /// </summary>
    public GuildTheme Themes;

    /// <summary>
    /// Damage type if applicable
    /// </summary>
    public DamageType DamageType;

    /// <summary>
    /// Purpose tags (what does this do?)
    /// </summary>
    public AbilityPurpose Purpose;

    /// <summary>
    /// Schools involved (for magic)
    /// </summary>
    public SpellSchool PrimarySchool;
    public SpellSchool SecondarySchool;
}

[Flags]
public enum AbilityPurpose : uint
{
    None = 0,
    DealDamage = 1 << 0,
    Heal = 1 << 1,
    Buff = 1 << 2,
    Debuff = 1 << 3,
    Summon = 1 << 4,
    Craft = 1 << 5,
    Harvest = 1 << 6,
    Transport = 1 << 7,
    Utility = 1 << 8,
    Social = 1 << 9,
    Stealth = 1 << 10,
    Detection = 1 << 11,
}
```

## Systems

### GuildCurriculumBuildSystem
- Runs once at startup/when guilds are created
- Scans all lessons/spells and matches them to guild themes
- Populates `GuildCurriculumEntry` buffers for each guild
- Calculates theme relevance scores

### GuildTeachingSystem
- Manages teaching sessions between members
- Applies guild bonuses to learning rate
- Handles tuition payments
- Tracks teacher-student lineages

### GuildBonusApplicationSystem
- Applies `GuildSpecializationBonus` to members based on rank
- Stacks with individual mastery bonuses
- Updates when member rank changes

### GuildCurriculumQuerySystem
- Handles queries like "what can I learn at my rank?"
- Generates recommendations based on member's current skills
- Filters curriculum based on prerequisites

## Example Guild Definitions

### Guild of Wrath
```
Name: "Guild of Wrath"
Type: GuildType.Heroes (combat-focused)
PrimaryTheme: Offensive
SecondaryTheme: FireFocus | DarkFocus
TeachingEfficiency: 1.2
AdvancedRankRequirement: 3 (Officer)
AcceptsPublicStudents: false (members only)

Curriculum includes:
- All combat lessons with AbilityPurpose.DealDamage
- All spells with GuildTheme.Offensive
- Fire/dark damage spells regardless of school
- Berserker rage techniques
- Weapon mastery (all types)
- Intimidation and fear effects

Bonuses:
- +20% damage with offensive abilities
- +10% critical chance
- -15% rage/fury costs
```

### Merchants' League
```
Name: "Merchants' League"
Type: GuildType.Merchants
PrimaryTheme: Trade
SecondaryTheme: Diplomacy | Exploration
TeachingEfficiency: 1.0
AdvancedRankRequirement: 2
AcceptsPublicStudents: true
PublicLessonCost: 50 gold per session

Curriculum includes:
- Appraisal lessons
- Negotiation and persuasion
- Route finding and navigation
- Market analysis
- Contract law knowledge
- Currency exchange techniques
- Caravan management

Bonuses:
- +25% trade prices
- -10% travel time on trade routes
- +15% negotiation success
```

### Arcane Society
```
Name: "Arcane Society"
Type: GuildType.Scholars
PrimaryTheme: Arcane
SecondaryTheme: Elemental | Temporal
TeachingEfficiency: 0.8 (complex material)
AdvancedRankRequirement: 4 (Master rank)
AcceptsPublicStudents: false
RequiresEnlightenment: 50

Curriculum includes:
- ALL spell creation lessons
- Mana efficiency techniques
- Ritual casting methods
- Spell hybridization
- Magical theory
- Elemental manipulation (all types)
- Time magic fundamentals

Bonuses:
- +30% spell research speed
- -20% spell casting costs
- +1 maximum spell hybrid combinations
```

## Integration Points

### With Existing Systems
- **LessonAcquisitionSystem**: Check if lesson is in guild curriculum before learning
- **SpellLearningSystem**: Guild members get bonuses when learning curriculum spells
- **GuildFormationSystem**: Initialize curriculum when guild is created
- **GuildKnowledge**: Extend with curriculum metrics (lessons taught, students trained)

### With Economy
- Tuition payments go to guild treasury
- Non-member lessons generate income
- Guild funding for research unlocks advanced curriculum

### With Reputation
- Teaching quality affects guild reputation
- Student success reflects on teacher and guild
- Guild wars can lock curriculum access

### With Quests
- "Learn X lessons from Guild of Wrath" quest objectives
- Guild master quests to unlock signature techniques
- Rival guild sabotage (destroy lesson scrolls, assassinate teachers)

## Authoring Workflow

1. **Define Guild Profile**:
   - Set theme flags
   - Configure teaching parameters
   - Define signature techniques

2. **Tag Lessons/Spells**:
   - Add theme tags to each lesson/spell definition
   - Set damage types and purposes
   - Mark exclusive content

3. **System Auto-Generates Curriculum**:
   - Matches tags to guild themes
   - Calculates relevance scores
   - Assigns rank requirements based on complexity

4. **Designer Override**:
   - Manually add/remove specific entries
   - Adjust rank requirements
   - Mark signature techniques

## Future Extensions

- **Guild Specialization Paths**: Members choose sub-focus within guild theme
- **Curriculum Evolution**: Guilds discover new techniques over time
- **Cross-Guild Learning**: Diplomatic ties allow curriculum sharing
- **Forbidden Techniques**: Certain lessons only taught in secret
- **Apprenticeship System**: Formal teacher-student contracts
- **Guild Schools**: Physical locations with libraries and training grounds
- **Curriculum Rivalry**: Competing guilds develop counter-techniques

# Guild Curriculum Authoring Workflow

This guide explains how to create and configure guilds with thematic curricula in the Unity Editor.

## Overview

The guild curriculum system uses a two-phase approach:

1. **Tag Phase**: Tag lessons, spells, and recipes with theme metadata
2. **Guild Phase**: Define guilds with theme filters that auto-match tagged content

This allows guilds to automatically include relevant content without manual curation.

## Phase 1: Tagging Content

### Tagging Lessons

In your lesson authoring assets, add theme tags:

```csharp
[CreateAssetMenu(menuName = "PureDOTS/Knowledge/Lesson Definition")]
public class LessonAuthoringAsset : ScriptableObject
{
    // Existing lesson fields
    public string LessonId;
    public string DisplayName;
    public LessonCategory Category;
    // ...

    // NEW: Theme tags
    [Header("Curriculum Tags")]
    public GuildTheme Themes;
    public GuildThemeExtended ExtendedThemes;
    public AbilityPurpose Purpose;
    public bool IsForbidden;
    public bool IsUtility;
}
```

**Example: "Berserker Rage" lesson**
```
LessonId: "berserker_rage"
DisplayName: "Berserker Rage"
Category: Combat

Themes: Offensive
Purpose: DealDamage | Buff
IsForbidden: false
```

**Example: "Shadow Bolt" spell**
```
SpellId: "shadow_bolt"
DisplayName: "Shadow Bolt"
School: Dark

Themes: Offensive | DarkFocus
Purpose: DealDamage
DamageType: Shadow
```

**Example: "Negotiation" lesson**
```
LessonId: "negotiation"
DisplayName: "Negotiation"
Category: Social

Themes: Trade | Diplomacy
Purpose: Social
```

### Tag Categories Reference

#### Combat Tags
```
Offensive - Deals damage or increases damage output
Defensive - Reduces damage taken or protects allies
Tactical - Battlefield positioning/coordination
MonsterHunting - Boss/elite combat bonuses
```

#### Magic Tags
```
Elemental - Fire/ice/lightning/earth
Necromancy - Death/undead
Divine - Holy/light
Arcane - Pure magic theory
Temporal - Time manipulation
Illusion - Deception/stealth magic
Transmutation - Shape changing
```

#### Craft Tags
```
Smithing - Metal working
Alchemy - Potion/chemical crafting
Enchanting - Magic item enhancement
Construction - Building
Cooking - Food preparation
Tailoring - Cloth/leather armor
```

#### Knowledge Tags
```
Trade - Commerce/markets
Healing - Medicine/restoration
Survival - Nature/wilderness
Exploration - Discovery/mapping
ForbiddenKnowledge - Dark/dangerous lore
History - Cultural knowledge
```

#### Social Tags
```
Diplomacy - Negotiation/influence
Espionage - Spying/intelligence
Performance - Arts/entertainment
Leadership - Command/morale
```

#### Purpose Tags (What does it do?)
```
DealDamage - Offensive combat
Heal - Restore HP/mana
Buff - Enhance allies
Debuff - Weaken enemies
Summon - Call creatures
Craft - Create items
Harvest - Gather resources
Transport - Move entities/items
Utility - General utility
Social - NPC interaction
Stealth - Hide/sneak
Detection - Reveal hidden
Control - Crowd control
Protection - Shields/mitigation
Mobility - Movement enhancement
```

### Auto-Tagging Helper

Create an editor tool to auto-tag based on heuristics:

```csharp
public static class LessonAutoTagger
{
    public static void AutoTagLesson(LessonAuthoringAsset lesson)
    {
        // Auto-tag based on category
        switch (lesson.Category)
        {
            case LessonCategory.Combat:
                // Check if offensive or defensive based on effects
                foreach (var effect in lesson.Effects)
                {
                    if (effect.Type == LessonEffectType.StatBonus &&
                        effect.TargetId.Contains("Damage"))
                    {
                        lesson.Themes |= GuildTheme.Offensive;
                        lesson.Purpose |= AbilityPurpose.DealDamage;
                    }
                    else if (effect.TargetId.Contains("Defense") ||
                             effect.TargetId.Contains("Armor"))
                    {
                        lesson.Themes |= GuildTheme.Defensive;
                        lesson.Purpose |= AbilityPurpose.Protection;
                    }
                }
                break;

            case LessonCategory.Crafting:
                if (lesson.DisplayName.Contains("Smith") ||
                    lesson.DisplayName.Contains("Forge"))
                {
                    lesson.Themes |= GuildTheme.Smithing;
                    lesson.Purpose |= AbilityPurpose.Craft;
                }
                else if (lesson.DisplayName.Contains("Potion") ||
                         lesson.DisplayName.Contains("Alchemy"))
                {
                    lesson.Themes |= GuildTheme.Alchemy;
                    lesson.Purpose |= AbilityPurpose.Craft;
                }
                break;

            case LessonCategory.Harvest:
                lesson.Themes |= GuildTheme.Survival;
                lesson.Purpose |= AbilityPurpose.Harvest;
                break;

            case LessonCategory.Magic:
                lesson.Themes |= GuildTheme.Arcane;
                // Determine element based on spell name
                if (lesson.DisplayName.Contains("Fire"))
                    lesson.Themes |= GuildTheme.FireFocus;
                // etc.
                break;

            case LessonCategory.Social:
                if (lesson.DisplayName.Contains("Trade") ||
                    lesson.DisplayName.Contains("Merchant"))
                {
                    lesson.Themes |= GuildTheme.Trade;
                    lesson.Purpose |= AbilityPurpose.Social;
                }
                else if (lesson.DisplayName.Contains("Diplomacy") ||
                         lesson.DisplayName.Contains("Negotiation"))
                {
                    lesson.Themes |= GuildTheme.Diplomacy;
                    lesson.Purpose |= AbilityPurpose.Social;
                }
                break;
        }
    }
}
```

## Phase 2: Defining Guilds

### Guild Profile Authoring

Create a `GuildProfileAuthoring` ScriptableObject:

```csharp
[CreateAssetMenu(menuName = "PureDOTS/Guilds/Guild Profile")]
public class GuildProfileAuthoring : ScriptableObject
{
    [Header("Identity")]
    public string GuildName;
    public Guild.GuildType Type;
    public string Description;

    [Header("Curriculum")]
    public GuildTheme PrimaryTheme;
    public GuildTheme SecondaryTheme;
    public float TeachingEfficiency = 1.0f;
    public byte AdvancedRankRequirement = 2;

    [Header("Access")]
    public bool AcceptsPublicStudents = false;
    public float PublicLessonCost = 100f;
    public byte RequiredEnlightenment = 0;
    public bool HasSignatureTechniques = false;

    [Header("Bonuses")]
    public GuildRankBonuses MemberBonuses;
    public GuildRankBonuses OfficerBonuses;
    public GuildRankBonuses MasterBonuses;

    [Header("Manual Curriculum Override")]
    public bool UseManualCurriculum = false;
    public List<CurriculumEntryOverride> ManualEntries;
}

[Serializable]
public struct GuildRankBonuses
{
    public float SpeedBonus;
    public float EffectivenessBonus;
    public float CostReduction;
    public float CriticalBonus;
    public float LearningSpeedBonus;
}

[Serializable]
public struct CurriculumEntryOverride
{
    public string EntryId;
    public GuildCurriculumEntry.EntryType Type;
    public byte MinimumRank;
    public byte ThemeRelevance;
    public bool IsSignature;
    public float TuitionCost;
}
```

### Example: Guild of Wrath Profile

```
GuildName: "Guild of Wrath"
Type: Heroes
Description: "Masters of destruction. We teach all offensive techniques regardless of school or method."

PrimaryTheme: Offensive | FireFocus | DarkFocus
SecondaryTheme: MonsterHunting | Tactical
TeachingEfficiency: 1.2
AdvancedRankRequirement: 3

AcceptsPublicStudents: false
HasSignatureTechniques: true

MemberBonuses:
  SpeedBonus: 1.0
  EffectivenessBonus: 1.15
  CostReduction: 0.0
  CriticalBonus: 0.05
  LearningSpeedBonus: 1.0

OfficerBonuses:
  SpeedBonus: 1.1
  EffectivenessBonus: 1.25
  CostReduction: 0.05
  CriticalBonus: 0.10
  LearningSpeedBonus: 1.1

MasterBonuses:
  SpeedBonus: 1.2
  EffectivenessBonus: 1.40
  CostReduction: 0.15
  CriticalBonus: 0.15
  LearningSpeedBonus: 1.2

UseManualCurriculum: false (auto-generate from theme tags)
```

### Example: Merchants' League Profile

```
GuildName: "Merchants' League"
Type: Merchants
Description: "Wealth through trade and negotiation. We connect the world through commerce."

PrimaryTheme: Trade | Diplomacy
SecondaryTheme: Exploration
TeachingEfficiency: 1.0
AdvancedRankRequirement: 2

AcceptsPublicStudents: true
PublicLessonCost: 50
RequiredEnlightenment: 0

MemberBonuses:
  EffectivenessBonus: 1.15 (better trade prices)
  LearningSpeedBonus: 1.0

OfficerBonuses:
  EffectivenessBonus: 1.25
  LearningSpeedBonus: 1.1
  SpeedBonus: 1.1 (faster travel)

MasterBonuses:
  EffectivenessBonus: 1.40
  LearningSpeedBonus: 1.2
  SpeedBonus: 1.2
  CostReduction: 0.2 (caravan costs)
```

## Phase 3: Curriculum Generation

### Automatic Curriculum Builder

At runtime (or in editor preview), the system builds curriculum:

```csharp
public class GuildCurriculumBuilder
{
    public void BuildCurriculum(Entity guildEntity, GuildCurriculum curriculum,
                                LessonCatalogRef lessonCatalog)
    {
        var buffer = EntityManager.GetBuffer<GuildCurriculumEntry>(guildEntity);
        buffer.Clear();

        // Scan all lessons
        foreach (var lesson in lessonCatalog.Blob.Value.Lessons)
        {
            // Get theme tags for this lesson
            if (!TryGetLessonThemeTags(lesson, out var tags))
                continue;

            // Check if lesson matches guild themes
            int relevance = CalculateThemeRelevance(tags, curriculum);

            if (relevance >= 20) // Minimum 20% match
            {
                buffer.Add(new GuildCurriculumEntry
                {
                    Type = GuildCurriculumEntry.EntryType.Lesson,
                    EntryId = lesson.LessonId,
                    MinimumRank = CalculateRankRequirement(lesson.Complexity),
                    ThemeRelevance = (byte)relevance,
                    IsSignature = false, // Set manually for signature techniques
                    RequiresTeacher = lesson.TeachingDifficulty > 0.7f,
                    TuitionCost = curriculum.PublicLessonCost * (1 + lesson.TeachingDifficulty)
                });
            }
        }

        // Sort by relevance (most relevant first)
        buffer.AsNativeArray().Sort(new CurriculumRelevanceComparer());
    }

    private int CalculateThemeRelevance(KnowledgeThemeTags tags, GuildCurriculum curriculum)
    {
        int score = 0;

        // Primary theme match = 100 points
        if ((tags.Themes & curriculum.PrimaryTheme) != 0)
            score += 100;

        // Secondary theme match = 50 points
        if ((tags.Themes & curriculum.SecondaryTheme) != 0)
            score += 50;

        // Purpose alignment = 25 points
        if (PurposeAligns(tags.Purpose, curriculum.PrimaryTheme))
            score += 25;

        // Clamp to 0-100
        return Math.Min(100, score);
    }

    private bool PurposeAligns(AbilityPurpose purpose, GuildTheme theme)
    {
        // Offensive theme wants damage-dealing
        if ((theme & GuildTheme.Offensive) != 0 &&
            (purpose & AbilityPurpose.DealDamage) != 0)
            return true;

        // Defensive theme wants protection/healing
        if ((theme & GuildTheme.Defensive) != 0 &&
            ((purpose & AbilityPurpose.Protection) != 0 ||
             (purpose & AbilityPurpose.Heal) != 0))
            return true;

        // Trade theme wants social/utility
        if ((theme & GuildTheme.Trade) != 0 &&
            (purpose & AbilityPurpose.Social) != 0)
            return true;

        // etc.
        return false;
    }
}
```

### Manual Overrides

For signature techniques or special cases:

```
In GuildProfileAuthoring:

UseManualCurriculum: true

ManualEntries:
  - EntryId: "wrath_incarnate"
    Type: Technique
    MinimumRank: 2 (Master)
    ThemeRelevance: 100
    IsSignature: true
    RequiresTeacher: true
    TuitionCost: 0 (members only)

  - EntryId: "infernal_wrath"
    Type: Spell
    MinimumRank: 2 (Master)
    ThemeRelevance: 100
    IsSignature: true
    RequiresTeacher: true
    TuitionCost: 0
```

## Phase 4: Editor Tools

### Curriculum Preview Tool

Create an editor window to preview generated curriculum:

```csharp
public class GuildCurriculumPreviewWindow : EditorWindow
{
    private GuildProfileAuthoring selectedGuild;

    void OnGUI()
    {
        selectedGuild = EditorGUILayout.ObjectField("Guild Profile",
            selectedGuild, typeof(GuildProfileAuthoring), false) as GuildProfileAuthoring;

        if (selectedGuild != null && GUILayout.Button("Generate Preview"))
        {
            var curriculum = GeneratePreviewCurriculum(selectedGuild);
            DisplayCurriculum(curriculum);
        }
    }

    private List<CurriculumPreviewEntry> GeneratePreviewCurriculum(GuildProfileAuthoring guild)
    {
        // Scan all lesson/spell assets in project
        var lessons = AssetDatabase.FindAssets("t:LessonAuthoringAsset");
        var entries = new List<CurriculumPreviewEntry>();

        foreach (var guid in lessons)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var lesson = AssetDatabase.LoadAssetAtPath<LessonAuthoringAsset>(path);

            int relevance = CalculateRelevance(lesson, guild);
            if (relevance >= 20)
            {
                entries.Add(new CurriculumPreviewEntry
                {
                    Name = lesson.DisplayName,
                    Id = lesson.LessonId,
                    Relevance = relevance,
                    Type = "Lesson"
                });
            }
        }

        return entries.OrderByDescending(e => e.Relevance).ToList();
    }

    private void DisplayCurriculum(List<CurriculumPreviewEntry> entries)
    {
        EditorGUILayout.LabelField($"Total Entries: {entries.Count}");
        EditorGUILayout.Space();

        foreach (var entry in entries)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(entry.Name, GUILayout.Width(200));
            EditorGUILayout.LabelField($"{entry.Relevance}%", GUILayout.Width(50));
            EditorGUILayout.LabelField(entry.Type, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }
    }
}
```

### Theme Validator

Tool to ensure lessons are properly tagged:

```csharp
public class LessonThemeValidator : EditorWindow
{
    void OnGUI()
    {
        if (GUILayout.Button("Validate All Lessons"))
        {
            ValidateAllLessonThemes();
        }
    }

    private void ValidateAllLessonThemes()
    {
        var lessons = AssetDatabase.FindAssets("t:LessonAuthoringAsset");
        int untagged = 0;

        foreach (var guid in lessons)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var lesson = AssetDatabase.LoadAssetAtPath<LessonAuthoringAsset>(path);

            if (lesson.Themes == GuildTheme.None &&
                lesson.Purpose == AbilityPurpose.None)
            {
                Debug.LogWarning($"Untagged lesson: {lesson.DisplayName} ({path})");
                untagged++;
            }
        }

        if (untagged == 0)
        {
            Debug.Log("All lessons are properly tagged!");
        }
        else
        {
            Debug.LogWarning($"{untagged} lessons need theme tags.");
        }
    }
}
```

## Best Practices

### Tagging Strategy
1. **Be generous with tags**: Better to over-tag than under-tag
2. **Use multiple tags**: Most abilities fit multiple themes
3. **Purpose is key**: Always set purpose flags
4. **Damage types matter**: Helps specialized guilds filter correctly

### Guild Design
1. **Start with 2-3 themes**: Don't try to cover everything
2. **Mix combat + utility**: Pure combat guilds are limiting
3. **Signature techniques**: 2-4 exclusive abilities per guild
4. **Teaching efficiency**: Reflect guild's educational focus

### Curriculum Management
1. **Auto-generate first**: Let the system match themes
2. **Manual override sparingly**: Only for signature techniques
3. **Preview before deploy**: Use editor tools to verify
4. **Balance across ranks**: Novice, Apprentice, Journeyman, Expert, Master should all have content

### Testing
1. **Create test guilds**: "All Offensive", "All Defensive", etc.
2. **Check curriculum counts**: Should have 20-50 entries typically
3. **Verify relevance scores**: 80-100 = core, 50-79 = relevant, 20-49 = tangential
4. **Test cross-school**: Fire spells in both Elementalists and Guild of Wrath

## Integration Checklist

- [ ] Tag all existing lessons with themes
- [ ] Tag all existing spells with themes
- [ ] Create guild profile assets
- [ ] Build curriculum generation system
- [ ] Test curriculum preview tool
- [ ] Validate theme coverage
- [ ] Create signature techniques
- [ ] Test teaching sessions
- [ ] Verify bonuses apply correctly
- [ ] Test public vs member-only lessons

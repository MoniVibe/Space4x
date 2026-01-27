# Guild Curriculum System Integration Guide

This document shows how the thematic guild curriculum system integrates with existing PureDOTS systems and game projects.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    KNOWLEDGE CONTENT                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐   │
│  │ Lessons  │  │  Spells  │  │ Recipes  │  │Techniques│   │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘   │
│       │             │              │             │          │
│       └─────────────┴──────────────┴─────────────┘          │
│                     │                                        │
│              ┌──────▼──────┐                                │
│              │ Theme Tags  │ (GuildTheme, Purpose, etc.)    │
│              └──────┬──────┘                                │
└─────────────────────┼───────────────────────────────────────┘
                      │
                      │ Auto-Match
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                    GUILD LAYER                               │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Guild Profile (PrimaryTheme, SecondaryTheme, ...)    │  │
│  └──────────────────┬───────────────────────────────────┘  │
│                     │                                        │
│              ┌──────▼──────┐                                │
│              │ Curriculum  │ (Auto-generated entries)       │
│              │   Builder   │                                │
│              └──────┬──────┘                                │
│                     │                                        │
│        ┌────────────┼────────────┐                          │
│        │            │            │                          │
│  ┌─────▼─────┐ ┌───▼────┐ ┌────▼─────┐                    │
│  │ Novice    │ │ Expert │ │  Master  │ (Curriculum entries │
│  │ Entries   │ │Entries │ │ Entries  │  by rank)          │
│  └───────────┘ └────────┘ └──────────┘                    │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      │ Teaching
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                  MEMBER LAYER                                │
│  ┌──────────────┐    ┌──────────────┐   ┌──────────────┐  │
│  │Guild Member 1│    │Guild Member 2│   │Guild Member N│  │
│  ├──────────────┤    ├──────────────┤   ├──────────────┤  │
│  │Student       │    │  Teacher     │   │  Student     │  │
│  │Progress      │◄───┤  Profile     │──►│  Progress    │  │
│  │Buffer        │    │              │   │  Buffer      │  │
│  └──────────────┘    └──────────────┘   └──────────────┘  │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Specialization Bonuses (applied based on rank)       │  │
│  │ - Speed, Effectiveness, Cost, Critical, Learning     │  │
│  └──────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

## System Integration Points

### 1. Knowledge System Integration

**Existing Systems**:
- `LessonAcquisitionSystem` - Handles learning lessons
- `LessonProgressionSystem` - Tracks mastery progression
- `SpellLearningSystem` - Handles spell acquisition

**New Integration**:

```csharp
// In LessonAcquisitionSystem
public partial struct LessonAcquisitionJob : IJobEntity
{
    void Execute(Entity entity, ref LessonProgress progress,
                 in DynamicBuffer<GuildMember> guilds)
    {
        // Check if entity is in a guild that teaches this lesson
        foreach (var membership in guilds)
        {
            if (GuildTeachesLesson(membership.GuildEntity, progress.LessonId))
            {
                // Apply guild teaching bonus
                float guildBonus = GetGuildTeachingBonus(membership);
                progress.LearningRate *= guildBonus;

                // Track progress in guild buffer
                AddGuildStudentProgress(entity, membership.GuildEntity,
                                       progress.LessonId);
            }
        }
    }
}
```

### 2. Aggregate System Integration

**Existing**:
- `GuildFormationSystem` - Creates guilds
- Guild components - `Guild`, `GuildMember`, `GuildKnowledge`

**Extension**:

```csharp
// Extend GuildFormationSystem
public partial class GuildFormationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Existing guild creation...

        // NEW: Build curriculum when guild is created
        Entities
            .WithNone<GuildCurriculum>()
            .WithAll<Guild>()
            .ForEach((Entity guildEntity, in Guild guild) =>
            {
                // Initialize curriculum based on guild type
                var curriculum = GetDefaultCurriculumForType(guild.Type);
                EntityManager.AddComponentData(guildEntity, curriculum);

                // Build curriculum entries
                var builder = new GuildCurriculumBuilder();
                builder.BuildCurriculum(guildEntity, curriculum,
                                       GetSingleton<LessonCatalogRef>());

                // Initialize teaching stats
                EntityManager.AddComponentData(guildEntity,
                    new GuildTeachingStats { /* defaults */ });
            }).WithStructuralChanges().Run();
    }
}
```

### 3. Teaching System (NEW)

**Purpose**: Manage teaching sessions between guild members

```csharp
[UpdateInGroup(typeof(GameplaySystemGroup))]
[UpdateAfter(typeof(LessonAcquisitionSystem))]
public partial class GuildTeachingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        var currentTick = GetSingleton<GameTickSingleton>().Tick;

        // Process active teaching sessions
        Entities
            .ForEach((Entity sessionEntity,
                     ref GuildTeachingSession session) =>
            {
                var elapsed = currentTick - session.SessionStartTick;
                if (elapsed >= session.SessionDuration)
                {
                    // Session complete - apply learning progress
                    var studentProgress = GetGuildStudentProgress(
                        session.StudentEntity,
                        session.GuildEntity,
                        session.EntryId);

                    studentProgress.Progress += session.ExpectedProgressGain;
                    studentProgress.TotalStudyTime += session.SessionDuration;
                    studentProgress.LastPracticeTick = currentTick;

                    // Update teacher stats
                    var teacherProfile = GetComponent<GuildTeacherProfile>(
                        session.TeacherEntity);
                    teacherProfile.LessonsTaught++;
                    teacherProfile.ActiveStudentCount--;

                    // Update guild stats
                    var guildStats = GetComponent<GuildTeachingStats>(
                        session.GuildEntity);
                    guildStats.TotalLessonsTaught++;
                    guildStats.LastTeachingTick = currentTick;

                    if (session.IsPaidLesson)
                    {
                        guildStats.TotalTuitionEarned += session.TuitionPaid;

                        // Pay teacher
                        var treasury = GetComponent<GuildTreasury>(
                            session.GuildEntity);
                        treasury.GoldReserves += session.TuitionPaid * 0.5f; // 50% to guild
                        // 50% to teacher (add to villager gold)
                    }

                    // Destroy session entity
                    EntityManager.DestroyEntity(sessionEntity);
                }
            }).WithStructuralChanges().Run();

        // Match students with teachers
        ProcessLearningRequests(currentTick);
    }

    private void ProcessLearningRequests(uint currentTick)
    {
        // For each guild with learning requests
        Entities
            .ForEach((Entity guildEntity,
                     in DynamicBuffer<GuildLearningRequest> requests,
                     in GuildCurriculum curriculum) =>
            {
                foreach (var request in requests)
                {
                    // Find available teacher
                    var teacher = FindTeacher(guildEntity,
                                            request.EntryId,
                                            request.PreferredTeacher);

                    if (teacher != Entity.Null)
                    {
                        // Create teaching session
                        CreateTeachingSession(guildEntity, teacher,
                                            request.RequesterEntity,
                                            request.EntryId,
                                            curriculum,
                                            currentTick);
                    }
                }
            }).WithoutBurst().Run();
    }
}
```

### 4. Bonus Application System (NEW)

**Purpose**: Apply guild specialization bonuses to combat/crafting

```csharp
[UpdateInGroup(typeof(GameplaySystemGroup))]
[UpdateAfter(typeof(GuildFormationSystem))]
public partial class GuildBonusApplicationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Apply bonuses when guild rank changes
        Entities
            .ForEach((Entity entity,
                     in DynamicBuffer<GuildMember> memberships,
                     ref DynamicBuffer<StatModifier> statMods) =>
            {
                foreach (var membership in memberships)
                {
                    var guild = GetComponent<Guild>(membership.GuildEntity);
                    var curriculum = GetComponent<GuildCurriculum>(
                        membership.GuildEntity);

                    // Calculate bonuses based on rank
                    var bonus = CalculateBonusForRank(membership.Rank,
                                                     curriculum);

                    // Apply as buff/stat modifier
                    ApplyGuildBonus(entity, bonus, curriculum.PrimaryTheme);
                }
            }).WithoutBurst().Run();
    }

    private void ApplyGuildBonus(Entity entity,
                                GuildSpecializationBonus bonus,
                                GuildTheme theme)
    {
        // Apply speed bonus
        if (bonus.SpeedBonus > 1.0f)
        {
            AddOrUpdateBuff(entity, new SpeedBuff
            {
                Multiplier = bonus.SpeedBonus,
                Source = BuffSource.Guild
            });
        }

        // Apply effectiveness bonus (damage, healing, etc.)
        if (bonus.EffectivenessBonus > 1.0f)
        {
            // Theme-specific bonuses
            if ((theme & GuildTheme.Offensive) != 0)
            {
                AddStatModifier(entity, StatType.Damage,
                              bonus.EffectivenessBonus);
            }
            else if ((theme & GuildTheme.Defensive) != 0)
            {
                AddStatModifier(entity, StatType.Defense,
                              bonus.EffectivenessBonus);
            }
            // etc.
        }

        // Apply cost reduction
        if (bonus.CostReduction > 0)
        {
            AddStatModifier(entity, StatType.ManaCostReduction,
                          bonus.CostReduction);
        }

        // Apply critical bonus
        if (bonus.CriticalBonus > 0)
        {
            AddStatModifier(entity, StatType.CriticalChance,
                          bonus.CriticalBonus);
        }
    }
}
```

### 5. Combat System Integration

**Integration with SpellCastingSystem**:

```csharp
// In SpellCastingSystem
partial struct SpellCastJob : IJobEntity
{
    void Execute(ref SpellCast cast, in SpellKnowledge knowledge,
                 in DynamicBuffer<GuildMember> guilds)
    {
        // Check if any guild provides bonuses for this spell
        foreach (var membership in guilds)
        {
            var bonus = GetGuildBonus(membership.GuildEntity,
                                     cast.SpellId);

            if (bonus.EffectivenessBonus > 0)
            {
                // Apply guild effectiveness bonus
                cast.PowerMultiplier *= bonus.EffectivenessBonus;
            }

            if (bonus.CostReduction > 0)
            {
                // Reduce mana cost
                cast.ManaCost *= (1.0f - bonus.CostReduction);
            }

            if (bonus.SpeedBonus > 1.0f)
            {
                // Reduce cast time
                cast.CastTime /= bonus.SpeedBonus;
            }
        }
    }
}
```

## Project-Specific Integration

### Godgame Project

**Guild Examples**:
- Farmers' Guild: Teaching farming lessons, harvest bonuses
- Smiths' Guild: Teaching crafting, equipment quality bonuses
- Mages' Guild: Teaching spells from all schools

**Integration**:

```csharp
// In Godgame project
namespace Godgame.Guilds
{
    public static class GodgameGuildPresets
    {
        public static GuildCurriculum FarmersGuild => new()
        {
            PrimaryTheme = GuildTheme.Survival | GuildTheme.Cooking,
            SecondaryTheme = GuildTheme.None,
            TeachingEfficiency = 1.1f,
            AcceptsPublicStudents = true,
            PublicLessonCost = 25f
        };

        public static GuildCurriculum SmithsGuild => new()
        {
            PrimaryTheme = GuildTheme.Smithing | GuildTheme.Enchanting,
            SecondaryTheme = GuildTheme.None,
            TeachingEfficiency = 1.0f,
            AcceptsPublicStudents = true,
            PublicLessonCost = 100f,
            HasSignatureTechniques = true
        };

        public static GuildCurriculum MagesGuild => new()
        {
            PrimaryTheme = GuildTheme.Arcane | GuildTheme.Elemental,
            SecondaryTheme = GuildTheme.Divine | GuildTheme.Necromancy,
            TeachingEfficiency = 0.9f,
            AcceptsPublicStudents = true,
            PublicLessonCost = 150f,
            RequiredEnlightenment = 30
        };
    }
}
```

### Space4X Project

**Guild Examples** (adapted to space setting):
- Fleet Academy: Tactical lessons, piloting, combat maneuvers
- Engineers' Corps: Ship repair, module crafting, system optimization
- Explorer's Society: Navigation, sensor mastery, jump calculations

**Integration**:

```csharp
namespace Space4X.Guilds
{
    public static class Space4XGuildPresets
    {
        public static GuildCurriculum FleetAcademy => new()
        {
            PrimaryTheme = GuildTheme.Tactical | GuildTheme.Leadership,
            SecondaryTheme = GuildTheme.Offensive | GuildTheme.Defensive,
            TeachingEfficiency = 1.2f,
            AcceptsPublicStudents = false // Military training
        };

        public static GuildCurriculum EngineersCorps => new()
        {
            PrimaryTheme = GuildTheme.Construction | GuildTheme.Enchanting,
            SecondaryTheme = GuildTheme.None,
            TeachingEfficiency = 1.0f,
            AcceptsPublicStudents = true
        };
    }
}
```

## Data Flow Example

### Example: Villager Joins Guild of Wrath

1. **Guild Formation**:
   ```
   Entity: GuildOfWrath
   Components:
     - Guild { Type: Heroes, Name: "Guild of Wrath" }
     - GuildCurriculum { PrimaryTheme: Offensive | FireFocus }
     - GuildCurriculumEntry[] (buffer, 47 entries auto-generated)
     - GuildTeachingStats { TotalLessonsTaught: 0 }
   ```

2. **Villager Joins**:
   ```
   Entity: Villager_023
   Add to GuildMember buffer on GuildOfWrath:
     - VillagerEntity: Villager_023
     - Rank: 0 (Member)
     - JoinedTick: 12500

   Add components to Villager_023:
     - GuildStudentProgress[] (buffer, empty initially)
     - GuildSpecializationBonus:
         SpeedBonus: 1.0
         EffectivenessBonus: 1.15
         CriticalBonus: 0.05
   ```

3. **Villager Requests Lesson**:
   ```
   Add to GuildLearningRequest buffer on GuildOfWrath:
     - RequesterEntity: Villager_023
     - EntryId: "berserker_rage"
     - WillPayTuition: false (member)
   ```

4. **Teaching Session Created**:
   ```
   Entity: TeachingSession_001
   Components:
     - GuildTeachingSession:
         TeacherEntity: Villager_101 (officer with teaching skill)
         StudentEntity: Villager_023
         GuildEntity: GuildOfWrath
         EntryId: "berserker_rage"
         SessionDuration: 3600 (1 hour game time)
         TeachingQuality: 1.8 (teacher is expert + guild bonus)
         ExpectedProgressGain: 0.15 (15% toward Novice)
   ```

5. **Session Completes**:
   ```
   Update GuildStudentProgress on Villager_023:
     - EntryId: "berserker_rage"
     - CurrentTier: Novice
     - Progress: 0.15
     - TeacherEntity: Villager_101
     - TotalStudyTime: 3600

   Update LessonProgress on Villager_023:
     - LessonId: "berserker_rage"
     - MasteryTier: Novice
     - Progress: 0.15

   Update GuildTeachingStats on GuildOfWrath:
     - TotalLessonsTaught: 1
     - TotalStudentsTrained: 1
   ```

6. **Villager Uses Berserker Rage**:
   ```
   In combat, applies bonuses:
     - Base Damage: 50
     - Guild Effectiveness Bonus: 1.15x
     - Total Damage: 57.5
     - Critical Chance: +5% from guild
   ```

## Performance Considerations

### Memory Layout

```csharp
// Efficient SoA storage for guild bonuses
public struct GuildBonusArray : IComponentData
{
    public NativeArray<float> SpeedBonuses;      // Per-member
    public NativeArray<float> EffectivenessBonuses;
    public NativeArray<float> CostReductions;
    public NativeArray<byte> Ranks;
}
```

### Burst Compilation

```csharp
[BurstCompile]
public partial struct GuildBonusCalculationJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<GuildCurriculum> GuildLookup;

    void Execute(in GuildMember membership,
                 ref GuildSpecializationBonus bonus)
    {
        var curriculum = GuildLookup[membership.GuildEntity];

        // Calculate bonuses based on rank (Burst-friendly)
        bonus.SpeedBonus = 1.0f + (membership.Rank * 0.1f);
        bonus.EffectivenessBonus = 1.0f + (membership.Rank * 0.15f);
        // ...
    }
}
```

### Update Frequency

- **Curriculum Building**: Once on guild creation, or when catalog updates
- **Bonus Calculation**: Only when rank changes or new member joins
- **Teaching Sessions**: Every frame (or fixed timestep)
- **Learning Requests**: Every few seconds (scheduled with delay)

## Testing Strategy

### Unit Tests

```csharp
[TestFixture]
public class GuildCurriculumTests
{
    [Test]
    public void CurriculumBuilder_MatchesThemes()
    {
        // Create test lesson with Offensive theme
        var lesson = CreateTestLesson("fireball",
            GuildTheme.Offensive | GuildTheme.FireFocus);

        // Create test guild with Offensive theme
        var guild = CreateTestGuild(GuildTheme.Offensive);

        // Build curriculum
        var builder = new GuildCurriculumBuilder();
        var entries = builder.BuildCurriculum(guild, lesson);

        // Assert lesson is included
        Assert.IsTrue(entries.Any(e => e.EntryId == "fireball"));
        Assert.GreaterOrEqual(entries.First().ThemeRelevance, 80);
    }

    [Test]
    public void TeachingSession_AppliesLearningBonus()
    {
        // Create guild with 1.2x teaching efficiency
        var guild = CreateTestGuild(teachingEfficiency: 1.2f);

        // Create teaching session
        var session = CreateTestSession(guild);

        // Calculate expected progress
        float baseProgress = 0.1f;
        float expected = baseProgress * 1.2f; // Guild bonus

        Assert.AreEqual(expected, session.ExpectedProgressGain);
    }
}
```

### Integration Tests

```csharp
[TestFixture]
public class GuildIntegrationTests
{
    [Test]
    public void Villager_JoinsGuild_ReceivesBonuses()
    {
        // Setup
        var world = CreateTestWorld();
        var guild = CreateGuildOfWrath(world);
        var villager = CreateTestVillager(world);

        // Act: Villager joins guild
        JoinGuild(villager, guild);
        world.Update();

        // Assert: Bonuses applied
        var bonus = GetComponent<GuildSpecializationBonus>(villager);
        Assert.AreEqual(1.15f, bonus.EffectivenessBonus);
    }
}
```

## Migration Path

### From Existing Systems

If you have existing guild/faction systems:

1. **Add theme tags to existing content** (lessons, spells)
2. **Create GuildCurriculum components** for existing guilds
3. **Run curriculum builder** to auto-populate entries
4. **Test bonuses** in existing gameplay scenarios
5. **Migrate teaching logic** to new GuildTeachingSystem

### Backward Compatibility

```csharp
// Legacy guild creation still works
public struct LegacyGuild : IComponentData
{
    public GuildType Type;
    // ...
}

// Auto-convert to new system
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class LegacyGuildMigrationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithAll<LegacyGuild>()
            .WithNone<GuildCurriculum>()
            .ForEach((Entity e, in LegacyGuild legacy) =>
            {
                var curriculum = ConvertLegacyToNewCurriculum(legacy);
                EntityManager.AddComponentData(e, curriculum);
            }).WithStructuralChanges().Run();
    }
}
```

## Summary

The thematic guild curriculum system:

✅ **Flexible**: Guilds teach across schools based on themes
✅ **Automatic**: Curriculum auto-generated from tags
✅ **Integrated**: Works with existing knowledge/combat systems
✅ **Performant**: Burst-compiled, efficient data layout
✅ **Testable**: Clear separation of concerns
✅ **Extensible**: Easy to add new themes and content

**Next Steps**:
1. Implement authoring tools
2. Tag existing content
3. Create system implementations
4. Test in game projects
5. Iterate based on gameplay feedback

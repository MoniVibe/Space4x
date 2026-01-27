# Environmental Quests and Loot Vectors

**Status**: Concept Design
**Last Updated**: 2025-11-30
**Cross-Project**: Godgame (primary), Space4X (adapted)

---

## Overview

**Environmental Quests** are dynamic encounters that spawn based on player/NPC actions and environmental conditions. These range from benevolent spirits seeking closure to malevolent entities threatening civilizations. The system creates organic questlines from environmental corruption, abandoned spaces, and supernatural reactions to violence, exploitation, or neglect.

**Core Principles**:
- ✅ **Consequence-Driven**: Spawns triggered by player actions (deforestation, bloodshed, abandonment)
- ✅ **Alignment-Responsive**: Not all spawns are hostile; interactions vary by alignment
- ✅ **Class-Asymmetry**: Spiritual classes excel, physical classes struggle (or vice versa)
- ✅ **Territorial Dynamics**: Spawns encroach on civilization unless borders secured
- ✅ **Multiple Resolution Paths**: Combat, diplomacy, communion, enslavement, research
- ✅ **Knowledge & Labor Economy**: Undead/spirits can be workers, teachers, council members

**Design Goals**:
- Create emergent questlines from environmental state
- Reward diverse class composition (priests, shamans, necromancers)
- Punish unchecked exploitation (deforestation, mining, war)
- Enable alignment-specific strategies (Good communes, Evil enslaves)
- Integrate with existing systems (knowledge, labor, defenses)

---

## Environmental Corruption Triggers

### Godgame Triggers

```csharp
public struct EnvironmentalCorruption : IComponentData
{
    public CorruptionType Type;
    public float Intensity;               // 0-1 (threshold for spawn)
    public float3 Epicenter;
    public float Radius;
    public uint AccumulationStartTick;
    public Entity ResponsibleAggregate;   // Who caused it (for grudges)
}

public enum CorruptionType : byte
{
    // Death & Violence
    BloodSoakedGround,        // Battle aftermath, massacres
    MassGrave,                // Cemeteries, burial sites
    Desecration,              // Defiled holy sites

    // Exploitation
    Deforestation,            // Heavy logging, forest destruction
    MineInfestation,          // Deep mining, earth wounds
    OverHunting,              // Ecosystem collapse

    // Abandonment
    GhostTown,                // Abandoned villages
    RuinedStructure,          // Collapsed buildings, temples
    ForgottenShrine,          // Neglected holy sites

    // Natural Phenomena
    DeepOceanHorror,          // Ancient things in the deep
    DarknessCrawl,            // Night-spawn entities
    SkyFall,                  // Otherworldly arrivals

    // Memetic/Ideological
    IdeaInfestation,          // Thought-form entities
    CursedKnowledge,          // Forbidden lore spreading
    PlagueOfMadness,          // Collective insanity
}
```

### Corruption Accumulation

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class EnvironmentalCorruptionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Blood-soaked ground from combat
        Entities.ForEach((Entity entity, in DeathEvent death, in LocalTransform transform) =>
        {
            var corruption = new EnvironmentalCorruption
            {
                Type = CorruptionType.BloodSoakedGround,
                Intensity = 0.1f,
                Epicenter = transform.Position,
                Radius = 10f,
                AccumulationStartTick = CurrentTick,
            };

            // Accumulate at battle sites
            AccumulateCorruption(transform.Position, corruption);

        }).Run();

        // Deforestation from logging
        Entities.ForEach((Entity entity, in TreeHarvestEvent harvest, in LocalTransform transform) =>
        {
            var forestCorruption = GetOrCreateCorruption(transform.Position, CorruptionType.Deforestation);
            forestCorruption.Intensity += 0.05f;  // Each tree cut adds 5%

            if (forestCorruption.Intensity >= 0.7f)
            {
                // Forest is 70% destroyed → spawn forest hauntings
                TriggerForestHaunting(transform.Position);
            }

        }).Run();

        // Mine infestation from deep mining
        Entities.ForEach((ref MineComponents mine, in LocalTransform transform) =>
        {
            if (mine.Depth > 100f)  // Deep mines
            {
                var mineCorruption = GetOrCreateCorruption(transform.Position, CorruptionType.MineInfestation);
                mineCorruption.Intensity += 0.01f * Time.DeltaTime;  // Slowly increases

                if (mineCorruption.Intensity >= 1f)
                {
                    SpawnMineInfestation(entity);
                }
            }

        }).Run();

        // Abandoned villages become ghost towns
        Entities.ForEach((Entity village, ref SettlementComponents settlement, in LocalTransform transform) =>
        {
            if (settlement.PopulationCount == 0 && settlement.AbandonedTicks > 5000)
            {
                var ghostTown = new EnvironmentalCorruption
                {
                    Type = CorruptionType.GhostTown,
                    Intensity = 1f,
                    Epicenter = transform.Position,
                    Radius = settlement.InfluenceRadius,
                    AccumulationStartTick = CurrentTick,
                };

                SpawnGhostTownEncounter(village, ghostTown);
            }

        }).Run();
    }
}
```

---

## Spawn Types and Entities

### Benevolent Spawns

```csharp
public struct BenevolentSpirit : IComponentData
{
    public SpiritType Type;
    public Entity OriginalVillager;      // If spirit of dead villager
    public QuestObjective Objective;
    public float PatienceRemaining;      // 0-1 (will turn malevolent if ignored)
    public Entity TargetVillager;        // Who they want to contact
}

public enum SpiritType : byte
{
    // Seeking Closure
    LostHeirloom,             // Return trinket to loved ones
    UnfinishedBusiness,       // Complete task they failed
    HonorableDuel,            // Defeated by worthy foe to move on
    ForgottenPromise,         // Fulfill oath they couldn't

    // Benevolent Aid
    AncestralGuide,           // Teach skills to descendants
    TreasureReveal,           // Show hidden cache
    WarningSpirit,            // Alert to danger
    ProtectiveGuardian,       // Defend holy site
}

public struct QuestObjective : IComponentData
{
    public ObjectiveType Type;
    public Entity TargetEntity;          // Villager, location, item
    public float3 TargetLocation;
    public DynamicBuffer<SkillDefinition> SkillsToTeach;  // If teaching
    public int TreasureValue;            // If treasure
    public bool Completed;
}
```

### Malevolent Spawns

```csharp
public struct MalevolentEntity : IComponentData
{
    public ThreatType Type;
    public float PowerLevel;             // 0-1 (determines difficulty)
    public Entity Anchor;                // Corruption source (cemetery, mine, etc.)
    public uint SpawnTick;
    public ThreatBehavior Behavior;
    public AlignmentGate AlignmentLock;  // Can be reasoned with by specific alignments
}

public enum ThreatType : byte
{
    // Undead
    Haunt,                    // Poltergeist, minor undead
    Wraith,                   // Ethereal undead
    Lich,                     // Powerful undead sorcerer
    UndeadArmy,               // Mass undead (lich followers)

    // Demonic
    ImpSwarm,                 // Minor demons
    Demon,                    // Major demon
    PortalGuardian,           // Demon protecting rift

    // Possession
    Dibbuk,                   // Possessing spirit
    Poltergeist,              // Object-manipulating entity

    // Otherworldly
    DimensionalHorror,        // Lovecraftian entity
    IdeaParasite,             // Memetic hazard
    VoidCrawler,              // Darkness-spawn creature

    // Corrupted Nature
    CorruptedTreent,          // Twisted forest guardian
    MineAbomination,          // Deep-earth horror
    OceanLeviathan,           // Deep sea monster
}

public enum ThreatBehavior : byte
{
    Aggressive,               // Attack on sight
    Territorial,              // Defend area, don't pursue
    Diplomatic,               // Open to negotiation
    Parasitic,                // Spread corruption/possession
    Dormant,                  // Inactive until triggered
}
```

### Persistent Spirit Knowledge

```csharp
public struct SpiritKnowledge : IComponentData
{
    public Entity DeceasedVillager;      // Original villager entity (dead)
    public DynamicBuffer<SkillDefinition> RetainedSkills;
    public DynamicBuffer<KnowledgeFragment> Secrets;  // Knowledge they had
    public VillagerAlignment AlignmentAtDeath;
    public float CommunionDifficulty;    // 0-1 (how hard to contact)
    public bool WillingToTeach;          // Benevolent or payment required
}

// Dead villagers persist as spirits with their skills
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class VillagerDeathSpiritPersistence : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((Entity entity, in DeathEvent death, in VillagerSkills skills, in VillagerAlignment alignment) =>
        {
            if (death.DeathType == DeathType.Natural || death.DeathType == DeathType.Combat)
            {
                // Create spirit entity
                var spirit = EntityManager.CreateEntity();
                EntityManager.AddComponent<BenevolentSpirit>(spirit);
                EntityManager.AddComponent<SpiritKnowledge>(spirit);

                var spiritKnowledge = new SpiritKnowledge
                {
                    DeceasedVillager = entity,
                    RetainedSkills = skills.Skills,  // Copy skills
                    AlignmentAtDeath = alignment,
                    CommunionDifficulty = CalculateCommunionDifficulty(alignment, skills),
                    WillingToTeach = alignment.MoralAxis > 0,  // Good spirits willing to teach
                };

                // Spirit behavior based on alignment at death
                if (alignment.MoralAxis < -50)  // Evil spirit
                {
                    // May become malevolent, seek resurrection, curse land
                    ConvertToMalevolentSpirit(spirit);
                }
            }

        }).Run();
    }
}
```

---

## Class Effectiveness Matrix

### Godgame Classes vs. Encounter Types

```csharp
public struct ClassEffectiveness : IComponentData
{
    public VillagerClass Class;
    public ThreatType TargetType;
    public float EffectivenessMultiplier;  // 0.1 (very weak) to 3.0 (very strong)
    public bool CanAcceptQuest;            // Some classes refuse certain quests
}

// Example effectiveness table
public static readonly ClassEffectiveness[] EffectivenessTable = new[]
{
    // Priests: Strong vs undead, demons
    new ClassEffectiveness { Class = VillagerClass.Priest, TargetType = ThreatType.Haunt, EffectivenessMultiplier = 2.5f, CanAcceptQuest = true },
    new ClassEffectiveness { Class = VillagerClass.Priest, TargetType = ThreatType.Wraith, EffectivenessMultiplier = 2.5f, CanAcceptQuest = true },
    new ClassEffectiveness { Class = VillagerClass.Priest, TargetType = ThreatType.Demon, EffectivenessMultiplier = 2.0f, CanAcceptQuest = true },
    new ClassEffectiveness { Class = VillagerClass.Priest, TargetType = ThreatType.Dibbuk, EffectivenessMultiplier = 3.0f, CanAcceptQuest = true },

    // Warriors: Weak vs ethereal, strong vs physical
    new ClassEffectiveness { Class = VillagerClass.Warrior, TargetType = ThreatType.Wraith, EffectivenessMultiplier = 0.2f, CanAcceptQuest = false },
    new ClassEffectiveness { Class = VillagerClass.Warrior, TargetType = ThreatType.UndeadArmy, EffectivenessMultiplier = 0.8f, CanAcceptQuest = true },
    new ClassEffectiveness { Class = VillagerClass.Warrior, TargetType = ThreatType.CorruptedTreent, EffectivenessMultiplier = 1.5f, CanAcceptQuest = true },

    // Shamans: Moderate vs all, can commune with spirits
    new ClassEffectiveness { Class = VillagerClass.Shaman, TargetType = ThreatType.Haunt, EffectivenessMultiplier = 1.8f, CanAcceptQuest = true },
    new ClassEffectiveness { Class = VillagerClass.Shaman, TargetType = ThreatType.BenevolentSpirit, EffectivenessMultiplier = 2.5f, CanAcceptQuest = true },
    new ClassEffectiveness { Class = VillagerClass.Shaman, TargetType = ThreatType.IdeaParasite, EffectivenessMultiplier = 2.0f, CanAcceptQuest = true },

    // Necromancers: Can enslave undead instead of destroying
    new ClassEffectiveness { Class = VillagerClass.Necromancer, TargetType = ThreatType.Haunt, EffectivenessMultiplier = 2.0f, CanAcceptQuest = true },
    new ClassEffectiveness { Class = VillagerClass.Necromancer, TargetType = ThreatType.UndeadArmy, EffectivenessMultiplier = 3.0f, CanAcceptQuest = true },
    new ClassEffectiveness { Class = VillagerClass.Necromancer, TargetType = ThreatType.Lich, EffectivenessMultiplier = 2.5f, CanAcceptQuest = true },

    // Paladins: Strong vs all evil, righteous fury
    new ClassEffectiveness { Class = VillagerClass.Paladin, TargetType = ThreatType.Demon, EffectivenessMultiplier = 3.0f, CanAcceptQuest = true },
    new ClassEffectiveness { Class = VillagerClass.Paladin, TargetType = ThreatType.Wraith, EffectivenessMultiplier = 2.5f, CanAcceptQuest = true },
    new ClassEffectiveness { Class = VillagerClass.Paladin, TargetType = ThreatType.Lich, EffectivenessMultiplier = 2.0f, CanAcceptQuest = true },
};

public struct QuestAcceptanceCheck : IComponentData
{
    public Entity QuestEntity;
    public Entity VillagerEntity;
    public float AcceptanceProbability;   // 0-1
    public DynamicBuffer<Entity> RequiredClasses;  // May need specific class combo
}

// Quest acceptance logic
public static bool CanAcceptQuest(Entity villager, Entity quest)
{
    var villagerClass = GetComponent<VillagerClass>(villager);
    var questType = GetComponent<QuestDefinition>(quest).ThreatType;
    var alignment = GetComponent<VillagerAlignment>(villager);

    // Find effectiveness entry
    var effectiveness = EffectivenessTable.FirstOrDefault(e =>
        e.Class == villagerClass && e.TargetType == questType);

    if (effectiveness == null || !effectiveness.CanAcceptQuest)
        return false;

    // Warriors won't accept ethereal quests alone
    if (effectiveness.EffectivenessMultiplier < 0.5f)
    {
        // Check if they have backup (priest, paladin in party)
        var party = GetComponent<PartyMembers>(villager);
        if (!party.Members.Any(m => HasSpiritualClass(m)))
            return false;
    }

    // Evil villagers may embrace demonic quests instead of destroying
    if (questType == ThreatType.Demon && alignment.MoralAxis < -50)
    {
        // May try to ally with demon instead (different quest outcome)
        return true;
    }

    return true;
}
```

---

## Territorial Mechanics

### Village Influence and Encroachment

```csharp
public struct TerritorialInfluence : IComponentData
{
    public Entity Village;
    public float InfluenceRadius;        // Distance from village center
    public float LightLevel;             // 0 (darkness) to 1 (full light)
    public int PeacekeeperCount;         // Patrols securing borders
    public float EncroachmentPressure;   // 0-1 (spawn pressure at borders)
    public uint LastPatrolTick;
}

public struct EncroachmentZone : IComponentData
{
    public float3 Position;
    public float Radius;
    public float ThreatLevel;            // 0-1
    public DynamicBuffer<Entity> ActiveThreats;
    public bool IsSecured;               // Peacekeepers cleared it
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class TerritorialEncroachmentSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref TerritorialInfluence influence, in SettlementComponents settlement) =>
        {
            // Calculate light level based on peacekeepers
            influence.LightLevel = CalculateLightLevel(influence.PeacekeeperCount, settlement.PopulationCount);

            // Encroachment pressure increases if borders not secured
            uint ticksSincePatrol = CurrentTick - influence.LastPatrolTick;
            if (ticksSincePatrol > 500)  // No patrol in 500 ticks
            {
                influence.EncroachmentPressure += 0.1f * Time.DeltaTime;
            }

            // Spawn threats at borders if pressure high
            if (influence.EncroachmentPressure > 0.7f)
            {
                SpawnBorderThreat(influence);
                influence.EncroachmentPressure = 0f;
            }

            // Light pushes back darkness
            if (influence.LightLevel > 0.8f)
            {
                influence.EncroachmentPressure = math.max(0f, influence.EncroachmentPressure - 0.05f);
            }

        }).Run();

        // Peacekeepers patrol borders, fight wilderness
        Entities.ForEach((ref VillagerBehavior behavior, in VillagerClass villagerClass, in LocalTransform transform) =>
        {
            if (villagerClass == VillagerClass.Peacekeeper)
            {
                var nearestVillage = FindNearestVillage(transform.Position);
                var influence = GetComponent<TerritorialInfluence>(nearestVillage);

                // Patrol behavior: Move to border, clear threats
                if (IsAtBorder(transform.Position, influence))
                {
                    // Update patrol timestamp
                    influence.LastPatrolTick = CurrentTick;

                    // Engage threats
                    var threats = GetThreatsInRadius(transform.Position, 50f);
                    if (threats.Length > 0)
                    {
                        EngageThreat(entity, threats[0]);
                    }
                }
            }

        }).Run();
    }
}
```

### Village Expansion by Alignment

```csharp
public struct VillageExpansionConfig : IComponentData
{
    public ExpansionStrategy Strategy;
    public float ExpansionRate;          // Units per 1000 ticks
    public float DefenseInvestment;      // % of resources to walls/defenses
    public bool BuildWalls;
    public bool AggressiveClear;         // Clear threats preemptively
}

// Expansion strategy based on alignment/outlook
public static ExpansionStrategy GetExpansionStrategy(VillagerAlignment alignment, VillagerOutlook outlook)
{
    // Lawful + Good: Defensive expansion, high walls
    if (alignment.OrderAxis > 50 && alignment.MoralAxis > 50)
    {
        return new ExpansionStrategy
        {
            ExpansionRate = 1.0f,
            DefenseInvestment = 0.6f,  // 60% to defenses
            BuildWalls = true,
            AggressiveClear = false,   // Only respond to threats
        };
    }

    // Chaotic + Evil: Aggressive expansion, low defenses
    if (alignment.OrderAxis < -50 && alignment.MoralAxis < -50)
    {
        return new ExpansionStrategy
        {
            ExpansionRate = 2.0f,       // Fast expansion
            DefenseInvestment = 0.2f,   // 20% to defenses
            BuildWalls = false,
            AggressiveClear = true,     // Preemptively attack threats
        };
    }

    // Neutral: Balanced
    return new ExpansionStrategy
    {
        ExpansionRate = 1.2f,
        DefenseInvestment = 0.4f,
        BuildWalls = true,
        AggressiveClear = false,
    };
}
```

---

## Interaction Modes

### Combat Resolution

```csharp
public struct QuestCombatEncounter : IComponentData
{
    public Entity QuestEntity;
    public Entity ThreatEntity;
    public DynamicBuffer<Entity> PartyMembers;
    public float CombatDifficulty;       // 0-1
    public float SuccessProbability;     // Based on class effectiveness
}

// Combat effectiveness calculation
public static float CalculateCombatSuccess(Entity threat, DynamicBuffer<Entity> party)
{
    float totalEffectiveness = 0f;
    var threatType = GetComponent<MalevolentEntity>(threat).Type;

    foreach (var member in party)
    {
        var memberClass = GetComponent<VillagerClass>(member);
        var effectiveness = GetEffectiveness(memberClass, threatType);
        totalEffectiveness += effectiveness.EffectivenessMultiplier;
    }

    // Party size bonus (diminishing returns)
    float partyBonus = math.log2(party.Length + 1) * 0.2f;

    // Calculate success probability
    float baseDifficulty = GetComponent<MalevolentEntity>(threat).PowerLevel;
    float successProb = math.clamp(totalEffectiveness + partyBonus - baseDifficulty, 0f, 1f);

    return successProb;
}
```

### Communion & Teaching

```csharp
public struct CommunionAttempt : IComponentData
{
    public Entity Shaman;
    public Entity Spirit;
    public float SuccessChance;          // Based on shaman skill
    public CommunionObjective Objective;
}

public enum CommunionObjective : byte
{
    LearnSkills,              // Spirit teaches retained skills
    GainKnowledge,            // Spirit shares secrets
    AcceptQuest,              // Spirit gives quest
    Bargain,                  // Trade (spirit wants something)
    Banish,                   // Peacefully dismiss spirit
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SpiritCommunionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref CommunionAttempt communion, in VillagerSkills shamanSkills) =>
        {
            var spirit = GetComponent<SpiritKnowledge>(communion.Spirit);
            var shamanClass = GetComponent<VillagerClass>(communion.Shaman);

            // Shamans have easiest time
            float baseDifficulty = spirit.CommunionDifficulty;
            float classModifier = shamanClass == VillagerClass.Shaman ? 0.5f : 1.0f;

            // Skill in relevant domain helps
            float skillBonus = GetSpiritualSkillLevel(shamanSkills) * 0.3f;

            communion.SuccessChance = math.clamp(1f - (baseDifficulty * classModifier) + skillBonus, 0f, 1f);

            // Roll for success
            if (Random.NextFloat() < communion.SuccessChance)
            {
                switch (communion.Objective)
                {
                    case CommunionObjective.LearnSkills:
                        // Transfer skills from spirit to shaman
                        TransferSkills(spirit.RetainedSkills, communion.Shaman);
                        break;

                    case CommunionObjective.GainKnowledge:
                        // Spirit reveals secrets
                        RevealKnowledge(spirit.Secrets, communion.Shaman);
                        break;

                    case CommunionObjective.Banish:
                        // Peacefully dismiss spirit (no combat)
                        EntityManager.DestroyEntity(communion.Spirit);
                        break;
                }
            }

        }).Run();
    }
}
```

### Enslavement (Necromancers/Demonologists)

```csharp
public struct EnslavedEntity : IComponentData
{
    public Entity Master;                // Necromancer/demonologist
    public Entity OriginalThreat;
    public float ObedienceLevel;         // 0-1 (revolt risk)
    public EnslavedRole Role;
    public uint EnslavementTick;
}

public enum EnslavedRole : byte
{
    Labor,                    // Hauling, mining, woodcutting
    Refining,                 // Resource processing
    Research,                 // Scholarly work (high-level undead)
    Defense,                  // Guard village
    CouncilMember,            // Advisor (lich-level intellect)
}

public struct NecromancerEnslavement : IComponentData
{
    public Entity Necromancer;
    public Entity TargetThreat;
    public float EnslavementChance;      // Based on necromancer skill
    public int MaxSlaves;                // Necromancer capacity
    public int CurrentSlaves;
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NecromancerEnslavementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref NecromancerEnslavement enslavement, in VillagerSkills skills) =>
        {
            var threat = GetComponent<MalevolentEntity>(enslavement.TargetThreat);

            // Can only enslave undead/demons (not all threats)
            if (threat.Type != ThreatType.Haunt &&
                threat.Type != ThreatType.Wraith &&
                threat.Type != ThreatType.Demon &&
                threat.Type != ThreatType.UndeadArmy)
            {
                return;  // Cannot enslave this type
            }

            // Check capacity
            if (enslavement.CurrentSlaves >= enslavement.MaxSlaves)
            {
                return;  // At capacity
            }

            // Enslavement difficulty based on threat power
            float difficulty = threat.PowerLevel;
            float necromancerPower = GetNecromancySkill(skills) / 100f;
            enslavement.EnslavementChance = math.clamp(necromancerPower - difficulty, 0f, 1f);

            // Attempt enslavement
            if (Random.NextFloat() < enslavement.EnslavementChance)
            {
                var enslaved = new EnslavedEntity
                {
                    Master = enslavement.Necromancer,
                    OriginalThreat = enslavement.TargetThreat,
                    ObedienceLevel = 1f - difficulty,  // Powerful entities more likely to revolt
                    Role = DetermineRole(threat.Type),
                    EnslavementTick = CurrentTick,
                };

                EntityManager.AddComponent(enslavement.TargetThreat, enslaved);
                enslavement.CurrentSlaves++;

                // Remove threat tag (now enslaved)
                EntityManager.RemoveComponent<MalevolentEntity>(enslavement.TargetThreat);
            }

        }).Run();

        // Enslaved labor integration
        Entities.ForEach((ref EnslavedEntity enslaved, in LocalTransform transform) =>
        {
            switch (enslaved.Role)
            {
                case EnslavedRole.Labor:
                    // Perform labor tasks (mining, hauling, etc.)
                    AssignLaborTask(entity, enslaved.Master);
                    break;

                case EnslavedRole.Research:
                    // Contribute to research (if lich-level)
                    ContributeToResearch(entity, enslaved.Master);
                    break;

                case EnslavedRole.CouncilMember:
                    // Serve on village council (high-level undead)
                    ParticipateInCouncil(entity, enslaved.Master);
                    break;
            }

            // Obedience decay over time
            enslaved.ObedienceLevel -= 0.001f * Time.DeltaTime;

            // Revolt check
            if (enslaved.ObedienceLevel < 0.3f && Random.NextFloat() < 0.1f)
            {
                TriggerRevolt(entity, enslaved);
            }

        }).Run();
    }
}
```

### Demonic Bargains & Forbidden Knowledge

```csharp
public struct DemonicBargain : IComponentData
{
    public Entity Demon;
    public Entity Villager;
    public KnowledgeType OfferedKnowledge;
    public float PowerGain;              // 0-1
    public float AlignmentCost;          // Shift toward Evil
    public BargainPrice Price;
}

public enum BargainPrice : byte
{
    SoulDebt,                 // Future obligation
    AlignmentShift,           // Become more Evil
    Sacrifice,                // Kill someone
    ServiceDuration,          // Serve demon for N ticks
    SpreadCorruption,         // Corrupt others
}

public struct ForbiddenKnowledge : IComponentData
{
    public KnowledgeFragment Knowledge;
    public float PowerBonus;             // +30% effectiveness in domain
    public float CorruptionRate;         // Alignment drift per tick
    public bool Contagious;              // Spreads to others who learn it
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class DemonicBargainSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref DemonicBargain bargain, ref VillagerAlignment alignment) =>
        {
            // Demon offers knowledge/power
            var demon = GetComponent<MalevolentEntity>(bargain.Demon);

            // Only Opportunist, Evil, or desperate villagers accept
            if (alignment.MoralAxis > 30 && alignment.AlignmentStrength > 0.6f)
            {
                // Good-aligned with strong conviction refuses
                EntityManager.DestroyEntity(bargain.Entity);
                return;
            }

            // Villager accepts bargain
            var knowledge = new ForbiddenKnowledge
            {
                Knowledge = GenerateForbiddenKnowledge(demon),
                PowerBonus = bargain.PowerGain,
                CorruptionRate = 0.01f,  // 1% shift toward Evil per tick
                Contagious = true,       // Spreads to guild members
            };

            EntityManager.AddComponent(bargain.Villager, knowledge);

            // Immediate alignment shift
            alignment.MoralAxis -= (sbyte)(bargain.AlignmentCost * 100);
            alignment.PurityAxis -= (sbyte)(bargain.AlignmentCost * 50);

            // Apply price
            ApplyBargainPrice(bargain.Villager, bargain.Price);

        }).Run();

        // Forbidden knowledge corruption over time
        Entities.ForEach((ref VillagerAlignment alignment, in ForbiddenKnowledge knowledge) =>
        {
            // Gradual alignment drift
            alignment.MoralAxis -= (sbyte)(knowledge.CorruptionRate * 100 * Time.DeltaTime);
            alignment.PurityAxis -= (sbyte)(knowledge.CorruptionRate * 50 * Time.DeltaTime);

            // Spread to guild members (if contagious)
            if (knowledge.Contagious)
            {
                var guild = GetComponent<GuildMembership>(entity);
                if (guild.Guild != Entity.Null)
                {
                    SpreadKnowledgeToGuild(guild.Guild, knowledge);
                }
            }

        }).Run();
    }
}
```

---

## Alignment-Based Outcomes

### Good Alignment: Communion & Purification

```csharp
// Good villagers commune with spirits, purify corruption
public static void ResolveGoodEncounter(Entity villager, Entity threat)
{
    var alignment = GetComponent<VillagerAlignment>(villager);
    var threatType = GetComponent<MalevolentEntity>(threat).Type;

    if (alignment.MoralAxis > 50)
    {
        // Good-aligned options:
        // 1. Peaceful communion with benevolent spirits
        // 2. Exorcism of malevolent entities
        // 3. Purification of corrupted land
        // 4. Helping spirits find closure

        if (threatType == ThreatType.BenevolentSpirit)
        {
            CommuneWithSpirit(villager, threat);  // Learn, help, fulfill quest
        }
        else if (threatType == ThreatType.Haunt || threatType == ThreatType.Wraith)
        {
            ExorciseEntity(villager, threat);     // Banish peacefully
        }
        else
        {
            CombatWithRighteousFury(villager, threat);  // Fight evil
        }
    }
}
```

### Evil Alignment: Enslavement & Bargains

```csharp
// Evil villagers enslave undead, bargain with demons
public static void ResolveEvilEncounter(Entity villager, Entity threat)
{
    var alignment = GetComponent<VillagerAlignment>(villager);
    var threatType = GetComponent<MalevolentEntity>(threat).Type;

    if (alignment.MoralAxis < -50)
    {
        // Evil-aligned options:
        // 1. Enslave undead for labor
        // 2. Bargain with demons for power
        // 3. Embrace corruption (join threat)
        // 4. Resurrect old gods/deities

        if (threatType == ThreatType.Demon)
        {
            BargainWithDemon(villager, threat);   // Gain forbidden knowledge
        }
        else if (threatType == ThreatType.UndeadArmy || threatType == ThreatType.Lich)
        {
            EnslaveUndead(villager, threat);      // Use as workers/soldiers
        }
        else if (threatType == ThreatType.DimensionalHorror)
        {
            AttemptResurrection(villager, threat);  // Resurrect old god
        }
    }
}
```

### Neutral/Opportunist: Pragmatic Solutions

```csharp
// Neutral villagers adapt based on threat/reward
public static void ResolveNeutralEncounter(Entity villager, Entity threat)
{
    var alignment = GetComponent<VillagerAlignment>(villager);
    var threatPower = GetComponent<MalevolentEntity>(threat).PowerLevel;

    if (alignment.AlignmentStrength < 0.5f)  // Weak convictions
    {
        // Opportunist options:
        // 1. If threat weak: Combat/exorcise
        // 2. If threat strong: Bargain/flee
        // 3. If profitable: Enslave/use
        // 4. If dangerous: Avoid/ignore

        if (threatPower < 0.3f)
        {
            CombatEncounter(villager, threat);  // Easy fight, take loot
        }
        else if (threatPower > 0.7f)
        {
            if (CanBargain(threat))
            {
                BargainForPower(villager, threat);  // Opportunistic deal
            }
            else
            {
                FleeEncounter(villager, threat);    // Too dangerous
            }
        }
    }
}
```

---

## Morale & Mood Impact

### Individual Responses

```csharp
public struct EncounterMoraleImpact : IComponentData
{
    public Entity Villager;
    public ThreatType EncounteredThreat;
    public float MoraleChange;           // -30 to +30
    public float MoodChange;             // -30 to +30
    public bool TriggeredFear;           // Phobia/trauma
}

// Morale impact varies by individual alignment/traits
public static float CalculateMoraleImpact(Entity villager, ThreatType threat)
{
    var alignment = GetComponent<VillagerAlignment>(villager);
    var behavior = GetComponent<VillagerBehavior>(villager);

    float moraleDelta = 0f;

    switch (threat)
    {
        case ThreatType.Haunt:
            // Craven: −20 morale (terrified)
            // Bold: −5 morale (unsettled but brave)
            // Priest: +10 morale (purpose/calling)
            moraleDelta = behavior.BoldScore < 0 ? -20f : (behavior.BoldScore > 50 ? -5f : -10f);
            break;

        case ThreatType.Demon:
            // Good: −30 morale (existential threat)
            // Evil: +10 morale (opportunity)
            moraleDelta = alignment.MoralAxis > 50 ? -30f : (alignment.MoralAxis < -50 ? +10f : -15f);
            break;

        case ThreatType.BenevolentSpirit:
            // Shaman: +20 morale (communion opportunity)
            // Normal villager: +5 morale (ancestors watching)
            var villagerClass = GetComponent<VillagerClass>(villager);
            moraleDelta = villagerClass == VillagerClass.Shaman ? +20f : +5f;
            break;
    }

    return moraleDelta;
}
```

### Aggregate Responses

```csharp
public struct AggregateThreatResponse : IComponentData
{
    public Entity Aggregate;
    public ThreatType Threat;
    public AggregateReaction Reaction;
    public float CohesionChange;         // -0.3 to +0.3
}

public enum AggregateReaction : byte
{
    Panic,                    // Cohesion collapse, flee
    Rally,                    // Cohesion increase, fight together
    Split,                    // Moral conflict, splintering
    Embrace,                  // Evil aggregates join threat
    Adapt,                    // Pragmatic response
}

// Aggregate reaction based on average alignment
public static AggregateReaction DetermineAggregateReaction(Entity aggregate, ThreatType threat)
{
    var members = GetComponent<DynamicBuffer<AggregateMembers>>(aggregate);
    var avgAlignment = CalculateAverageAlignment(members);
    var cohesion = GetComponent<AggregateCohesion>(aggregate).Value;

    // Low cohesion → panic
    if (cohesion < 0.3f)
    {
        return AggregateReaction.Panic;
    }

    // Good aggregate facing demons → rally
    if (avgAlignment.MoralAxis > 50 && (threat == ThreatType.Demon || threat == ThreatType.Lich))
    {
        return AggregateReaction.Rally;
    }

    // Evil aggregate facing demons → embrace
    if (avgAlignment.MoralAxis < -50 && threat == ThreatType.Demon)
    {
        return AggregateReaction.Embrace;
    }

    // High alignment variance → split (moral conflict)
    if (CalculateAlignmentVariance(members) > 0.6f)
    {
        return AggregateReaction.Split;
    }

    // Default: adapt
    return AggregateReaction.Adapt;
}
```

---

## Space4X Adaptation

### Space Encounters

```csharp
public enum SpaceEncounterType : byte
{
    // Flora & Fauna
    SporeCloud,               // Asteroid belt life
    SpaceWhale,               // Giant space creature
    CrystallineEntity,        // Silicon-based life

    // Derelicts
    AbandonedStation,         // Ghost station
    DerelictHulk,             // Dead ship, infestation
    AncientWreck,             // Precursor tech

    // Anomalies
    WormholeStable,           // Permanent wormhole
    WormholeUnstable,         // Random portal
    TemporalRift,             // Time anomaly
    DarkMatterVortex,         // Gravity/physics distortion

    // Hostile
    PirateNest,               // Bandit hideout
    AlienHive,                // Hostile xenos
    RogueAI,                  // Malfunctioning AI
    VoidHorror,               // Lovecraftian space entity
}

public struct SpaceEncounter : IComponentData
{
    public SpaceEncounterType Type;
    public float3 Position;
    public float ThreatLevel;            // 0-1
    public bool IsHostile;
    public bool CanNegotiate;
    public DynamicBuffer<LootTable> PossibleRewards;
}
```

### Captain-Based Resolution

```csharp
public struct CaptainEncounterChoice : IComponentData
{
    public Entity Captain;
    public Entity Encounter;
    public ResolutionMethod Method;
    public float SuccessProbability;
}

public enum ResolutionMethod : byte
{
    Combat,                   // Fight
    Diplomacy,                // Negotiate
    Science,                  // Study/analyze
    Stealth,                  // Avoid detection
    Salvage,                  // Loot and leave
}

// Captain personality influences choice
public static ResolutionMethod DetermineCaptainChoice(Entity captain, SpaceEncounter encounter)
{
    var alignment = GetComponent<IndividualAlignment>(captain);  // Space4X uses Individual, not Villager
    var skills = GetComponent<CaptainSkills>(captain);

    if (encounter.IsHostile)
    {
        // Bold captains prefer combat
        if (alignment.BoldScore > 60)
        {
            return ResolutionMethod.Combat;
        }

        // Scholarly captains prefer study
        if (skills.ScienceSkill > 70)
        {
            return ResolutionMethod.Science;
        }

        // Opportunists prefer salvage
        if (alignment.AlignmentStrength < 0.4f)
        {
            return ResolutionMethod.Salvage;
        }
    }
    else if (encounter.CanNegotiate)
    {
        // Lawful captains prefer diplomacy
        if (alignment.OrderAxis > 50)
        {
            return ResolutionMethod.Diplomacy;
        }
    }

    // Default: stealth (avoid)
    return ResolutionMethod.Stealth;
}
```

### Derelict Infestation

```csharp
public struct DerelictInfestation : IComponentData
{
    public Entity DerelictShip;
    public InfestationType Type;
    public int InfestationLevel;         // 0-100
    public DynamicBuffer<Entity> Hostiles;
    public bool HasValuableCargo;
    public bool HasAncientTech;
}

public enum InfestationType : byte
{
    SpaceVermin,              // Space rats, parasites
    AlienNest,                // Hostile xenos breeding
    RogueRobots,              // Malfunctioning crew bots
    GhostShip,                // Haunted by dead crew
    VoidInfestation,          // Lovecraftian corruption
}

// Boarding party composition matters
public struct DerelictBoardingParty : IComponentData
{
    public DynamicBuffer<Entity> CrewMembers;
    public int Marines;
    public int Engineers;
    public int Scientists;
    public float ClearanceSuccessRate;   // Based on composition
}

public static float CalculateDerelictClearance(DerelictBoardingParty party, InfestationType infestation)
{
    float successRate = 0.5f;  // Base

    switch (infestation)
    {
        case InfestationType.SpaceVermin:
            // Marines effective
            successRate += party.Marines * 0.1f;
            break;

        case InfestationType.RogueRobots:
            // Engineers can disable
            successRate += party.Engineers * 0.15f;
            break;

        case InfestationType.GhostShip:
            // Scientists understand phenomenon
            successRate += party.Scientists * 0.2f;
            break;

        case InfestationType.VoidInfestation:
            // Very difficult, need all hands
            successRate += (party.Marines + party.Engineers + party.Scientists) * 0.05f;
            break;
    }

    return math.clamp(successRate, 0.1f, 0.95f);
}
```

---

## Quest Structure

### Quest Definition

```csharp
public struct QuestDefinition : IComponentData
{
    public FixedString64Bytes QuestID;
    public QuestType Type;
    public QuestGiver Giver;
    public QuestObjective Objective;
    public DynamicBuffer<QuestReward> Rewards;
    public DynamicBuffer<QuestRequirement> Requirements;
    public uint TimeLimit;               // 0 = no limit
    public bool IsPersistent;            // Remains until completed
    public float DifficultyRating;       // 0-1
}

public enum QuestType : byte
{
    // Benevolent
    ReturnHeirloom,
    FulfillPromise,
    HonorableDuel,
    LearnFromSpirit,

    // Malevolent
    ExorciseHaunt,
    DefeatLich,
    ClosePortal,
    PurifyCorruption,

    // Neutral
    InvestigateAnomaly,
    SalvageDerelict,
    NegotiateWithEntity,
    SecureBorder,
}

public struct QuestGiver : IComponentData
{
    public Entity Entity;                // Spirit, demon, or village elder
    public QuestGiverType Type;
    public float TrustLevel;             // 0-1 (can giver be trusted?)
}

public enum QuestGiverType : byte
{
    BenevolentSpirit,
    MalevolentEntity,
    VillageElder,
    GuildMaster,
    DemonicEntity,
    AncientAI,                // Space4X
    AlienAmbassador,          // Space4X
}
```

### Quest Progression

```csharp
public struct QuestProgress : IComponentData
{
    public Entity QuestEntity;
    public QuestState State;
    public float CompletionProgress;     // 0-1
    public DynamicBuffer<Entity> PartyMembers;
    public uint StartTick;
    public uint LastUpdateTick;
}

public enum QuestState : byte
{
    NotStarted,
    InProgress,
    Completed,
    Failed,
    Abandoned,
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class QuestProgressionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref QuestProgress progress, in QuestDefinition quest) =>
        {
            switch (quest.Type)
            {
                case QuestType.ReturnHeirloom:
                    // Check if heirloom delivered to target
                    if (IsHeirloomDelivered(quest.Objective))
                    {
                        progress.State = QuestState.Completed;
                        GrantRewards(quest.Rewards, progress.PartyMembers);
                    }
                    break;

                case QuestType.ExorciseHaunt:
                    // Check if haunt defeated/banished
                    if (IsHauntDefeated(quest.Objective.TargetEntity))
                    {
                        progress.State = QuestState.Completed;
                        GrantRewards(quest.Rewards, progress.PartyMembers);

                        // Reduce corruption at site
                        ReduceCorruption(quest.Objective.TargetLocation, 0.5f);
                    }
                    break;

                case QuestType.SecureBorder:
                    // Check if border threats cleared
                    var threatsRemaining = CountThreatsInZone(quest.Objective.TargetLocation, 100f);
                    progress.CompletionProgress = 1f - (threatsRemaining / 10f);  // Assume 10 threats

                    if (threatsRemaining == 0)
                    {
                        progress.State = QuestState.Completed;
                        GrantRewards(quest.Rewards, progress.PartyMembers);
                    }
                    break;
            }

            // Time limit check
            if (quest.TimeLimit > 0 && CurrentTick - progress.StartTick > quest.TimeLimit)
            {
                progress.State = QuestState.Failed;
            }

        }).Run();
    }
}
```

### Quest Rewards

```csharp
public struct QuestReward : IBufferElementData
{
    public RewardType Type;
    public float Value;
    public Entity TargetEntity;          // If reward is entity-specific
}

public enum RewardType : byte
{
    // Material
    Gold,
    Resources,
    Equipment,
    AncientTech,              // Space4X

    // Knowledge
    SkillIncrease,
    ForbiddenKnowledge,
    SpellSignature,
    TechBlueprint,            // Space4X

    // Social
    Reputation,
    AlignmentShift,
    FactionRelations,

    // Supernatural
    Blessing,                 // Benevolent spirit buff
    Curse,                    // Malevolent entity debuff
    SpiritAlly,               // Permanent spirit companion
}

// Reward distribution
public static void GrantRewards(DynamicBuffer<QuestReward> rewards, DynamicBuffer<Entity> party)
{
    foreach (var reward in rewards)
    {
        switch (reward.Type)
        {
            case RewardType.SkillIncrease:
                // Distribute skill XP to party
                foreach (var member in party)
                {
                    var skills = GetComponent<VillagerSkills>(member);
                    skills.XP += reward.Value / party.Length;  // Split XP
                }
                break;

            case RewardType.ForbiddenKnowledge:
                // Only one party member learns (highest skill)
                var scholar = GetMostScholarlyMember(party);
                GrantForbiddenKnowledge(scholar, reward);
                break;

            case RewardType.Blessing:
                // Apply buff to all party members
                foreach (var member in party)
                {
                    ApplyBlessing(member, reward.Value);
                }
                break;

            case RewardType.Reputation:
                // Reputation to quest leader
                var leader = party[0];
                var reputation = GetComponent<IndividualReputation>(leader);
                reputation.Value += reward.Value;
                break;
        }
    }
}
```

---

## Integration with Existing Systems

### Knowledge Transmission

- Dead villagers persist as spirits with retained skills
- Shamans commune to learn from ancestral spirits
- Necromancers interrogate enslaved undead for secrets
- Demons offer forbidden knowledge (alignment cost)

### Labor Economy

- Enslaved undead work as miners, haulers, refiners
- High-level undead (liches) perform research
- Demons serve on councils (if alignment permits)
- Ghost workers require no food but drain morale

### Alignment & Moral Conflict

- Good villagers refuse demonic bargains (moral conflict)
- Evil villagers embrace corruption (alignment-gated quests)
- Neutral villagers pragmatic (choose based on reward/risk)
- Alignment drift from forbidden knowledge

### Guild Systems

- Priest guilds specialize in exorcism
- Necromancer guilds manage undead labor
- Shaman guilds commune with spirit world
- Knowledge spreads through guild (including forbidden)

### Territorial Control

- Villages expand by securing borders (peacekeepers)
- Walls and defenses push back encroachment
- Light vs. darkness (influence radius vs. spawn zones)
- Alignment determines expansion strategy

---

## Implementation Checklist

- [ ] Define `EnvironmentalCorruption` component with trigger types
- [ ] Implement corruption accumulation systems (combat, deforestation, mining)
- [ ] Create spawn catalog (50+ spirit/threat types)
- [ ] Define class effectiveness matrix for quest acceptance
- [ ] Implement territorial influence and encroachment mechanics
- [ ] Create peacekeeper patrol behavior (border security)
- [ ] Implement village expansion based on alignment
- [ ] Add spirit communion system (skill/knowledge transfer)
- [ ] Add necromancer enslavement system (undead labor)
- [ ] Add demonic bargain system (forbidden knowledge)
- [ ] Create quest definition and progression systems
- [ ] Implement morale/mood impact per encounter type
- [ ] Add aggregate threat response (panic, rally, split)
- [ ] Create loot tables and reward distribution
- [ ] Integrate with existing knowledge/guild systems
- [ ] Adapt for Space4X (derelicts, anomalies, captain choices)
- [ ] Add UI for quest tracking and party composition
- [ ] Test class asymmetry (priest vs. warrior effectiveness)
- [ ] Balance corruption thresholds and spawn rates

---

## Example Scenarios

### Scenario 1: Haunted Cemetery (Godgame)

**Setup**:
- Village suffered plague, 50 deaths in cemetery
- Blood-soaked ground corruption at 0.8
- Benevolent spirits seek closure, malevolent spirits angry

**Spawns**:
- 3x Benevolent spirits (want heirlooms returned)
- 2x Haunts (angry at abandonment)
- 1x Wraith (plague victim seeking revenge)

**Party Composition**:
- 2x Priest (effective vs. undead)
- 1x Shaman (can commune with spirits)
- 1x Warrior (backup, weak vs. ethereal)

**Resolution**:
1. Shaman communes with benevolent spirits, learns about heirlooms
2. Party retrieves heirlooms from village
3. Benevolent spirits granted peace, leave cemetery
4. Priests exorcise haunts (easy)
5. Wraith requires combat (difficult)
6. Corruption reduced to 0.3, cemetery secured

**Rewards**:
- Spirits teach skills (carpentry, herbalism)
- Reputation +20 ("Cemetery Purifier")
- Village morale +15 (ancestors at peace)

---

### Scenario 2: Forest Haunting (Godgame)

**Setup**:
- Village aggressively logged forest (70% deforestation)
- Forest hauntings at corruption 0.9
- Corrupted treants spawn

**Spawns**:
- 5x Corrupted Treants (territorial, angry)
- 1x Forest Spirit (malevolent, seeks revenge)

**Party Composition**:
- 3x Warriors (effective vs. physical treants)
- 1x Druid (can negotiate with forest spirit)

**Resolution Path A: Combat**
1. Warriors engage treants (high effectiveness)
2. Treants defeated but forest spirit escapes
3. Corruption persists at 0.6 (not resolved)
4. Forest continues spawning threats

**Resolution Path B: Negotiation**
1. Druid negotiates with forest spirit
2. Spirit demands: Stop logging, replant 100 trees
3. Village agrees, begins reforestation
4. Spirit grants blessing (+10% harvest yield)
5. Corruption reduces to 0.2 over 2000 ticks

**Outcome**: Path B superior long-term

---

### Scenario 3: Demonic Bargain (Godgame)

**Setup**:
- Necromancer discovers demon portal in mine
- Demon offers forbidden knowledge (animate dead)
- Necromancer must decide: accept or report

**Alignment Check**:
- Necromancer alignment: Moral −60, Order −20, Purity −40 (Evil Chaotic)
- Accepts bargain (no moral conflict)

**Bargain**:
- Knowledge: Advanced necromancy (+30% effectiveness)
- Price: Serve demon for 3000 ticks, spread corruption
- Alignment shift: Moral −20 (becomes −80)

**Consequences**:
1. Necromancer gains power, enslaves 10 undead
2. Undead work in mines (+40% ore production)
3. Village morale −20 ("Undead Workers" debuff)
4. Good-aligned villagers leave (splintering risk)
5. Demon portal grows (regional threat)
6. After 3000 ticks: Necromancer can betray demon or fulfill pact

**Long-term**: Village becomes evil necropolis or crushed by demon invasion

---

### Scenario 4: Derelict Salvage (Space4X)

**Setup**:
- Ancient derelict detected on scanners
- Infestation: Rogue robots (level 60)
- Valuable cargo + ancient tech blueprint

**Captain Alignment**:
- Moral 20 (Neutral), Order 70 (Lawful), Bold 80 (Heroic)
- High engineering skill (75)

**Boarding Party**:
- 4x Marines
- 3x Engineers
- 1x Scientist

**Resolution**:
1. Engineers disable robot command node (75% success)
2. Success: Robots deactivated
3. Marines secure cargo bay
4. Scientist analyzes ancient tech
5. Salvage: 500 tons cargo, tech blueprint (jump drive upgrade)

**Rewards**:
- Tech blueprint: Micro-jump range +20%
- Cargo value: 10,000 credits
- Reputation: +15 ("Derelict Raider")

---

## Evil Experimentation and Counter-Quest System

**Purpose**: Evil and Chaotic factions engage in forbidden activities (grafting limbs, creating undead patchworks, breeding horrors, summoning entities) that trigger counter-quests for Good and Lawful factions. These create asymmetric conflicts outside villages or within guilds, with outcomes ranging from heroic near-miss to apocalyptic invasion.

**Key Mechanics**:
- Evil/Chaotic alignment-gated experiments spawn threats
- Good/Lawful factions discover schemes and mobilize counter-quests
- Guilds/bands can act independently (uncover plot, decide to intervene)
- Outcome escalation based on intervention success/failure
- Time pressure creates urgency (stop ritual before completion)

---

### Evil Experimentation Types

#### Grafting Limbs

**Alignment Gate**: Evil (Moral < −40) or Chaotic (Order < −30)

**Actors**: Necromancers, dark surgeons, mad scientists, disciples, apprentices

```csharp
public struct LimbGraftingExperiment : IComponentData
{
    public Entity Experimenter;           // Necromancer/surgeon performing graft
    public Entity Subject;                // Unwilling subject (kidnapped villager)
    public LimbType GraftedLimb;          // Extra arm, tentacle, wing, etc.
    public float ExperimentProgress;      // 0-1 (completion %)
    public uint StartTick;
    public bool SubjectWilling;           // False = kidnapped/enslaved
    public float MortalityRisk;           // 0-1 (chance of death during procedure)
}

public struct GraftedLimb : IComponentData
{
    public Entity Owner;
    public LimbType Type;                 // ExtraArm, Tentacle, Wing, ClawedHand
    public float Functionality;           // 0-1 (how well integrated)
    public bool Painful;                  // Constant pain debuff
    public float AlignmentDriftRate;      // Grafts slowly shift alignment Evil
}
```

**Experiment Outcomes**:
- **Success (40%)**: Subject gains extra limb (e.g., 3rd arm for dual-wielding bows), alignment shift Evil −15
- **Partial (30%)**: Limb grafted but dysfunctional (50% effectiveness), chronic pain morale −20
- **Failure (20%)**: Subject dies, corpse becomes undead patchwork material
- **Catastrophic (10%)**: Subject transforms into abomination (hostile entity), experimenter at risk

**Counter-Quest Trigger**: Good villagers notice missing persons (kidnapping detection), investigate, discover lab

---

#### Undead Patchwork Creation

**Alignment Gate**: Evil (Moral < −50), Necromancer class

**Actors**: Necromancers, liches, apprentices, servants

```csharp
public struct UndeadPatchwork : IComponentData
{
    public Entity Creator;
    public DynamicBuffer<Entity> CorpseComponents;  // Body parts from multiple corpses
    public float CombatEffectiveness;               // 0-3 (can exceed normal undead)
    public bool Unstable;                           // Risk of going rogue
    public float ObedienceLevel;                    // 0-1 (revolt risk)
    public PatchworkType Type;                      // Brute, Assassin, Scholar, Abomination
}

public enum PatchworkType : byte
{
    Brute,           // 4 arms, enhanced strength
    Assassin,        // Extra legs, spider-climb, stealth
    Scholar,         // Multiple heads, enhanced intellect
    Abomination,     // Random horrific mix, unstable
}
```

**Creation Process**:
1. Collect 3-8 corpses (battlefields, cemeteries, kidnapped villagers)
2. Surgical assembly (1000 ticks, high necromancy skill required)
3. Reanimation ritual (500 ticks, mana cost)
4. Obedience binding (necromancer must maintain control)

**Risks**:
- Unstable patchworks revolt if creator dies or skill drops (30% chance)
- Good villagers horrified if discovered (morale −40, "Abomination Witnessed")
- Corruption spreads from creation site (0.02 per tick)

**Counter-Quest**: Destroy patchworks, shut down necromancer lab, rescue kidnapped victims

---

#### Breeding Horrors (Animals/Monsters)

**Alignment Gate**: Evil (Moral < −30) or Chaotic (Order < −40)

**Actors**: Dark druids, mad breeders, chaos cultists, evil scientists

```csharp
public struct BreedingProgram : IComponentData
{
    public Entity Overseer;
    public BreedingType Type;              // Chimera, DireWolf, GiantSpider, etc.
    public int GenerationCount;            // Generations bred
    public float MutationRate;             // 0-1 (unstable = higher)
    public DynamicBuffer<Entity> Offspring;
    public bool ContainmentBreach;         // Horrors escaped
}

public enum BreedingType : byte
{
    Chimera,         // Multi-animal hybrid (lion/goat/snake)
    DireWolf,        // Enlarged aggressive wolf
    GiantSpider,     // House-sized spider
    Basilisk,        // Petrifying serpent
    Manticore,       // Lion/scorpion/bat hybrid
}
```

**Breeding Outcomes**:
- **Generation 1-3**: Mild enhancements (2x size, +20% damage)
- **Generation 4-6**: Significant mutations (new abilities, instability)
- **Generation 7+**: Unstable abominations (50% chance of breach)

**Containment Breach**:
- Horrors escape breeding pens (30% chance at Gen 7+)
- Attack nearby villagers (threat level based on generation)
- Spread into wilderness (random encounters for travelers)
- Reproduce in wild (permanent threat unless exterminated)

**Counter-Quest**: Exterminate breeding stock, burn facility, hunt escaped horrors

---

#### Conjuring Demons

**Alignment Gate**: Evil (Moral < −60), specific demon-aligned class or dark knowledge

**Actors**: Demonologists, warlocks, evil priests, chaos cultists

```csharp
public struct DemonConjuration : IComponentData
{
    public Entity Summoner;
    public DemonType TargetDemon;          // Imp, Demon, DemonLord
    public float RitualProgress;           // 0-1 (completion %)
    public uint RitualStartTick;
    public int SacrificeCount;             // Villagers/animals sacrificed
    public bool BindingCircleIntact;       // False = demon uncontrolled
    public float ControlDifficulty;        // 0-1 (higher = harder to bind)
}

public enum DemonType : byte
{
    Imp,             // Minor demon (weak, easy to control)
    Demon,           // Major demon (powerful, hard to control)
    DemonLord,       // Apocalyptic entity (near-impossible to control)
}
```

**Conjuration Process**:
1. Prepare ritual site (300 ticks)
2. Gather sacrifices (Imp: 1, Demon: 5, DemonLord: 20)
3. Perform summoning ritual (500-2000 ticks)
4. Bind demon to circle (skill check, failure = unbound demon)
5. Compel service (negotiation or enslavement)

**Binding Failure**:
- Imp (20% failure): Demon flees, minor threat
- Demon (50% failure): Demon rampages, kills summoner, regional threat
- DemonLord (80% failure): Demon opens portal, invasion begins

**Counter-Quest Timeline**:
- **Early Detection (0-30% progress)**: Disrupt ritual, prevent summoning (easy)
- **Mid-Ritual (30-70% progress)**: Interrupt before demon manifests (moderate)
- **Near Completion (70-99% progress)**: Desperate battle to stop summoning (hard)
- **Post-Summoning (100%)**: Slay demon or close portal (very hard)

---

#### Summoning Otherworldly Entities

**Alignment Gate**: Chaotic (Order < −50), or corrupted knowledge (Idea Parasite)

**Actors**: Chaos cultists, mad scholars, possessed villagers, idea-infected

```csharp
public struct OtherworldlySummoning : IComponentData
{
    public Entity Summoner;
    public EntityType TargetEntity;        // Lovecraftian horror, void spawn, elder thing
    public float RealityThinness;          // 0-1 (how close to breakthrough)
    public uint SummoningDuration;         // Ticks ritual has been active
    public int MindsBroken;                // Witnesses driven mad
    public float CorruptionRadius;         // Growing corruption from ritual
}

public enum EntityType : byte
{
    VoidSpawn,       // Shapeless darkness entity
    ElderThing,      // Ancient precursor horror
    DimensionalHorror,  // Reality-warping entity
    IdeaParasite,    // Memetic hazard (spreads as concept)
}
```

**Summoning Effects**:
- Reality weakens around ritual site (CorruptionRadius expands)
- Witnesses suffer sanity damage (morale −50, risk of madness)
- Corruption spreads as idea/meme (IdeaParasite infects minds)
- Entities warp physics (gravity anomalies, time dilation)

**Intervention Difficulty**:
- Early (0-40% thinness): Disrupt ritual, seal breach (moderate)
- Advanced (40-80% thinness): Entity partially manifested, reality unstable (hard)
- Breakthrough (80-100% thinness): Entity fully manifested, apocalyptic (near-impossible)

**Counter-Quest**: Close rift, banish entity, restore reality, cure idea infection

---

### Counter-Quest System

**Detection Mechanics**:

```csharp
public struct CounterQuestDetection : IComponentData
{
    public Entity DetectingFaction;        // Good/Lawful village or guild
    public Entity EvilScheme;              // Grafting lab, breeding pen, summoning site
    public float DetectionProgress;        // 0-1 (investigation progress)
    public DetectionMethod Method;
    public uint DetectionTick;
}

public enum DetectionMethod : byte
{
    MissingPersons,   // Kidnapping noticed (periodic checks)
    ScoutingParty,    // Peacekeepers patrol discovers site
    SurvivorReport,   // Escaped victim reports horror
    DivinationRitual, // Priest/shaman senses corruption
    RandomEncounter,  // Band stumbles upon site
}
```

**Detection Triggers**:
- **Missing Persons**: 3+ villagers kidnapped triggers investigation (Good villages only)
- **Corruption Spikes**: Rapid corruption growth (0.1+ per 100 ticks) alerts shamans
- **Survivor Escape**: 20% chance kidnapped victim escapes, reports scheme
- **Peacekeeper Patrol**: 10% chance per patrol to discover hidden site
- **Band Discovery**: Adventuring bands have 15% chance to stumble upon site

**Counter-Quest Mobilization**:

```csharp
public struct CounterQuest : IComponentData
{
    public Entity QuestGiver;              // Village elder, priest, guild leader
    public Entity TargetScheme;            // Evil experiment to stop
    public QuestUrgency Urgency;
    public uint TimeRemaining;             // Ticks until ritual completes
    public float SuccessChance;            // Based on party strength vs. threat
    public DynamicBuffer<Entity> Volunteers;  // Villagers joining quest
}

public enum QuestUrgency : byte
{
    Routine,         // No time pressure (breeding pen discovered early)
    Urgent,          // Limited time (demon ritual 50% complete)
    Critical,        // Desperate (summoning 90% complete, hours remaining)
    Apocalyptic,     // World-ending (entity breakthrough imminent)
}
```

**Quest Actors**:
- **Village-Wide**: Elder calls all capable fighters (Good villages rally)
- **Guild-Based**: Specific guild (Warriors, Priests) mobilizes members
- **Band Initiative**: Independent band discovers and decides to act alone
- **Player Miracle**: Player directly intervenes (Divine Sniper, time stop, etc.)

**Guild/Band Independent Action**:

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class BandIndependentQuestSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Bands can discover schemes independently and act
        Entities.ForEach((Entity band, ref BandComponents bandData, in LocalTransform transform) =>
        {
            // Discovery check (while traveling/exploring)
            var nearbySchemes = GetSchemesInRadius(transform.Position, 100f);
            if (nearbySchemes.Length > 0)
            {
                var scheme = nearbySchemes[0];
                var schemeData = GetComponent<EvilScheme>(scheme);

                // Band decides whether to intervene
                bool shouldIntervene = BandDecisionLogic(bandData, schemeData);

                if (shouldIntervene)
                {
                    // Band acts without village approval
                    CreateCounterQuest(band, scheme, QuestUrgency.Urgent);

                    // Reputation impact based on outcome
                    // Success: +30 regional reputation ("Heroes")
                    // Failure: −10 ("Overreached")
                }
            }

        }).Run();
    }

    private bool BandDecisionLogic(BandComponents band, EvilScheme scheme)
    {
        // Bold bands more likely to intervene
        if (band.LeaderOutlook == VillagerOutlook.Bold) return true;

        // Good-aligned bands intervene against Evil
        if (band.AverageAlignment.MoralAxis > 30 && scheme.AlignmentType == AlignmentType.Evil) return true;

        // Craven bands avoid (unless overwhelming strength)
        if (band.LeaderOutlook == VillagerOutlook.Craven && band.CombatStrength < scheme.ThreatLevel * 2) return false;

        // Default: 50% chance if moderate strength
        return band.CombatStrength > scheme.ThreatLevel * 0.8f && Random.NextFloat() > 0.5f;
    }
}
```

---

### Outcome Escalation Tiers

**Tier 1: Near Miss (Heroic Victory)**

**Preconditions**:
- Counter-quest intervenes early (0-40% ritual progress)
- Party strength > threat level (competent heroes)
- Time remaining > 500 ticks (not rushed)

**Outcomes**:
- Ritual disrupted, evil actors killed/captured
- Kidnapped victims rescued (70% survival rate)
- Corruption cleared to 0.2 (minor residual taint)
- Regional reputation +40 ("Saviors of the Realm")
- Band/guild morale +30 ("Heroic Triumph")
- Loot: Forbidden knowledge texts (can be destroyed or studied)

**Narrative**: "A band of unlikely heroes stumbled upon the scheme and stopped it just in time."

---

**Tier 2: Partial Success (Costly Victory)**

**Preconditions**:
- Counter-quest intervenes mid-ritual (40-70% progress)
- Party strength ≈ threat level (close match)
- Time remaining 200-500 ticks (moderate pressure)

**Outcomes**:
- Ritual stopped but partial manifestation occurred (Imp/minor demon spawned)
- Demon/horror escapes into wilderness (regional threat remains)
- 40% kidnapped victims saved, 60% died
- Corruption reduced to 0.5 (significant taint remains)
- Regional reputation +20 ("Tried Their Best")
- Party casualties (30% death rate)
- Loot: Partial tech/knowledge (incomplete scrolls)

**Narrative**: "The heroes arrived in time to prevent catastrophe, but at great cost."

---

**Tier 3: Pyrrhic Victory (Apocalypse Averted, Barely)**

**Preconditions**:
- Counter-quest intervenes late (70-95% progress)
- Party strength < threat level (desperate fight)
- Time remaining < 200 ticks (critical pressure)

**Outcomes**:
- Major demon/horror fully manifested, defeated after devastating battle
- 80% kidnapped victims dead
- Corruption at 0.8 (permanent taint, ongoing effects)
- Regional morale −30 ("Scarred by Horror")
- Party casualties (60% death rate), survivors traumatized
- Entity defeated but portal/corruption remains (requires follow-up)
- Loot: Powerful cursed artifacts (dangerous to use)

**Narrative**: "They stopped the invasion, but the scars will never heal."

---

**Tier 4: Failure (Partial Invasion)**

**Preconditions**:
- No intervention or intervention failed (95-99% progress)
- Party wiped out or routed
- Demon/horror fully manifested and uncontained

**Outcomes**:
- Demon/horror establishes regional presence (corrupted zone)
- All kidnapped victims dead or transformed
- Corruption at 1.0 (maximum, spreading to neighboring regions)
- Regional population −50% (deaths, refugees)
- Portal remains open (ongoing demon spawns)
- Requires major coalition to defeat (multi-village army)

**Narrative**: "The heroes failed, and now the nightmare spreads unchecked."

---

**Tier 5: Apocalypse (Full Invasion)**

**Preconditions**:
- DemonLord summoning completes (100% progress)
- No intervention or all attempts failed
- Reality breach fully manifested

**Outcomes**:
- DemonLord opens permanent portal to demon realm
- 10-100 demons invade per 1000 ticks
- Regional population −90% (mass death)
- Multiple villages destroyed
- Corruption spreads globally (0.01 per tick across world)
- Requires player miracle intervention (Divine Sniper, time rewind, etc.)
- Game-ending scenario if not stopped within 5000 ticks

**Narrative**: "The world burns, and only divine intervention can save it now."

---

### Cross-Faction Conflict Examples

#### Scenario 1: Band Uncovers Grafting Lab

**Setup**:
- Adventuring band (5 members: 2 warriors, 1 rogue, 1 priest, 1 mage)
- Discovers necromancer grafting lab while exploring mine
- 3 kidnapped villagers inside (2 alive, 1 dead)
- Necromancer is 60% through grafting 3rd arm onto victim

**Band Decision**:
- Leader is Bold (willing to fight)
- Priest detects Evil corruption (Moral −70 necromancer)
- Band decides to intervene (no time to fetch village reinforcements)

**Combat**:
- Necromancer + 4 undead servants vs. 5-member band
- Band strength: 18, Threat level: 15 (slight advantage)
- Combat resolved (Near Miss tier)

**Outcome**:
- Necromancer killed, undead destroyed
- 2 victims rescued (1 partially grafted, requires surgery)
- Corruption cleared to 0.3
- Band reputation +35 ("Unlikely Heroes")
- Village rewards band with 500 gold, honorary titles

---

#### Scenario 2: Guild Internal Conflict

**Setup**:
- Mage's Guild member discovers demon summoning ritual in guildhall basement
- Summoner is senior guild member (high rank, Evil alignment)
- Guild master unaware (Neutral alignment, trusts senior member)
- Ritual is 75% complete (Demon summoning)

**Detection**:
- Junior mage stumbles upon ritual chamber (random encounter)
- Reports to guild master immediately

**Guild Split**:
- Good-aligned members demand immediate intervention (50% of guild)
- Evil-aligned members support summoner (20% of guild)
- Neutral members hesitant (30% of guild, follow guild master)

**Resolution**:
- Guild master decides to stop summoning (Lawful Order alignment)
- Internal battle: Good + Neutral vs. Evil faction
- Evil faction loses, summoner killed at 80% progress
- Demon partially manifests, guild defeats it (Pyrrhic Victory tier)

**Consequences**:
- Guild split: Evil members expelled or killed (−20% membership)
- Guild reputation −15 ("Internal Corruption Scandal")
- Corruption remains at 0.6 (guildhall tainted)
- Guild master resigns in shame, new election held

---

#### Scenario 3: World-Ending Prevented by Player Miracle

**Setup**:
- Evil cult summons DemonLord (Apocalyptic tier)
- Ritual 98% complete (2 minutes real-time remaining)
- All NPC counter-quests failed (villages destroyed, heroes dead)
- Player is last hope

**Player Intervention**:
- Player activates Divine Sniper miracle (time stop)
- Aims precisely at ritual leader's heart
- Perfect headshot (leader dies instantly)
- Ritual collapses at 99% progress (Near Miss tier, barely)

**Outcome**:
- DemonLord manifestation prevented at last moment
- Portal collapses, cult scattered
- Regional corruption 0.9 (permanent scars)
- World saved, but 4 villages destroyed, 10,000 dead
- Player reputation +100 ("Savior of the World")
- Survivors worship player as deity (cult forms)

**Narrative**: "At the final second, a divine arrow ended the nightmare."

---

## Summary

The **Environmental Quests and Loot Vectors** system creates:

1. **Dynamic Content**: Quests emerge from environmental state, not scripted
2. **Consequence-Driven**: Player actions (war, logging, mining) spawn encounters
3. **Class Asymmetry**: Priests excel vs. undead, warriors struggle vs. ethereal
4. **Alignment Integration**: Good communes, Evil enslaves, Neutral adapts
5. **Multiple Paths**: Combat, diplomacy, communion, enslavement, research
6. **Economic Impact**: Undead labor, spirit teaching, demonic knowledge
7. **Territorial Dynamics**: Villages expand by securing borders, building defenses
8. **Cross-Project**: Godgame (spirits/demons) and Space4X (derelicts/anomalies)

**Next Steps**:
- Prototype corruption accumulation from player actions
- Create spawn catalog with 50+ entity types
- Implement class effectiveness matrix
- Design UI for quest tracking and party composition
- Balance corruption thresholds and spawn rates
- Integrate with knowledge/guild/labor systems

---

**Related Documents**:
- [BehaviorAlignment_Summary.md](../../../Docs/BehaviorAlignment_Summary.md) - Alignment system
- [DualLeadershipPattern.md](DualLeadershipPattern.md) - Leadership dynamics
- [GuildCurriculum.md](GuildCurriculum.md) - Knowledge transmission
- [PatternBible.md](../PatternBible.md) - Emergent patterns

**Design Lead**: [TBD]
**Technical Lead**: [TBD]
**Last Review**: 2025-11-30

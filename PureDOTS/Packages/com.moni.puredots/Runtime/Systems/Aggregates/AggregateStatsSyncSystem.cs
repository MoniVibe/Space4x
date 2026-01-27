using PureDOTS.Runtime.Aggregates;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Skills;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Aggregates
{
    /// <summary>
    /// Synchronizes aggregate entity stats (e.g., average alignment, outlooks, mood)
    /// based on their member entities. This ensures that aggregate entities
    /// reflect the collective characteristics of their members.
    ///
    /// Aggregate entities (Bands, Guilds, Villages) derive their alignment,
    /// outlooks, and stats from their members - not the other way around.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AggregateStatsSyncSystem : ISystem
    {
        private ComponentLookup<VillagerAlignment> _alignmentLookup;
        private ComponentLookup<VillagerMood> _moodLookup;
        private ComponentLookup<VillagerAttributes> _attributesLookup;
        private ComponentLookup<SkillSet> _skillSetLookup;
        private ComponentLookup<VillagerNeeds> _needsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<BandAggregateStats>();

            _alignmentLookup = state.GetComponentLookup<VillagerAlignment>(false);
            _moodLookup = state.GetComponentLookup<VillagerMood>(true);
            _attributesLookup = state.GetComponentLookup<VillagerAttributes>(true);
            _skillSetLookup = state.GetComponentLookup<SkillSet>(true);
            _needsLookup = state.GetComponentLookup<VillagerNeeds>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _alignmentLookup.Update(ref state);
            _moodLookup.Update(ref state);
            _attributesLookup.Update(ref state);
            _skillSetLookup.Update(ref state);
            _needsLookup.Update(ref state);

            // Sync Band stats from members
            var bandHandle = new SyncBandStatsJob
            {
                AlignmentLookup = _alignmentLookup,
                MoodLookup = _moodLookup,
                AttributesLookup = _attributesLookup,
                NeedsLookup = _needsLookup
            }.ScheduleParallel(state.Dependency);

            // Sync Guild stats from members
            var guildHandle = new SyncGuildStatsJob
            {
                AlignmentLookup = _alignmentLookup,
                MoodLookup = _moodLookup,
                AttributesLookup = _attributesLookup,
                SkillSetLookup = _skillSetLookup
            }.ScheduleParallel(bandHandle);

            // Sync Village stats from residents
            var villageHandle = new SyncVillageStatsJob
            {
                AlignmentLookup = _alignmentLookup,
                MoodLookup = _moodLookup,
                AttributesLookup = _attributesLookup,
                NeedsLookup = _needsLookup
            }.ScheduleParallel(guildHandle);

            state.Dependency = villageHandle;
        }

        [BurstCompile]
        public partial struct SyncBandStatsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<VillagerAlignment> AlignmentLookup;
            [ReadOnly] public ComponentLookup<VillagerMood> MoodLookup;
            [ReadOnly] public ComponentLookup<VillagerAttributes> AttributesLookup;
            [ReadOnly] public ComponentLookup<VillagerNeeds> NeedsLookup;

            void Execute(
                Entity bandEntity,
                ref BandAggregateStats stats,
                ref VillagerAlignment bandAlignment,
                in DynamicBuffer<BandMember> members)
            {
                if (members.Length == 0)
                {
                    stats.MemberCount = 0;
                    stats.AverageMorale = 0f;
                    stats.AverageEnergy = 0f;
                    stats.AverageStrength = 0f;
                    return;
                }

                var totalMoral = 0f;
                var totalOrder = 0f;
                var totalPurity = 0f;
                var totalStrength = 0f;
                var totalMood = 0f;
                var totalEnergy = 0f;
                var totalStrengthAttr = 0f;
                var activeMembers = 0;

                for (int i = 0; i < members.Length; i++)
                {
                    var memberEntity = members[i].MemberEntity;

                    if (AlignmentLookup.HasComponent(memberEntity))
                    {
                        var alignment = AlignmentLookup[memberEntity];
                        totalMoral += alignment.MoralAxis;
                        totalOrder += alignment.OrderAxis;
                        totalPurity += alignment.PurityAxis;
                        totalStrength += alignment.AlignmentStrength;
                    }

                    if (MoodLookup.HasComponent(memberEntity))
                    {
                        var mood = MoodLookup[memberEntity];
                        totalMood += mood.Mood;
                    }

                    if (NeedsLookup.HasComponent(memberEntity))
                    {
                        var needs = NeedsLookup[memberEntity];
                        totalEnergy += needs.Energy;
                    }

                    if (AttributesLookup.HasComponent(memberEntity))
                    {
                        var attrs = AttributesLookup[memberEntity];
                        totalStrengthAttr += attrs.Strength;
                    }

                    activeMembers++;
                }

                if (activeMembers > 0)
                {
                    stats.MemberCount = (ushort)activeMembers;
                    stats.AverageMorale = totalMood / activeMembers;
                    stats.AverageEnergy = totalEnergy / activeMembers;
                    stats.AverageStrength = totalStrengthAttr / activeMembers;

                    bandAlignment.MoralAxis = (sbyte)math.clamp(totalMoral / activeMembers, -100f, 100f);
                    bandAlignment.OrderAxis = (sbyte)math.clamp(totalOrder / activeMembers, -100f, 100f);
                    bandAlignment.PurityAxis = (sbyte)math.clamp(totalPurity / activeMembers, -100f, 100f);
                    bandAlignment.AlignmentStrength = totalStrength / activeMembers;
                }
            }
        }

        [BurstCompile]
        public partial struct SyncGuildStatsJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public ComponentLookup<VillagerAlignment> AlignmentLookup;
            [ReadOnly] public ComponentLookup<VillagerMood> MoodLookup;
            [ReadOnly] public ComponentLookup<VillagerAttributes> AttributesLookup;
            [ReadOnly] public ComponentLookup<SkillSet> SkillSetLookup;

            void Execute(
                Entity guildEntity,
                ref PureDOTS.Runtime.Aggregates.Guild guild,
                ref PureDOTS.Runtime.Aggregates.GuildOutlookSet outlooks,
                in DynamicBuffer<PureDOTS.Runtime.Aggregates.GuildMember> members)
            {
                if (members.Length == 0)
                {
                    guild.MemberCount = 0;
                    guild.AverageMemberLevel = 0f;
                    guild.TotalExperience = 0;
                    return;
                }

                var totalMoral = 0f;
                var totalOrder = 0f;
                var totalPurity = 0f;
                var totalStrength = 0f;
                var totalMood = 0f;
                var totalLevel = 0f;
                uint totalXp = 0;
                var activeMembers = 0;

                for (int i = 0; i < members.Length; i++)
                {
                    var memberEntity = members[i].VillagerEntity;

                    if (AlignmentLookup.HasComponent(memberEntity))
                    {
                        var alignment = AlignmentLookup[memberEntity];
                        totalMoral += alignment.MoralAxis;
                        totalOrder += alignment.OrderAxis;
                        totalPurity += alignment.PurityAxis;
                        totalStrength += alignment.AlignmentStrength;
                    }

                    if (MoodLookup.HasComponent(memberEntity))
                    {
                        var mood = MoodLookup[memberEntity];
                        totalMood += mood.Mood;
                    }

                    if (SkillSetLookup.HasComponent(memberEntity))
                    {
                        var skills = SkillSetLookup[memberEntity];
                        totalLevel += skills.GetMaxLevel();
                    }

                    totalXp += members[i].ContributionScore;
                    activeMembers++;
                }

                if (activeMembers > 0)
                {
                    guild.MemberCount = (ushort)activeMembers;
                    guild.AverageMemberLevel = totalLevel / activeMembers;
                    guild.TotalExperience = totalXp;

                    if (AlignmentLookup.HasComponent(guildEntity))
                    {
                        var guildAlignment = AlignmentLookup[guildEntity];
                        guildAlignment.MoralAxis = (sbyte)math.clamp(totalMoral / activeMembers, -100f, 100f);
                        guildAlignment.OrderAxis = (sbyte)math.clamp(totalOrder / activeMembers, -100f, 100f);
                        guildAlignment.PurityAxis = (sbyte)math.clamp(totalPurity / activeMembers, -100f, 100f);
                        guildAlignment.AlignmentStrength = totalStrength / activeMembers;
                        AlignmentLookup[guildEntity] = guildAlignment;

                        // Update outlooks based on aggregate alignment
                        outlooks.Outlook1 = DeriveOutlookFromAlignment(guildAlignment, 0);
                        outlooks.Outlook2 = DeriveOutlookFromAlignment(guildAlignment, 1);
                        outlooks.IsFanatic = guildAlignment.AlignmentStrength > 0.7f;
                    }
                }
            }

            private static byte DeriveOutlookFromAlignment(VillagerAlignment alignment, int index)
            {
                if (index == 0)
                {
                    if (alignment.MoralAxis > 50 && alignment.OrderAxis > 50)
                    {
                        return 1; // Heroic
                    }
                    if (alignment.MoralAxis < -50)
                    {
                        return 4; // Ruthless
                    }
                    if (alignment.PurityAxis > 50)
                    {
                        return 5; // Devout
                    }
                    if (alignment.OrderAxis < -50)
                    {
                        return 6; // Rebellious
                    }
                    return 7; // Pragmatic
                }
                if (index == 1)
                {
                    if (alignment.OrderAxis > 30)
                    {
                        return 3; // Methodical
                    }
                    if (alignment.MoralAxis > 30)
                    {
                        return 2; // Fair
                    }
                }
                return 0;
            }
        }

        /// <summary>
        /// Syncs village stats from resident villagers.
        /// Village alignment is stored in the VillageStats.Alignment field (0-100 scale).
        /// </summary>
        [BurstCompile]
        public partial struct SyncVillageStatsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<VillagerAlignment> AlignmentLookup;
            [ReadOnly] public ComponentLookup<VillagerMood> MoodLookup;
            [ReadOnly] public ComponentLookup<VillagerAttributes> AttributesLookup;
            [ReadOnly] public ComponentLookup<VillagerNeeds> NeedsLookup;

            void Execute(
                ref VillageStats villageStats,
                in DynamicBuffer<VillageResidentEntry> residents)
            {
                if (residents.Length == 0)
                {
                    villageStats.Population = 0;
                    villageStats.Cohesion = 0f;
                    villageStats.Initiative = 0f;
                    return;
                }

                var totalMoral = 0f;
                var totalMood = 0f;
                var totalCohesion = 0f;
                var totalInitiative = 0f;
                var activeResidents = 0;

                for (int i = 0; i < residents.Length; i++)
                {
                    var residentEntity = residents[i].VillagerEntity;

                    if (AlignmentLookup.HasComponent(residentEntity))
                    {
                        var alignment = AlignmentLookup[residentEntity];
                        // Average moral axis contributes to village alignment (0-100 scale)
                        totalMoral += alignment.MoralAxis;
                    }

                    if (MoodLookup.HasComponent(residentEntity))
                    {
                        var mood = MoodLookup[residentEntity];
                        totalMood += mood.Mood;
                    }

                    if (AttributesLookup.HasComponent(residentEntity))
                    {
                        var attrs = AttributesLookup[residentEntity];
                        // Cohesion influenced by average charisma and mood
                        totalCohesion += attrs.Wisdom * 0.5f;
                        // Initiative influenced by average willpower
                        totalInitiative += attrs.Willpower * 0.5f;
                    }

                    activeResidents++;
                }

                if (activeResidents > 0)
                {
                    villageStats.Population = activeResidents;
                    villageStats.Cohesion = math.clamp((totalCohesion / activeResidents) + (totalMood / activeResidents) * 0.3f, 0f, 100f);
                    villageStats.Initiative = math.clamp(totalInitiative / activeResidents, 0f, 100f);

                    // Convert moral axis (-100 to +100) to alignment (0 to 100)
                    // -100 moral = 0 alignment, +100 moral = 100 alignment
                    villageStats.Alignment = math.clamp((totalMoral / activeResidents + 100f) * 0.5f, 0f, 100f);
                }
            }
        }
    }
}

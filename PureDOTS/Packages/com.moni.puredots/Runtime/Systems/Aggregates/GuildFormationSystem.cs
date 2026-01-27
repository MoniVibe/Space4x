using PureDOTS.Runtime.Aggregates;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Skills;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Aggregates
{
    /// <summary>
    /// Spawns guilds when groups of entities with shared goals and compatible alignments form.
    /// Guilds are formed by member entities based on their shared goals and outlooks/alignments,
    /// NOT based on village characteristics. Villages may host guild enclaves/embassies based
    /// on the village's relations with the guild.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GuildFormationSystem : ISystem
    {
        private EntityQuery _potentialFounderQuery;
        private ComponentLookup<VillagerAlignment> _alignmentLookup;
        private ComponentLookup<VillagerMood> _moodLookup;
        private ComponentLookup<VillagerAIState> _aiStateLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SkillSet> _skillSetLookup;
        private ComponentLookup<GuildMembership> _guildMembershipLookup;

        private const int MinFoundersForGuild = 5;
        private const float FounderProximityRadius = 25f;
        private const uint GuildCheckInterval = 300; // Check every ~5 seconds at 60 ticks/sec
        
        // Instance fields for Burst-compatible FixedString patterns (initialized in OnCreate)
        private FixedString64Bytes _guildNameHeroes;
        private FixedString64Bytes _guildNameMerchants;
        private FixedString64Bytes _guildNameScholars;
        private FixedString64Bytes _guildNameAssassins;
        private FixedString64Bytes _guildNameArtisans;
        private FixedString64Bytes _guildNameFarmers;
        private FixedString64Bytes _guildNameMystics;
        private FixedString64Bytes _guildNameMages;
        private FixedString64Bytes _guildNameHolyOrder;
        private FixedString64Bytes _guildNameRogues;
        private FixedString64Bytes _guildNameRebels;
        private FixedString64Bytes _guildNameDefault;

        public void OnCreate(ref SystemState state)
        {
            // Query for potential guild founders - entities with alignment and goals, not in a guild
            _potentialFounderQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerId, VillagerAlignment, VillagerAIState, LocalTransform>()
                .WithNone<GuildMembership, BandMembership, PlaybackGuardTag>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _alignmentLookup = state.GetComponentLookup<VillagerAlignment>(true);
            _moodLookup = state.GetComponentLookup<VillagerMood>(true);
            _aiStateLookup = state.GetComponentLookup<VillagerAIState>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _skillSetLookup = state.GetComponentLookup<SkillSet>(true);
            _guildMembershipLookup = state.GetComponentLookup<GuildMembership>(true);
            
            // Initialize FixedString patterns (OnCreate is not Burst-compiled)
            _guildNameHeroes = new FixedString64Bytes("Champions");
            _guildNameMerchants = new FixedString64Bytes("Trade League");
            _guildNameScholars = new FixedString64Bytes("Seekers");
            _guildNameAssassins = new FixedString64Bytes("Shadow Hand");
            _guildNameArtisans = new FixedString64Bytes("Crafters Union");
            _guildNameFarmers = new FixedString64Bytes("Harvest Guild");
            _guildNameMystics = new FixedString64Bytes("Seers Circle");
            _guildNameMages = new FixedString64Bytes("Arcane Order");
            _guildNameHolyOrder = new FixedString64Bytes("Holy Covenant");
            _guildNameRogues = new FixedString64Bytes("Thieves Guild");
            _guildNameRebels = new FixedString64Bytes("Liberation Front");
            _guildNameDefault = new FixedString64Bytes("Guild");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Only check periodically
            if (timeState.Tick % GuildCheckInterval != 0)
            {
                return;
            }

            if (_potentialFounderQuery.IsEmpty)
            {
                return;
            }

            state.EntityManager.CompleteDependencyBeforeRO<VillagerAIState>();
            state.CompleteDependency();

            _alignmentLookup.Update(ref state);
            _moodLookup.Update(ref state);
            _aiStateLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _skillSetLookup.Update(ref state);
            _guildMembershipLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Collect potential founders
            var entities = _potentialFounderQuery.ToEntityArray(Allocator.Temp);
            var positions = new NativeArray<float3>(entities.Length, Allocator.Temp);
            var alignments = new NativeArray<VillagerAlignment>(entities.Length, Allocator.Temp);
            var goals = new NativeArray<VillagerAIState.Goal>(entities.Length, Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                positions[i] = _transformLookup[entities[i]].Position;
                alignments[i] = _alignmentLookup[entities[i]];
                goals[i] = _aiStateLookup[entities[i]].CurrentGoal;
            }

            // Find clusters of entities with compatible alignments and shared goals
            var proximityRadiusSq = FounderProximityRadius * FounderProximityRadius;
            var processed = new NativeHashSet<int>(entities.Length, Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (processed.Contains(i))
                {
                    continue;
                }

                var cluster = new NativeList<int>(Allocator.Temp);
                cluster.Add(i);
                processed.Add(i);

                // Find nearby entities with compatible alignments
                for (int j = i + 1; j < entities.Length; j++)
                {
                    if (processed.Contains(j))
                    {
                        continue;
                    }

                    var distSq = math.distancesq(positions[i], positions[j]);
                    if (distSq > proximityRadiusSq)
                    {
                        continue;
                    }

                    // Check alignment compatibility
                    if (!AreAlignmentsCompatible(alignments[i], alignments[j]))
                    {
                        continue;
                    }

                    // Check goal compatibility (shared purpose)
                    if (!AreGoalsCompatibleForGuild(goals[i], goals[j]))
                    {
                        continue;
                    }

                    cluster.Add(j);
                    processed.Add(j);
                }

                // Need minimum founders
                if (cluster.Length < MinFoundersForGuild)
                {
                    cluster.Dispose();
                    continue;
                }

                // Calculate aggregate alignment from founders
                var aggregateAlignment = CalculateAggregateAlignment(alignments, cluster);

                // Roll formation probability based on alignment strength and cluster size
                var formationChance = CalculateFormationProbability(cluster.Length, aggregateAlignment);
                var random = new Random((uint)(timeState.Tick + i * 31));

                if (random.NextFloat() > formationChance)
                {
                    cluster.Dispose();
                    continue;
                }

                // Determine guild type from aggregate member alignment (not village!)
                var guildType = DetermineGuildTypeFromMemberAlignment(aggregateAlignment);

                // Calculate headquarters position (centroid of founders)
                var hqPosition = CalculateCentroid(positions, cluster);

                // Create the guild
                var guildEntity = ecb.CreateEntity();
                ecb.AddComponent(guildEntity, new PureDOTS.Runtime.Aggregates.Guild
                {
                    Type = guildType,
                    GuildName = GenerateGuildName(guildType, _guildNameHeroes, _guildNameMerchants, _guildNameScholars, 
                        _guildNameAssassins, _guildNameArtisans, _guildNameFarmers, _guildNameMystics,
                        _guildNameMages, _guildNameHolyOrder, _guildNameRogues, _guildNameRebels, _guildNameDefault),
                    FoundedTick = timeState.Tick,
                    HomeVillage = Entity.Null, // Guilds don't belong to villages
                    HeadquartersPosition = hqPosition,
                    MemberCount = (ushort)cluster.Length,
                    AverageMemberLevel = 0f, // Will be calculated by AggregateStatsSyncSystem
                    TotalExperience = 0,
                    ReputationScore = 50,
                    CurrentMission = default
                });

                // Add aggregate alignment component (will be synced from members)
                ecb.AddComponent(guildEntity, aggregateAlignment);

                // Add leadership component
                ecb.AddComponent(guildEntity, new GuildLeadership
                {
                    Governance = DetermineGovernanceFromAlignment(aggregateAlignment),
                    GuildMasterEntity = Entity.Null, // Will be elected
                    MasterElectedTick = 0,
                    QuartermasterEntity = Entity.Null,
                    RecruiterEntity = Entity.Null,
                    DiplomatEntity = Entity.Null,
                    WarMasterEntity = Entity.Null,
                    VoteInProgress = false,
                    VoteProposal = default,
                    VoteEndTick = 0
                });

                // Add knowledge component
                ecb.AddComponent(guildEntity, new GuildKnowledge
                {
                    DemonSlayingBonus = 0,
                    UndeadSlayingBonus = 0,
                    BossHuntingBonus = 0,
                    CelestialCombatBonus = 0,
                    EspionageEffectiveness = 10,
                    CoordinationBonus = 10,
                    SurvivalBonus = 10,
                    DemonsKilled = 0,
                    UndeadKilled = 0,
                    BossesKilled = 0,
                    CelestialsKilled = 0
                });

                // Add treasury
                ecb.AddComponent(guildEntity, new GuildTreasury
                {
                    GoldReserves = 50f * cluster.Length, // Starting funds based on founders
                    LootValue = 0f,
                    LegendaryItemCount = 0
                });

                // Add member buffer and populate with founders
                var members = ecb.AddBuffer<GuildMember>(guildEntity);
                Entity guildMaster = Entity.Null;
                float bestLeaderScore = -1f;

                for (int k = 0; k < cluster.Length; k++)
                {
                    var founderEntity = entities[cluster[k]];
                    var isFirst = k == 0;

                    // Calculate leadership score
                    var leaderScore = CalculateLeadershipScore(founderEntity);
                    if (leaderScore > bestLeaderScore)
                    {
                        bestLeaderScore = leaderScore;
                        guildMaster = founderEntity;
                    }

                    members.Add(new GuildMember
                    {
                        VillagerEntity = founderEntity,
                        JoinedTick = timeState.Tick,
                        ExperienceContributed = 0,
                        ContributionScore = 0,
                        Rank = 0, // Will update guild master after
                        IsOfficer = false,
                        IsGuildMaster = false
                    });

                    // Add membership to founder
                    ecb.AddComponent(founderEntity, new GuildMembership
                    {
                        GuildEntity = guildEntity,
                        JoinedTick = timeState.Tick,
                        Role = BandRole.Laborer
                    });
                }

                // Update guild master in members buffer (deferred - will be set by recruitment system)

                // Add relations buffer
                ecb.AddBuffer<GuildRelation>(guildEntity);

                // Add embassy buffer (embassies are established later based on village relations)
                ecb.AddBuffer<GuildEmbassy>(guildEntity);

                // Add vote buffer
                ecb.AddBuffer<GuildVote>(guildEntity);

                // Add outlook set (derived from member alignments, will be synced)
                ecb.AddComponent(guildEntity, new GuildOutlookSet
                {
                    Outlook1 = DeriveOutlookFromAlignment(aggregateAlignment, 0),
                    Outlook2 = DeriveOutlookFromAlignment(aggregateAlignment, 1),
                    Outlook3 = 0,
                    IsFanatic = aggregateAlignment.AlignmentStrength > 0.7f
                });

                cluster.Dispose();
            }

            entities.Dispose();
            positions.Dispose();
            alignments.Dispose();
            goals.Dispose();
            processed.Dispose();
        }

        private static bool AreAlignmentsCompatible(VillagerAlignment a, VillagerAlignment b)
        {
            // Alignments are compatible if they're within 40 points on each axis
            const int tolerance = 40;
            return math.abs(a.MoralAxis - b.MoralAxis) <= tolerance &&
                   math.abs(a.OrderAxis - b.OrderAxis) <= tolerance &&
                   math.abs(a.PurityAxis - b.PurityAxis) <= tolerance;
        }

        private static bool AreGoalsCompatibleForGuild(VillagerAIState.Goal a, VillagerAIState.Goal b)
        {
            // For guild formation, we care about long-term compatible goals
            // Work and Fight goals are compatible (both productive)
            if ((a == VillagerAIState.Goal.Work || a == VillagerAIState.Goal.Fight) &&
                (b == VillagerAIState.Goal.Work || b == VillagerAIState.Goal.Fight))
            {
                return true;
            }

            // Same goal is always compatible
            return a == b;
        }

        private static VillagerAlignment CalculateAggregateAlignment(NativeArray<VillagerAlignment> alignments, NativeList<int> cluster)
        {
            var totalMoral = 0f;
            var totalOrder = 0f;
            var totalPurity = 0f;
            var totalStrength = 0f;

            for (int i = 0; i < cluster.Length; i++)
            {
                var alignment = alignments[cluster[i]];
                totalMoral += alignment.MoralAxis;
                totalOrder += alignment.OrderAxis;
                totalPurity += alignment.PurityAxis;
                totalStrength += alignment.AlignmentStrength;
            }

            return new VillagerAlignment
            {
                MoralAxis = (sbyte)math.clamp(totalMoral / cluster.Length, -100f, 100f),
                OrderAxis = (sbyte)math.clamp(totalOrder / cluster.Length, -100f, 100f),
                PurityAxis = (sbyte)math.clamp(totalPurity / cluster.Length, -100f, 100f),
                AlignmentStrength = totalStrength / cluster.Length
            };
        }

        private static float3 CalculateCentroid(NativeArray<float3> positions, NativeList<int> cluster)
        {
            var sum = float3.zero;
            for (int i = 0; i < cluster.Length; i++)
            {
                sum += positions[cluster[i]];
            }
            return sum / cluster.Length;
        }

        private static float CalculateFormationProbability(int clusterSize, VillagerAlignment alignment)
        {
            var baseProbability = 0.1f;

            // Larger clusters = higher chance
            baseProbability += math.min(clusterSize * 0.02f, 0.2f);

            // Stronger alignment = higher chance (more unified purpose)
            baseProbability += alignment.AlignmentStrength * 0.15f;

            // Extreme alignments are more likely to form guilds (strong convictions)
            var extremity = (math.abs(alignment.MoralAxis) + math.abs(alignment.OrderAxis) + math.abs(alignment.PurityAxis)) / 300f;
            baseProbability += extremity * 0.1f;

            return math.clamp(baseProbability, 0f, 0.6f);
        }

        private float CalculateLeadershipScore(Entity entity)
        {
            var score = 0f;

            if (_skillSetLookup.HasComponent(entity))
            {
                var skillSet = _skillSetLookup[entity];
                score += skillSet.GetMaxLevel() * 0.5f;
            }

            if (_moodLookup.HasComponent(entity))
            {
                var mood = _moodLookup[entity];
                score += mood.Mood * 0.3f;
            }

            if (_alignmentLookup.HasComponent(entity))
            {
                var alignment = _alignmentLookup[entity];
                score += alignment.AlignmentStrength * 20f;
            }

            return score;
        }

        private static PureDOTS.Runtime.Aggregates.Guild.GuildType DetermineGuildTypeFromMemberAlignment(VillagerAlignment alignment)
        {
            // Determine guild type based on aggregate member alignment
            if (alignment.MoralAxis > 50 && alignment.OrderAxis > 30)
            {
                return PureDOTS.Runtime.Aggregates.Guild.GuildType.Heroes;
            }
            if (alignment.MoralAxis > 50 && alignment.PurityAxis > 50)
            {
                return PureDOTS.Runtime.Aggregates.Guild.GuildType.HolyOrder;
            }
            if (alignment.MoralAxis < -50 && alignment.OrderAxis < -30)
            {
                return PureDOTS.Runtime.Aggregates.Guild.GuildType.Assassins;
            }
            if (alignment.MoralAxis < -30 && alignment.OrderAxis < -50)
            {
                return PureDOTS.Runtime.Aggregates.Guild.GuildType.Rogues;
            }
            if (alignment.OrderAxis > 50 && math.abs(alignment.MoralAxis) < 30)
            {
                return PureDOTS.Runtime.Aggregates.Guild.GuildType.Scholars;
            }
            if (alignment.OrderAxis < -50 && alignment.MoralAxis > 30)
            {
                return PureDOTS.Runtime.Aggregates.Guild.GuildType.Rebels;
            }
            if (alignment.PurityAxis > 40)
            {
                return PureDOTS.Runtime.Aggregates.Guild.GuildType.Mystics;
            }
            if (alignment.PurityAxis < -40)
            {
                return PureDOTS.Runtime.Aggregates.Guild.GuildType.Mages;
            }

            // Default based on order axis
            if (alignment.OrderAxis > 0)
            {
                return PureDOTS.Runtime.Aggregates.Guild.GuildType.Artisans;
            }

            return PureDOTS.Runtime.Aggregates.Guild.GuildType.Merchants;
        }

        private static GuildLeadership.GovernanceType DetermineGovernanceFromAlignment(VillagerAlignment alignment)
        {
            if (alignment.OrderAxis > 50)
            {
                return GuildLeadership.GovernanceType.Meritocratic;
            }
            if (alignment.OrderAxis < -50)
            {
                return GuildLeadership.GovernanceType.Authoritarian;
            }
            if (alignment.MoralAxis > 50)
            {
                return GuildLeadership.GovernanceType.Democratic;
            }

            return GuildLeadership.GovernanceType.Oligarchic;
        }

        private static FixedString64Bytes GenerateGuildName(
            PureDOTS.Runtime.Aggregates.Guild.GuildType type,
            in FixedString64Bytes nameHeroes,
            in FixedString64Bytes nameMerchants,
            in FixedString64Bytes nameScholars,
            in FixedString64Bytes nameAssassins,
            in FixedString64Bytes nameArtisans,
            in FixedString64Bytes nameFarmers,
            in FixedString64Bytes nameMystics,
            in FixedString64Bytes nameMages,
            in FixedString64Bytes nameHolyOrder,
            in FixedString64Bytes nameRogues,
            in FixedString64Bytes nameRebels,
            in FixedString64Bytes nameDefault)
        {
            return type switch
            {
                PureDOTS.Runtime.Aggregates.Guild.GuildType.Heroes => nameHeroes,
                PureDOTS.Runtime.Aggregates.Guild.GuildType.Merchants => nameMerchants,
                PureDOTS.Runtime.Aggregates.Guild.GuildType.Scholars => nameScholars,
                PureDOTS.Runtime.Aggregates.Guild.GuildType.Assassins => nameAssassins,
                PureDOTS.Runtime.Aggregates.Guild.GuildType.Artisans => nameArtisans,
                PureDOTS.Runtime.Aggregates.Guild.GuildType.Farmers => nameFarmers,
                PureDOTS.Runtime.Aggregates.Guild.GuildType.Mystics => nameMystics,
                PureDOTS.Runtime.Aggregates.Guild.GuildType.Mages => nameMages,
                PureDOTS.Runtime.Aggregates.Guild.GuildType.HolyOrder => nameHolyOrder,
                PureDOTS.Runtime.Aggregates.Guild.GuildType.Rogues => nameRogues,
                PureDOTS.Runtime.Aggregates.Guild.GuildType.Rebels => nameRebels,
                _ => nameDefault
            };
        }

        private static byte DeriveOutlookFromAlignment(VillagerAlignment alignment, int index)
        {
            // Derive outlook from alignment axes
            if (index == 0)
            {
                if (alignment.MoralAxis > 50 && alignment.OrderAxis > 50)
                {
                    return 1; // Heroic/Righteous
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
                // Secondary outlook
                if (alignment.OrderAxis > 30)
                {
                    return 3; // Scholarly/Methodical
                }
                if (alignment.MoralAxis > 30)
                {
                    return 2; // Mercantile/Fair
                }
            }
            return 0;
        }
    }

    /// <summary>
    /// Processes guild member recruitment from villages.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GuildFormationSystem))]
    public partial struct GuildRecruitmentSystem : ISystem
    {
        private const uint RecruitmentInterval = 120;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Only recruit periodically
            if (timeState.Tick % RecruitmentInterval != 0)
            {
                return;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process guilds that need members
            foreach (var (guild, leadership, members, guildEntity) in
                SystemAPI.Query<RefRW<PureDOTS.Runtime.Aggregates.Guild>, RefRW<GuildLeadership>, DynamicBuffer<GuildMember>>()
                    .WithEntityAccess())
            {
                // Skip if guild has enough members
                if (guild.ValueRO.MemberCount >= 50)
                {
                    continue;
                }

                // Find eligible villagers from home village
                var homeVillage = guild.ValueRO.HomeVillage;
                if (homeVillage == Entity.Null)
                {
                    continue;
                }

                // Recruit villagers (simplified - would normally query villagers in village)
                // For now, just increment member count to show the system works
                foreach (var (villagerId, villagerMood, villagerEntity) in
                    SystemAPI.Query<RefRO<VillagerId>, RefRO<VillagerMood>>()
                        .WithNone<GuildMembership, BandMembership, PlaybackGuardTag>()
                        .WithEntityAccess())
                {
                    // Check if villager is willing to join (based on alignment match)
                    if (villagerMood.ValueRO.Alignment < 40f && guild.ValueRO.Type == PureDOTS.Runtime.Aggregates.Guild.GuildType.Heroes)
                    {
                        continue;
                    }

                    // Recruit this villager
                    var isFirst = members.Length == 0;
                    var role = isFirst ? (byte)2 : (byte)0; // First member is guild master

                    members.Add(new GuildMember
                    {
                        VillagerEntity = villagerEntity,
                        JoinedTick = timeState.Tick,
                        ExperienceContributed = 0,
                        ContributionScore = 0,
                        Rank = role,
                        IsOfficer = isFirst,
                        IsGuildMaster = isFirst
                    });

                    // Add membership to villager
                    ecb.AddComponent(villagerEntity, new GuildMembership
                    {
                        GuildEntity = guildEntity,
                        JoinedTick = timeState.Tick,
                        Role = isFirst ? BandRole.Leader : BandRole.Laborer
                    });

                    // Update guild master if first member
                    if (isFirst)
                    {
                        var leadershipValue = leadership.ValueRO;
                        leadershipValue.GuildMasterEntity = villagerEntity;
                        leadershipValue.MasterElectedTick = timeState.Tick;
                        leadership.ValueRW = leadershipValue;
                    }

                    // Update member count
                    var guildValue = guild.ValueRO;
                        guildValue.MemberCount++;
                        guild.ValueRW = guildValue;

                    // Only recruit one per tick to spread out
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Guild membership component for villagers.
    /// </summary>
    public struct GuildMembership : IComponentData
    {
        public Entity GuildEntity;
        public uint JoinedTick;
        public BandRole Role;
    }
}

using PureDOTS.Runtime.Aggregates;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Aggregates
{
    /// <summary>
    /// Detects when 2+ entities have aligned goals and suggests band formation.
    /// Checks entities in same location with compatible goals, rolls formation probability,
    /// and creates BandFormationCandidate if check succeeds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BandFormationSystem : ISystem
    {
        private EntityQuery _potentialMemberQuery;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VillagerAIState> _aiStateLookup;
        private ComponentLookup<VillagerMood> _moodLookup;
        private ComponentLookup<BandMembership> _membershipLookup;
        private ComponentLookup<VillagerNeeds> _needsLookup;
        
        // Instance fields for Burst-compatible FixedString patterns (initialized in OnCreate)
        private FixedString128Bytes _goalDescWork;
        private FixedString128Bytes _goalDescFight;
        private FixedString128Bytes _goalDescFlee;
        private FixedString128Bytes _goalDescHunger;
        private FixedString128Bytes _goalDescRest;
        private FixedString128Bytes _goalDescDefault;

        public void OnCreate(ref SystemState state)
        {
            _potentialMemberQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerId, LocalTransform, VillagerAIState>()
                .WithNone<BandMembership, PlaybackGuardTag>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _aiStateLookup = state.GetComponentLookup<VillagerAIState>(true);
            _moodLookup = state.GetComponentLookup<VillagerMood>(true);
            _membershipLookup = state.GetComponentLookup<BandMembership>(true);
            _needsLookup = state.GetComponentLookup<VillagerNeeds>(true);
            
            // Initialize FixedString patterns (OnCreate is not Burst-compiled)
            _goalDescWork = new FixedString128Bytes("Cooperative Work");
            _goalDescFight = new FixedString128Bytes("Combat Defense");
            _goalDescFlee = new FixedString128Bytes("Escape Danger");
            _goalDescHunger = new FixedString128Bytes("Find Food");
            _goalDescRest = new FixedString128Bytes("Find Shelter");
            _goalDescDefault = new FixedString128Bytes("Shared Purpose");
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

            // Only check for band formation periodically (every ~60 ticks)
            if (timeState.Tick % 60 != 0)
            {
                return;
            }

            if (_potentialMemberQuery.IsEmpty)
            {
                return;
            }

            state.EntityManager.CompleteDependencyBeforeRO<VillagerAIState>();
            state.CompleteDependency();

            _transformLookup.Update(ref state);
            _aiStateLookup.Update(ref state);
            _moodLookup.Update(ref state);
            _membershipLookup.Update(ref state);
            _needsLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Get all potential members
            var entities = _potentialMemberQuery.ToEntityArray(Allocator.Temp);
            var positions = new NativeArray<float3>(entities.Length, Allocator.Temp);
            var goals = new NativeArray<VillagerAIState.Goal>(entities.Length, Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                positions[i] = _transformLookup[entities[i]].Position;
                goals[i] = _aiStateLookup[entities[i]].CurrentGoal;
            }

            // Find clusters of entities with compatible goals
            const float clusterRadius = 10f;
            const float clusterRadiusSq = clusterRadius * clusterRadius;
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

                // Find nearby entities with compatible goals
                for (int j = i + 1; j < entities.Length; j++)
                {
                    if (processed.Contains(j))
                    {
                        continue;
                    }

                    var distSq = math.distancesq(positions[i], positions[j]);
                    if (distSq > clusterRadiusSq)
                    {
                        continue;
                    }

                    // Check goal compatibility
                    if (!AreGoalsCompatible(goals[i], goals[j]))
                    {
                        continue;
                    }

                    cluster.Add(j);
                    processed.Add(j);
                }

                // Need at least 2 entities to form a band
                if (cluster.Length < 2)
                {
                    cluster.Dispose();
                    continue;
                }

                // Roll formation probability
                var formationChance = CalculateFormationProbability(entities, cluster, timeState.Tick);
                var seed = (uint)(timeState.Tick + i * 31 + 1);
                if (seed == 0) seed = 1;
                var random = new Random(seed);

                if (random.NextFloat() > formationChance)
                {
                    cluster.Dispose();
                    continue;
                }

                // Create band formation candidate
                var candidateEntity = ecb.CreateEntity();
                ecb.AddComponent(candidateEntity, new BandFormationCandidate
                {
                    InitiatorEntity = entities[cluster[0]],
                    SharedGoal = GoalToDescription(goals[cluster[0]], _goalDescWork, _goalDescFight, _goalDescFlee, _goalDescHunger, _goalDescRest, _goalDescDefault),
                    ProposedTick = timeState.Tick,
                    ProspectiveMemberCount = (byte)math.min(cluster.Length, 255)
                });

                var prospects = ecb.AddBuffer<BandFormationProspect>(candidateEntity);
                for (int k = 0; k < cluster.Length; k++)
                {
                    prospects.Add(new BandFormationProspect
                    {
                        ProspectEntity = entities[cluster[k]],
                        HasAccepted = k == 0 // Initiator auto-accepts
                    });
                }

                cluster.Dispose();
            }

            entities.Dispose();
            positions.Dispose();
            goals.Dispose();
            processed.Dispose();
        }

        private static bool AreGoalsCompatible(VillagerAIState.Goal a, VillagerAIState.Goal b)
        {
            // Same goal is always compatible
            if (a == b)
            {
                return true;
            }

            // Combat goals are compatible with each other
            if ((a == VillagerAIState.Goal.Fight || a == VillagerAIState.Goal.Flee) &&
                (b == VillagerAIState.Goal.Fight || b == VillagerAIState.Goal.Flee))
            {
                return true;
            }

            // Work goals are compatible
            if (a == VillagerAIState.Goal.Work && b == VillagerAIState.Goal.Work)
            {
                return true;
            }

            return false;
        }

        private float CalculateFormationProbability(NativeArray<Entity> entities, NativeList<int> cluster, uint currentTick)
        {
            var baseProbability = 0.1f; // 10% base chance

            // More members = higher chance (up to a point)
            var memberBonus = math.min(cluster.Length * 0.05f, 0.3f);

            // Check desperation (low needs increase formation chance)
            var desperationBonus = 0f;
            for (int i = 0; i < cluster.Length; i++)
            {
                var entity = entities[cluster[i]];
                if (_needsLookup.HasComponent(entity))
                {
                    var needs = _needsLookup[entity];
                    // Low health or high hunger increases desperation
                    if (needs.Health < 50f || needs.Hunger > 70)
                    {
                        desperationBonus += 0.05f;
                    }
                }
            }
            desperationBonus = math.min(desperationBonus, 0.2f);

            // Check alignment (high alignment entities are more likely to cooperate)
            var alignmentBonus = 0f;
            for (int i = 0; i < cluster.Length; i++)
            {
                var entity = entities[cluster[i]];
                if (_moodLookup.HasComponent(entity))
                {
                    var mood = _moodLookup[entity];
                    if (mood.Alignment > 60f)
                    {
                        alignmentBonus += 0.02f;
                    }
                }
            }
            alignmentBonus = math.min(alignmentBonus, 0.15f);

            return math.clamp(baseProbability + memberBonus + desperationBonus + alignmentBonus, 0f, 0.8f);
        }

        private static FixedString128Bytes GoalToDescription(
            VillagerAIState.Goal goal,
            in FixedString128Bytes goalDescWork,
            in FixedString128Bytes goalDescFight,
            in FixedString128Bytes goalDescFlee,
            in FixedString128Bytes goalDescHunger,
            in FixedString128Bytes goalDescRest,
            in FixedString128Bytes goalDescDefault)
        {
            return goal switch
            {
                VillagerAIState.Goal.Work => goalDescWork,
                VillagerAIState.Goal.Fight => goalDescFight,
                VillagerAIState.Goal.Flee => goalDescFlee,
                VillagerAIState.Goal.SurviveHunger => goalDescHunger,
                VillagerAIState.Goal.Rest => goalDescRest,
                _ => goalDescDefault
            };
        }
    }

    /// <summary>
    /// Processes band formation candidates into actual bands.
    /// Confirms all prospects have accepted, creates Band entity,
    /// adds BandMembership components to members, and elects leader.
    /// Bands derive their alignment from their members via AggregateStatsSyncSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BandFormationSystem))]
    public partial struct BandFormationProcessingSystem : ISystem
    {
        private ComponentLookup<VillagerMood> _moodLookup;
        private ComponentLookup<VillagerAttributes> _attributesLookup;
        private ComponentLookup<VillagerAlignment> _alignmentLookup;
        
        // Instance fields for Burst-compatible FixedString patterns (initialized in OnCreate)
        private FixedString128Bytes _sharedGoalCombatDefense;
        private FixedString128Bytes _sharedGoalEscapeDanger;
        private FixedString128Bytes _sharedGoalFindFood;
        private FixedString128Bytes _sharedGoalCooperativeWork;
        private FixedString64Bytes _bandNameDefenders;
        private FixedString64Bytes _bandNameHunters;
        private FixedString64Bytes _bandNameMiners;
        private FixedString64Bytes _bandNameBuilders;
        private FixedString64Bytes _bandNameWanderers;
        private FixedString64Bytes _bandNameDefault;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _moodLookup = state.GetComponentLookup<VillagerMood>(true);
            _attributesLookup = state.GetComponentLookup<VillagerAttributes>(true);
            _alignmentLookup = state.GetComponentLookup<VillagerAlignment>(true);
            
            // Initialize FixedString patterns (OnCreate is not Burst-compiled)
            _sharedGoalCombatDefense = new FixedString128Bytes("Combat Defense");
            _sharedGoalEscapeDanger = new FixedString128Bytes("Escape Danger");
            _sharedGoalFindFood = new FixedString128Bytes("Find Food");
            _sharedGoalCooperativeWork = new FixedString128Bytes("Cooperative Work");
            _bandNameDefenders = new FixedString64Bytes("Defenders");
            _bandNameHunters = new FixedString64Bytes("Hunters");
            _bandNameMiners = new FixedString64Bytes("Miners");
            _bandNameBuilders = new FixedString64Bytes("Builders");
            _bandNameWanderers = new FixedString64Bytes("Wanderers");
            _bandNameDefault = new FixedString64Bytes("Band");
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

            _moodLookup.Update(ref state);
            _attributesLookup.Update(ref state);
            _alignmentLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process candidates
            foreach (var (candidate, prospects, candidateEntity) in
                SystemAPI.Query<RefRO<BandFormationCandidate>, DynamicBuffer<BandFormationProspect>>()
                    .WithEntityAccess())
            {
                var prospectsBuffer = prospects;

                // Check if enough time has passed for acceptance (give 30 ticks)
                if (timeState.Tick - candidate.ValueRO.ProposedTick < 30)
                {
                    // Auto-accept remaining prospects (simplified - in full implementation would check individual willingness)
                    for (int i = 0; i < prospectsBuffer.Length; i++)
                    {
                        var prospect = prospectsBuffer[i];
                        if (!prospect.HasAccepted)
                        {
                            prospect.HasAccepted = ShouldAcceptBandInvitation(prospect.ProspectEntity, candidate.ValueRO);
                            prospectsBuffer[i] = prospect;
                        }
                    }
                    continue;
                }

                // Count accepted members
                var acceptedCount = 0;
                for (int i = 0; i < prospectsBuffer.Length; i++)
                {
                    if (prospectsBuffer[i].HasAccepted)
                    {
                        acceptedCount++;
                    }
                }

                // Need at least 2 accepted members
                if (acceptedCount < 2)
                {
                    ecb.DestroyEntity(candidateEntity);
                    continue;
                }

                // Create the band
                var bandEntity = ecb.CreateEntity();
                var purpose = DetermineBandPurpose(candidate.ValueRO.SharedGoal, 
                    _sharedGoalCombatDefense, _sharedGoalEscapeDanger, _sharedGoalFindFood, _sharedGoalCooperativeWork);
                var leader = ElectLeader(prospectsBuffer);

                // Calculate initial aggregate alignment from founding members
                var initialAlignment = CalculateAggregateAlignment(prospectsBuffer);

                ecb.AddComponent(bandEntity, new BandIdentity
                {
                    BandName = GenerateBandName(purpose, _bandNameDefenders, _bandNameHunters, _bandNameMiners, _bandNameBuilders, _bandNameWanderers, _bandNameDefault),
                    Purpose = purpose,
                    LeaderEntity = leader,
                    FormationTick = timeState.Tick
                });

                ecb.AddComponent(bandEntity, new BandAggregateStats
                {
                    MemberCount = (ushort)acceptedCount,
                    AverageMorale = 50f,
                    AverageEnergy = 50f,
                    AverageStrength = 50f
                });

                // Add aggregate alignment component (will be synced from members by AggregateStatsSyncSystem)
                ecb.AddComponent(bandEntity, initialAlignment);

                var members = ecb.AddBuffer<BandMember>(bandEntity);
                for (int i = 0; i < prospects.Length; i++)
                {
                    var prospect = prospects[i];
                    if (!prospect.HasAccepted)
                    {
                        continue;
                    }

                    var role = prospect.ProspectEntity == leader ? BandRole.Leader : BandRole.Laborer;
                    members.Add(new BandMember
                    {
                        MemberEntity = prospect.ProspectEntity,
                        JoinedTick = timeState.Tick,
                        Role = role,
                        IsDoubleAgent = false
                    });

                    // Add membership component to member
                    ecb.AddComponent(prospect.ProspectEntity, new BandMembership
                    {
                        BandEntity = bandEntity,
                        JoinedTick = timeState.Tick,
                        Role = role
                    });
                }

                // Add evolution state
                ecb.AddComponent(bandEntity, new BandEvolutionState
                {
                    HasFamilies = false,
                    OriginalGoalCompleted = false,
                    TimeAsRoamingVillage = 0,
                    HasSettlementPlans = false,
                    HasGuildBacking = false,
                    BackingGuildEntity = Entity.Null
                });

                // Destroy the candidate
                ecb.DestroyEntity(candidateEntity);
            }
        }

        private bool ShouldAcceptBandInvitation(Entity entity, BandFormationCandidate candidate)
        {
            // Base acceptance rate
            var acceptChance = 0.6f;

            // Higher mood = more likely to accept
            if (_moodLookup.HasComponent(entity))
            {
                var mood = _moodLookup[entity];
                acceptChance += (mood.Mood - 50f) * 0.005f; // +/-25% based on mood
            }

            // Higher charisma = more likely to accept social interactions
            if (_attributesLookup.HasComponent(entity))
            {
                var attributes = _attributesLookup[entity];
                acceptChance += (attributes.Wisdom - 50f) * 0.003f;
            }

            // Simple deterministic decision based on entity index
            return (entity.Index % 100) < (int)(acceptChance * 100);
        }

        private Entity ElectLeader(DynamicBuffer<BandFormationProspect> prospects)
        {
            Entity bestLeader = Entity.Null;
            float bestScore = -1f;

            for (int i = 0; i < prospects.Length; i++)
            {
                var prospect = prospects[i];
                if (!prospect.HasAccepted)
                {
                    continue;
                }

                var score = 0f;

                // Wisdom stands in as the primary leadership stat
                if (_attributesLookup.HasComponent(prospect.ProspectEntity))
                {
                    var attributes = _attributesLookup[prospect.ProspectEntity];
                    score += attributes.Wisdom * 2f;
                    score += attributes.Intelligence;
                    score += attributes.Wisdom;
                }

                // High mood entities make better leaders
                if (_moodLookup.HasComponent(prospect.ProspectEntity))
                {
                    var mood = _moodLookup[prospect.ProspectEntity];
                    score += mood.Mood * 0.5f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestLeader = prospect.ProspectEntity;
                }
            }

            return bestLeader;
        }

        private static BandPurpose DetermineBandPurpose(
            FixedString128Bytes sharedGoal,
            in FixedString128Bytes goalCombatDefense,
            in FixedString128Bytes goalEscapeDanger,
            in FixedString128Bytes goalFindFood,
            in FixedString128Bytes goalCooperativeWork)
        {
            if (sharedGoal.Equals(goalCombatDefense))
            {
                return BandPurpose.Military_Defense;
            }
            if (sharedGoal.Equals(goalEscapeDanger))
            {
                return BandPurpose.Civilian_Adventuring;
            }
            if (sharedGoal.Equals(goalFindFood))
            {
                return BandPurpose.Work_Hunting;
            }
            if (sharedGoal.Equals(goalCooperativeWork))
            {
                return BandPurpose.Logistics_Construction;
            }

            return BandPurpose.Custom;
        }

        private static FixedString64Bytes GenerateBandName(
            BandPurpose purpose,
            in FixedString64Bytes nameDefenders,
            in FixedString64Bytes nameHunters,
            in FixedString64Bytes nameMiners,
            in FixedString64Bytes nameBuilders,
            in FixedString64Bytes nameWanderers,
            in FixedString64Bytes nameDefault)
        {
            switch (purpose)
            {
                case BandPurpose.Military_Defense:
                case BandPurpose.Military_Warband:
                    return nameDefenders;
                case BandPurpose.Work_Hunting:
                    return nameHunters;
                case BandPurpose.Work_Mining:
                    return nameMiners;
                case BandPurpose.Logistics_Construction:
                    return nameBuilders;
                case BandPurpose.Civilian_Adventuring:
                    return nameWanderers;
                default:
                    return nameDefault;
            }
        }

        private VillagerAlignment CalculateAggregateAlignment(DynamicBuffer<BandFormationProspect> prospects)
        {
            var totalMoral = 0f;
            var totalOrder = 0f;
            var totalPurity = 0f;
            var totalStrength = 0f;
            var count = 0;

            for (int i = 0; i < prospects.Length; i++)
            {
                var prospect = prospects[i];
                if (!prospect.HasAccepted)
                {
                    continue;
                }

                if (_alignmentLookup.HasComponent(prospect.ProspectEntity))
                {
                    var alignment = _alignmentLookup[prospect.ProspectEntity];
                    totalMoral += alignment.MoralAxis;
                    totalOrder += alignment.OrderAxis;
                    totalPurity += alignment.PurityAxis;
                    totalStrength += alignment.AlignmentStrength;
                    count++;
                }
            }

            if (count == 0)
            {
                return new VillagerAlignment
                {
                    MoralAxis = 0,
                    OrderAxis = 0,
                    PurityAxis = 0,
                    AlignmentStrength = 0f
                };
            }

            return new VillagerAlignment
            {
                MoralAxis = (sbyte)math.clamp(totalMoral / count, -100f, 100f),
                OrderAxis = (sbyte)math.clamp(totalOrder / count, -100f, 100f),
                PurityAxis = (sbyte)math.clamp(totalPurity / count, -100f, 100f),
                AlignmentStrength = totalStrength / count
            };
        }
    }
}

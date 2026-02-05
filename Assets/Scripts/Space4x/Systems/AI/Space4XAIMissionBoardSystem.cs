using PureDOTS.Runtime.Components;
using Space4X.Orbitals;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Generates mission offers and assigns them to available ships.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    public partial struct Space4XAIMissionBoardSystem : ISystem
    {
        private EntityQuery _factionQuery;
        private EntityQuery _offerQuery;
        private EntityQuery _agentQuery;
        private EntityQuery _asteroidQuery;
        private EntityQuery _poiQuery;
        private EntityQuery _anomalyQuery;
        private EntityQuery _systemQuery;
        private EntityQuery _colonyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _factionQuery = SystemAPI.QueryBuilder().WithAll<Space4XFaction, FactionResources>().Build();
            _offerQuery = SystemAPI.QueryBuilder().WithAll<Space4XMissionOffer>().Build();
            _agentQuery = SystemAPI.QueryBuilder()
                .WithAll<CaptainOrder, LocalTransform>()
                .WithNone<Space4XMissionAssignment>()
                .Build();
            _asteroidQuery = SystemAPI.QueryBuilder().WithAll<Asteroid, LocalTransform>().Build();
            _poiQuery = SystemAPI.QueryBuilder().WithAll<Space4XPoi, LocalTransform>().Build();
            _anomalyQuery = SystemAPI.QueryBuilder().WithAll<Space4XAnomaly, LocalTransform>().Build();
            _systemQuery = SystemAPI.QueryBuilder().WithAll<Space4XStarSystem, LocalTransform>().Build();
            _colonyQuery = SystemAPI.QueryBuilder().WithAll<Space4XColony, LocalTransform>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var configEntity = EnsureConfig(ref state, out var config, out var boardState);
            var currentTick = time.Tick;

            if (currentTick - boardState.LastGenerationTick < config.GenerationIntervalTicks)
            {
                AssignOffers(ref state, config, currentTick);
                return;
            }

            boardState.LastGenerationTick = currentTick;
            state.EntityManager.SetComponentData(configEntity, boardState);

            GenerateOffers(ref state, config, currentTick);
            AssignOffers(ref state, config, currentTick);
        }

        private static Entity EnsureConfig(ref SystemState state, out Space4XMissionBoardConfig config, out Space4XMissionBoardState boardState)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XMissionBoardConfig>(out var entity))
            {
                entity = state.EntityManager.CreateEntity(typeof(Space4XMissionBoardConfig), typeof(Space4XMissionBoardState));
                config = Space4XMissionBoardConfig.Default;
                boardState = new Space4XMissionBoardState { LastGenerationTick = 0 };
                state.EntityManager.SetComponentData(entity, config);
                state.EntityManager.SetComponentData(entity, boardState);
                return entity;
            }

            config = SystemAPI.GetSingleton<Space4XMissionBoardConfig>();
            boardState = SystemAPI.GetSingleton<Space4XMissionBoardState>();
            return entity;
        }

        private void GenerateOffers(ref SystemState state, in Space4XMissionBoardConfig config, uint currentTick)
        {
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var factions = _factionQuery.ToEntityArray(Allocator.Temp);
            var factionData = _factionQuery.ToComponentDataArray<Space4XFaction>(Allocator.Temp);
            var factionResources = _factionQuery.ToComponentDataArray<FactionResources>(Allocator.Temp);

            var offers = _offerQuery.ToComponentDataArray<Space4XMissionOffer>(Allocator.Temp);
            var offerCounts = new NativeHashMap<ushort, int>(math.max(8, factions.Length), Allocator.Temp);
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                if (offer.Status == Space4XMissionStatus.Completed || offer.Status == Space4XMissionStatus.Failed || offer.Status == Space4XMissionStatus.Expired)
                {
                    continue;
                }

                offerCounts.TryGetValue(offer.IssuerFactionId, out var count);
                offerCounts[offer.IssuerFactionId] = count + 1;
            }

            var asteroids = _asteroidQuery.ToEntityArray(Allocator.Temp);
            var asteroidTransforms = _asteroidQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var asteroidData = _asteroidQuery.ToComponentDataArray<Asteroid>(Allocator.Temp);

            var pois = _poiQuery.ToEntityArray(Allocator.Temp);
            var poiTransforms = _poiQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var poiData = _poiQuery.ToComponentDataArray<Space4XPoi>(Allocator.Temp);

            var anomalies = _anomalyQuery.ToEntityArray(Allocator.Temp);
            var anomalyTransforms = _anomalyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var systems = _systemQuery.ToEntityArray(Allocator.Temp);
            var systemTransforms = _systemQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var systemData = _systemQuery.ToComponentDataArray<Space4XStarSystem>(Allocator.Temp);

            var colonies = _colonyQuery.ToEntityArray(Allocator.Temp);
            var colonyTransforms = _colonyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < factions.Length; i++)
            {
                var factionEntity = factions[i];
                var faction = factionData[i];
                var resources = factionResources[i];
                var factionId = faction.FactionId;

                offerCounts.TryGetValue(factionId, out var existingCount);
                if (existingCount >= config.MaxOffersPerFaction)
                {
                    continue;
                }

                var seed = (uint)(currentTick + 97 * factionId + 1337);
                if (seed == 0)
                {
                    seed = 1u;
                }
                var rng = new Unity.Mathematics.Random(seed);

                // Scout / survey (prefer POIs)
                if (pois.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, pois.Length);
                    var pos = poiTransforms[index].Position;
                    var poi = poiData[index];
                    CreateOffer(ref ecb, factionEntity, factionId, Space4XMissionType.Scout, Entity.Null, pos,
                        0, 0f, poi.Reward * 80f, poi.Reward * 0.05f, poi.Reward * 0.1f, poi.Risk, currentTick, config.OfferExpiryTicks, 2);
                    existingCount++;
                }

                // Mine
                if (asteroids.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, asteroids.Length);
                    var asteroid = asteroidData[index];
                    var units = math.max(25f, asteroid.ResourceAmount * 0.1f);
                    CreateOffer(ref ecb, factionEntity, factionId, Space4XMissionType.Mine, asteroids[index], asteroidTransforms[index].Position,
                        (ushort)asteroid.ResourceType, units, ResolveReward(config, Space4XMissionType.Mine, asteroidTransforms[index].Position, units), 0.04f, 0.08f, 0.2f, currentTick, config.OfferExpiryTicks, 3);
                    existingCount++;
                }

                // Haul delivery
                if (colonies.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, colonies.Length);
                    var destPos = colonyTransforms[index].Position;
                    var units = math.max(20f, resources.Materials * 0.05f + rng.NextFloat(10f, 40f));
                    CreateOffer(ref ecb, factionEntity, factionId, Space4XMissionType.HaulDelivery, colonies[index], destPos,
                        (ushort)rng.NextInt(0, 3), units, ResolveReward(config, Space4XMissionType.HaulDelivery, destPos, units), 0.03f, 0.06f, 0.15f, currentTick, config.OfferExpiryTicks, 4);
                    existingCount++;
                }

                // Haul procurement
                if (colonies.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, colonies.Length);
                    var destPos = colonyTransforms[index].Position;
                    var units = math.max(15f, resources.Energy * 0.04f + rng.NextFloat(8f, 30f));
                    CreateOffer(ref ecb, factionEntity, factionId, Space4XMissionType.HaulProcure, colonies[index], destPos,
                        (ushort)rng.NextInt(0, 3), units, ResolveReward(config, Space4XMissionType.HaulProcure, destPos, units), 0.02f, 0.05f, 0.12f, currentTick, config.OfferExpiryTicks, 4);
                    existingCount++;
                }

                // Patrol
                if (systems.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, systems.Length);
                    var pos = systemTransforms[index].Position;
                    CreateOffer(ref ecb, factionEntity, factionId, Space4XMissionType.Patrol, systems[index], pos,
                        0, 0f, ResolveReward(config, Space4XMissionType.Patrol, pos, 0f), 0.06f, 0.09f, 0.25f, currentTick, config.OfferExpiryTicks, 5);
                    existingCount++;
                }

                // Intercept
                if (anomalies.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, anomalies.Length);
                    var pos = anomalyTransforms[index].Position;
                    CreateOffer(ref ecb, factionEntity, factionId, Space4XMissionType.Intercept, anomalies[index], pos,
                        0, 0f, ResolveReward(config, Space4XMissionType.Intercept, pos, 0f), 0.08f, 0.12f, 0.4f, currentTick, config.OfferExpiryTicks, 6);
                    existingCount++;
                }

                // Build station
                if (systems.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, systems.Length);
                    var basePos = systemTransforms[index].Position;
                    var offset = rng.NextFloat3Direction() * rng.NextFloat(180f, 360f);
                    var pos = basePos + offset;
                    CreateOffer(ref ecb, factionEntity, factionId, Space4XMissionType.BuildStation, systems[index], pos,
                        0, 0f, ResolveReward(config, Space4XMissionType.BuildStation, pos, 0f), 0.1f, 0.15f, 0.35f, currentTick, config.OfferExpiryTicks, 7);
                    existingCount++;
                }
            }

            ecb.Playback(entityManager);
            ecb.Dispose();

            factions.Dispose();
            factionData.Dispose();
            factionResources.Dispose();
            offers.Dispose();
            offerCounts.Dispose();
            asteroids.Dispose();
            asteroidTransforms.Dispose();
            asteroidData.Dispose();
            pois.Dispose();
            poiTransforms.Dispose();
            poiData.Dispose();
            anomalies.Dispose();
            anomalyTransforms.Dispose();
            systems.Dispose();
            systemTransforms.Dispose();
            systemData.Dispose();
            colonies.Dispose();
            colonyTransforms.Dispose();
        }

        private void AssignOffers(ref SystemState state, in Space4XMissionBoardConfig config, uint currentTick)
        {
            var entityManager = state.EntityManager;
            var offers = _offerQuery.ToEntityArray(Allocator.Temp);
            var offerData = _offerQuery.ToComponentDataArray<Space4XMissionOffer>(Allocator.Temp);

            var agents = _agentQuery.ToEntityArray(Allocator.Temp);
            var agentTransforms = _agentQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var agentOrders = _agentQuery.ToComponentDataArray<CaptainOrder>(Allocator.Temp);

            var dispositionLookup = state.GetComponentLookup<EntityDisposition>(true);
            var miningLookup = state.GetComponentLookup<MiningVessel>(true);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var assignments = 0;

            for (int i = 0; i < offers.Length && assignments < config.MaxAssignmentsPerTick; i++)
            {
                var offer = offerData[i];
                if (offer.Status != Space4XMissionStatus.Open)
                {
                    continue;
                }

                if (offer.ExpiryTick != 0 && currentTick >= offer.ExpiryTick)
                {
                    offer.Status = Space4XMissionStatus.Expired;
                    offerData[i] = offer;
                    continue;
                }

                var bestIndex = -1;
                var bestScore = float.MaxValue;

                for (int a = 0; a < agents.Length; a++)
                {
                    if (agentOrders[a].Type != CaptainOrderType.None)
                    {
                        continue;
                    }

                    if (!IsAgentEligible(agents[a], offer.Type, dispositionLookup, miningLookup))
                    {
                        continue;
                    }

                    var dist = math.length(agentTransforms[a].Position - offer.TargetPosition);
                    if (dist < bestScore)
                    {
                        bestScore = dist;
                        bestIndex = a;
                    }
                }

                if (bestIndex < 0)
                {
                    continue;
                }

                var agent = agents[bestIndex];
                var duration = ResolveDuration(config, offer.Type, offer.Risk);
                var dueTick = currentTick + duration;

                var order = agentOrders[bestIndex];
                order.Type = MapMissionToOrder(offer.Type);
                order.Status = CaptainOrderStatus.Received;
                order.TargetEntity = offer.TargetEntity;
                order.TargetPosition = offer.TargetPosition;
                order.Priority = offer.Priority;
                order.IssuedTick = currentTick;
                order.TimeoutTick = dueTick;
                ecb.SetComponent(agent, order);

                ecb.AddComponent(agent, new Space4XMissionAssignment
                {
                    OfferEntity = offers[i],
                    Type = offer.Type,
                    Status = Space4XMissionStatus.Assigned,
                    TargetEntity = offer.TargetEntity,
                    TargetPosition = offer.TargetPosition,
                    ResourceTypeIndex = offer.ResourceTypeIndex,
                    Units = offer.Units,
                    RewardCredits = offer.RewardCredits,
                    RewardStanding = offer.RewardStanding,
                    RewardLp = offer.RewardLp,
                    IssuerFactionId = offer.IssuerFactionId,
                    StartedTick = currentTick,
                    DueTick = dueTick,
                    CompletedTick = 0,
                    AutoComplete = 1
                });

                offer.Status = Space4XMissionStatus.Assigned;
                offer.AssignedEntity = agent;
                offer.AssignedTick = currentTick;
                offerData[i] = offer;
                assignments++;
            }

            for (int i = 0; i < offers.Length; i++)
            {
                ecb.SetComponent(offers[i], offerData[i]);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();

            offers.Dispose();
            offerData.Dispose();
            agents.Dispose();
            agentTransforms.Dispose();
            agentOrders.Dispose();
        }

        private static bool IsAgentEligible(Entity entity, Space4XMissionType type, ComponentLookup<EntityDisposition> dispositionLookup, ComponentLookup<MiningVessel> miningLookup)
        {
            if (type == Space4XMissionType.Mine)
            {
                return miningLookup.HasComponent(entity)
                       || (dispositionLookup.HasComponent(entity) && (dispositionLookup[entity].Flags & EntityDispositionFlags.Mining) != 0);
            }

            if (type == Space4XMissionType.HaulDelivery || type == Space4XMissionType.HaulProcure)
            {
                return !dispositionLookup.HasComponent(entity) || (dispositionLookup[entity].Flags & EntityDispositionFlags.Hauler) != 0;
            }

            if (type == Space4XMissionType.Patrol || type == Space4XMissionType.Intercept)
            {
                return !dispositionLookup.HasComponent(entity) || EntityDispositionUtility.IsCombatant(dispositionLookup[entity].Flags);
            }

            return true;
        }

        private static CaptainOrderType MapMissionToOrder(Space4XMissionType type)
        {
            return type switch
            {
                Space4XMissionType.Scout => CaptainOrderType.Survey,
                Space4XMissionType.Mine => CaptainOrderType.Mine,
                Space4XMissionType.HaulDelivery => CaptainOrderType.Haul,
                Space4XMissionType.HaulProcure => CaptainOrderType.Haul,
                Space4XMissionType.Patrol => CaptainOrderType.Patrol,
                Space4XMissionType.Intercept => CaptainOrderType.Intercept,
                Space4XMissionType.BuildStation => CaptainOrderType.Construct,
                _ => CaptainOrderType.MoveTo
            };
        }

        private static uint ResolveDuration(in Space4XMissionBoardConfig config, Space4XMissionType type, float risk)
        {
            var baseTicks = config.AssignmentBaseTicks;
            var variance = config.AssignmentVarianceTicks;
            var typeScale = type switch
            {
                Space4XMissionType.Mine => 1.4f,
                Space4XMissionType.HaulDelivery => 1.2f,
                Space4XMissionType.HaulProcure => 1.1f,
                Space4XMissionType.Patrol => 1.5f,
                Space4XMissionType.Intercept => 1.0f,
                Space4XMissionType.BuildStation => 2.0f,
                _ => 1.0f
            };

            var riskScale = math.saturate(0.8f + risk);
            var duration = (uint)math.max(60f, baseTicks * typeScale * riskScale + variance * 0.5f);
            return duration;
        }

        private static float ResolveReward(in Space4XMissionBoardConfig config, Space4XMissionType type, float3 position, float units)
        {
            var ringScale = math.saturate(math.length(position) / 8000f);
            var typeScale = type switch
            {
                Space4XMissionType.Mine => 1.1f,
                Space4XMissionType.HaulDelivery => 0.9f,
                Space4XMissionType.HaulProcure => 1.0f,
                Space4XMissionType.Patrol => 1.2f,
                Space4XMissionType.Intercept => 1.4f,
                Space4XMissionType.BuildStation => 1.6f,
                _ => 1f
            };
            return config.BaseReward * typeScale + config.RewardPerUnit * units + config.RewardPerRing * ringScale;
        }

        private static void CreateOffer(
            ref EntityCommandBuffer ecb,
            Entity issuer,
            ushort issuerFactionId,
            Space4XMissionType type,
            Entity targetEntity,
            float3 targetPosition,
            ushort resourceTypeIndex,
            float units,
            float rewardCredits,
            float rewardStanding,
            float rewardLp,
            float risk,
            uint currentTick,
            uint expiryTicks,
            byte priority)
        {
            var offerEntity = ecb.CreateEntity();
            ecb.AddComponent(offerEntity, new Space4XMissionOffer
            {
                Type = type,
                Status = Space4XMissionStatus.Open,
                Issuer = issuer,
                IssuerFactionId = issuerFactionId,
                TargetEntity = targetEntity,
                TargetPosition = targetPosition,
                ResourceTypeIndex = resourceTypeIndex,
                Units = units,
                RewardCredits = rewardCredits,
                RewardStanding = rewardStanding,
                RewardLp = rewardLp,
                Risk = risk,
                Priority = priority,
                CreatedTick = currentTick,
                ExpiryTick = expiryTicks > 0 ? currentTick + expiryTicks : 0,
                AssignedTick = 0,
                CompletedTick = 0,
                AssignedEntity = Entity.Null
            });
        }
    }

    /// <summary>
    /// Resolves mission completion and applies rewards.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4X.Registry.Space4XCaptainOrderSystem))]
    public partial struct Space4XMissionResolutionSystem : ISystem
    {
        private ComponentLookup<Space4XMissionOffer> _offerLookup;
        private ComponentLookup<FactionResources> _resourceLookup;
        private ComponentLookup<Reputation> _reputationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _offerLookup = state.GetComponentLookup<Space4XMissionOffer>(false);
            _resourceLookup = state.GetComponentLookup<FactionResources>(false);
            _reputationLookup = state.GetComponentLookup<Reputation>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            _offerLookup.Update(ref state);
            _resourceLookup.Update(ref state);
            _reputationLookup.Update(ref state);

            var factionMap = new NativeHashMap<ushort, Entity>(16, Allocator.Temp);
            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                factionMap[faction.ValueRO.FactionId] = entity;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var currentTick = time.Tick;

            foreach (var (assignment, order, transform, entity) in SystemAPI.Query<RefRW<Space4XMissionAssignment>, RefRW<CaptainOrder>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!_offerLookup.HasComponent(assignment.ValueRO.OfferEntity))
                {
                    ecb.RemoveComponent<Space4XMissionAssignment>(entity);
                    continue;
                }

                var offer = _offerLookup[assignment.ValueRO.OfferEntity];
                var status = assignment.ValueRO.Status;

                if (offer.Status == Space4XMissionStatus.Completed || offer.Status == Space4XMissionStatus.Failed || offer.Status == Space4XMissionStatus.Expired)
                {
                    ecb.RemoveComponent<Space4XMissionAssignment>(entity);
                    continue;
                }

                var complete = false;
                var failed = false;

                if (currentTick >= assignment.ValueRO.DueTick)
                {
                    if (assignment.ValueRO.AutoComplete != 0)
                    {
                        complete = true;
                    }
                    else
                    {
                        failed = true;
                    }
                }

                var orderValue = order.ValueRO;
                if (orderValue.Type == CaptainOrderType.None && !complete && !failed)
                {
                    complete = true;
                }

                if (!complete && !failed)
                {
                    var dist = math.length(transform.ValueRO.Position - assignment.ValueRO.TargetPosition);
                    if (dist <= 80f)
                    {
                        complete = true;
                    }
                }

                if (orderValue.Status == CaptainOrderStatus.Failed || orderValue.Status == CaptainOrderStatus.Cancelled)
                {
                    failed = true;
                }

                if (!complete && !failed)
                {
                    continue;
                }

                assignment.ValueRW.Status = complete ? Space4XMissionStatus.Completed : Space4XMissionStatus.Failed;
                assignment.ValueRW.CompletedTick = currentTick;
                offer.Status = assignment.ValueRW.Status;
                offer.CompletedTick = currentTick;
                _offerLookup[assignment.ValueRO.OfferEntity] = offer;

                if (complete)
                {
                    ApplyRewards(ref factionMap, assignment.ValueRO);
                }

                order.ValueRW.Type = CaptainOrderType.None;
                order.ValueRW.Status = CaptainOrderStatus.None;
                ecb.RemoveComponent<Space4XMissionAssignment>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            factionMap.Dispose();
        }

        private void ApplyRewards(ref NativeHashMap<ushort, Entity> factionMap, in Space4XMissionAssignment assignment)
        {
            if (assignment.IssuerFactionId == 0)
            {
                return;
            }

            if (!factionMap.TryGetValue(assignment.IssuerFactionId, out var factionEntity))
            {
                return;
            }

            if (_resourceLookup.HasComponent(factionEntity))
            {
                var resources = _resourceLookup[factionEntity];
                resources.Credits += assignment.RewardCredits;
                resources.Influence += assignment.RewardStanding * 2f;
                _resourceLookup[factionEntity] = resources;
            }

            if (_reputationLookup.HasComponent(factionEntity))
            {
                var rep = _reputationLookup[factionEntity];
                rep.ReputationScore = (half)math.saturate((float)rep.ReputationScore + assignment.RewardStanding);
                _reputationLookup[factionEntity] = rep;
            }
        }
    }
}

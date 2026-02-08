using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Transport;
using Space4X.Orbitals;
using Space4X.Registry;
using Space4X.SimServer;
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
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private ComponentLookup<EmpireMembership> _empireMembershipLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private BufferLookup<Space4XContactStanding> _contactLookup;
        private BufferLookup<FactionRelationEntry> _relationLookup;
        private BufferLookup<RacePresence> _raceLookup;
        private BufferLookup<CulturePresence> _cultureLookup;
        private EntityStorageInfoLookup _entityInfoLookup;

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
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _empireMembershipLookup = state.GetComponentLookup<EmpireMembership>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _contactLookup = state.GetBufferLookup<Space4XContactStanding>(true);
            _relationLookup = state.GetBufferLookup<FactionRelationEntry>(true);
            _raceLookup = state.GetBufferLookup<RacePresence>(true);
            _cultureLookup = state.GetBufferLookup<CulturePresence>(true);
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            _affiliationLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _empireMembershipLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _contactLookup.Update(ref state);
            _relationLookup.Update(ref state);
            _raceLookup.Update(ref state);
            _cultureLookup.Update(ref state);
            _entityInfoLookup.Update(ref state);

            var configEntity = EnsureConfig(ref state, out var config, out var boardState);
            var currentTick = time.Tick;

            if (currentTick - boardState.LastGenerationTick < config.GenerationIntervalTicks)
            {
                AssignOffers(ref state, config, currentTick);
                return;
            }

            var hasResourceWeights = TryGetResourceWeights(ref state, out var resourceWeights);
            boardState.LastGenerationTick = currentTick;
            GenerateOffers(ref state, config, currentTick, ref boardState, hasResourceWeights, resourceWeights);
            state.EntityManager.SetComponentData(configEntity, boardState);
            AssignOffers(ref state, config, currentTick);
        }

        private Entity EnsureConfig(ref SystemState state, out Space4XMissionBoardConfig config, out Space4XMissionBoardState boardState)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XMissionBoardConfig>(out var entity))
            {
                entity = state.EntityManager.CreateEntity(typeof(Space4XMissionBoardConfig), typeof(Space4XMissionBoardState));
                config = Space4XMissionBoardConfig.Default;
                boardState = new Space4XMissionBoardState { LastGenerationTick = 0, NextOfferId = 1 };
                state.EntityManager.SetComponentData(entity, config);
                state.EntityManager.SetComponentData(entity, boardState);
                return entity;
            }

            config = SystemAPI.GetSingleton<Space4XMissionBoardConfig>();
            boardState = SystemAPI.GetSingleton<Space4XMissionBoardState>();
            return entity;
        }

        private void GenerateOffers(
            ref SystemState state,
            in Space4XMissionBoardConfig config,
            uint currentTick,
            ref Space4XMissionBoardState boardState,
            bool hasResourceWeights,
            DynamicBuffer<Space4XResourceWeightEntry> resourceWeights)
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
            var anomalyData = _anomalyQuery.ToComponentDataArray<Space4XAnomaly>(Allocator.Temp);

            var systems = _systemQuery.ToEntityArray(Allocator.Temp);
            var systemTransforms = _systemQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var systemData = _systemQuery.ToComponentDataArray<Space4XStarSystem>(Allocator.Temp);

            var colonies = _colonyQuery.ToEntityArray(Allocator.Temp);
            var colonyTransforms = _colonyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var colonyData = _colonyQuery.ToComponentDataArray<Space4XColony>(Allocator.Temp);
            var colonyFactionMap = new NativeHashMap<Entity, ushort>(math.max(8, colonies.Length), Allocator.Temp);
            var colonyPositionMap = new NativeHashMap<Entity, float3>(math.max(8, colonies.Length), Allocator.Temp);
            for (int i = 0; i < colonies.Length; i++)
            {
                var colonyEntity = colonies[i];
                colonyPositionMap[colonyEntity] = colonyTransforms[i].Position;
                if (_affiliationLookup.HasBuffer(colonyEntity))
                {
                    var affiliations = _affiliationLookup[colonyEntity];
                    for (int a = 0; a < affiliations.Length; a++)
                    {
                        var tag = affiliations[a];
                        if (tag.Type != AffiliationType.Faction || !IsValidEntity(tag.Target))
                        {
                            continue;
                        }

                        if (entityManager.HasComponent<Space4XFaction>(tag.Target))
                        {
                            var factionId = entityManager.GetComponentData<Space4XFaction>(tag.Target).FactionId;
                            colonyFactionMap[colonyEntity] = factionId;
                            break;
                        }
                    }
                }
            }

            var hasDemandBuffer = false;
            DynamicBuffer<LogisticsDemandEntry> demandBuffer = default;
            if (SystemAPI.TryGetSingletonEntity<LogisticsBoard>(out var boardEntity))
            {
                demandBuffer = entityManager.GetBuffer<LogisticsDemandEntry>(boardEntity);
                hasDemandBuffer = true;
            }

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
                var usedDemandHaul = false;

                if (hasDemandBuffer && demandBuffer.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    for (int d = 0; d < demandBuffer.Length && existingCount < config.MaxOffersPerFaction; d++)
                    {
                        var demand = demandBuffer[d];
                        if (demand.OutstandingUnits <= 0f)
                        {
                            continue;
                        }

                        if (!IsValidEntity(demand.SiteEntity))
                        {
                            continue;
                        }

                        if (colonyFactionMap.TryGetValue(demand.SiteEntity, out var ownerId) && ownerId != factionId)
                        {
                            continue;
                        }

                        if (!colonyPositionMap.TryGetValue(demand.SiteEntity, out var sitePos))
                        {
                            continue;
                        }

                        var units = math.min(demand.OutstandingUnits, 120f);
                        var reward = ResolveReward(config, Space4XMissionType.HaulProcure, sitePos, units);
                        var priority = demand.Priority > 3 ? demand.Priority : (byte)3;
                        var offerId = NextOfferId(ref boardState);
                        CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.HaulProcure, demand.SiteEntity, sitePos,
                            demand.ResourceTypeIndex, units, reward, 0.03f, 0.06f, 0.2f, currentTick, config.OfferExpiryTicks, priority);
                        existingCount++;
                        usedDemandHaul = true;
                    }
                }

                // Scout / survey (prefer POIs)
                if (pois.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, pois.Length);
                    var pos = poiTransforms[index].Position;
                    var poi = poiData[index];
                    var offerId = NextOfferId(ref boardState);
                    CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Scout, Entity.Null, pos,
                        0, 0f, poi.Reward * 80f, poi.Reward * 0.05f, poi.Reward * 0.1f, poi.Risk, currentTick, config.OfferExpiryTicks, 2);
                    existingCount++;
                }

                // Mine
                if (asteroids.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, asteroids.Length);
                    var asteroid = asteroidData[index];
                    var units = math.max(25f, asteroid.ResourceAmount * 0.1f);
                    var offerId = NextOfferId(ref boardState);
                    CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Mine, asteroids[index], asteroidTransforms[index].Position,
                        (ushort)asteroid.ResourceType, units, ResolveReward(config, Space4XMissionType.Mine, asteroidTransforms[index].Position, units), 0.04f, 0.08f, 0.2f, currentTick, config.OfferExpiryTicks, 3);
                    existingCount++;
                }

                // Haul delivery
                if (colonies.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, colonies.Length);
                    var destPos = colonyTransforms[index].Position;
                    var units = math.max(20f, resources.Materials * 0.05f + rng.NextFloat(10f, 40f));
                    var offerId = NextOfferId(ref boardState);
                    CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.HaulDelivery, colonies[index], destPos,
                        (ushort)rng.NextInt(0, 3), units, ResolveReward(config, Space4XMissionType.HaulDelivery, destPos, units), 0.03f, 0.06f, 0.15f, currentTick, config.OfferExpiryTicks, 4);
                    existingCount++;
                }

                // Haul procurement (fallback if no logistics demand)
                if (!usedDemandHaul && colonies.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, colonies.Length);
                    var destPos = colonyTransforms[index].Position;
                    var units = math.max(15f, resources.Energy * 0.04f + rng.NextFloat(8f, 30f));
                    var offerId = NextOfferId(ref boardState);
                    CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.HaulProcure, colonies[index], destPos,
                        (ushort)rng.NextInt(0, 3), units, ResolveReward(config, Space4XMissionType.HaulProcure, destPos, units), 0.02f, 0.05f, 0.12f, currentTick, config.OfferExpiryTicks, 4);
                    existingCount++;
                }

                // Patrol
                if (systems.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, systems.Length);
                    var pos = systemTransforms[index].Position;
                    var offerId = NextOfferId(ref boardState);
                    CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Patrol, systems[index], pos,
                        0, 0f, ResolveReward(config, Space4XMissionType.Patrol, pos, 0f), 0.06f, 0.09f, 0.25f, currentTick, config.OfferExpiryTicks, 5);
                    existingCount++;
                }

                // Intercept
                if (anomalies.Length > 0 && existingCount < config.MaxOffersPerFaction)
                {
                    var index = rng.NextInt(0, anomalies.Length);
                    var pos = anomalyTransforms[index].Position;
                    var offerId = NextOfferId(ref boardState);
                    CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Intercept, anomalies[index], pos,
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
                    var offerId = NextOfferId(ref boardState);
                    CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.BuildStation, systems[index], pos,
                        0, 0f, ResolveReward(config, Space4XMissionType.BuildStation, pos, 0f), 0.1f, 0.15f, 0.35f, currentTick, config.OfferExpiryTicks, 7);
                    existingCount++;
                }

                // Extra variety (salvage/escort/resupply/trade/repair/survey/expedition/raid/destroy/acquire)
                for (int attempt = 0; attempt < 6 && existingCount < config.MaxOffersPerFaction; attempt++)
                {
                    var pick = rng.NextInt(0, 10);
                    switch (pick)
                    {
                        case 0: // Salvage (POI or anomaly)
                        {
                            if (pois.Length > 0)
                            {
                                var index = rng.NextInt(0, pois.Length);
                                var poi = poiData[index];
                                var pos = poiTransforms[index].Position;
                                var reward = ResolveReward(config, Space4XMissionType.Salvage, pos, 0f) * (1f + (float)poi.Reward);
                                var offerId = NextOfferId(ref boardState);
                                CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Salvage, pois[index], pos,
                                    0, 0f, reward, (float)poi.Reward * 0.06f, (float)poi.Reward * 0.12f, (float)poi.Risk, currentTick, config.OfferExpiryTicks, 4);
                                existingCount++;
                            }
                            else if (anomalies.Length > 0)
                            {
                                var index = rng.NextInt(0, anomalies.Length);
                                var pos = anomalyTransforms[index].Position;
                                var severity = anomalyData[index].Severity;
                                var risk = severity switch
                                {
                                    Space4XAnomalySeverity.Low => 0.15f,
                                    Space4XAnomalySeverity.Moderate => 0.25f,
                                    Space4XAnomalySeverity.Severe => 0.4f,
                                    Space4XAnomalySeverity.Critical => 0.55f,
                                    _ => 0.1f
                                };
                                var reward = ResolveReward(config, Space4XMissionType.Salvage, pos, 0f) * (1f + risk);
                                var offerId = NextOfferId(ref boardState);
                                CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Salvage, anomalies[index], pos,
                                    0, 0f, reward, 0.06f + risk * 0.1f, 0.12f + risk * 0.15f, risk, currentTick, config.OfferExpiryTicks, 4);
                                existingCount++;
                            }
                            break;
                        }
                        case 1: // Escort
                        {
                            if (colonies.Length > 0)
                            {
                                var index = rng.NextInt(0, colonies.Length);
                                var pos = colonyTransforms[index].Position;
                                var reward = ResolveReward(config, Space4XMissionType.Escort, pos, 0f);
                                var offerId = NextOfferId(ref boardState);
                                CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Escort, colonies[index], pos,
                                    0, 0f, reward, 0.04f, 0.08f, 0.12f, currentTick, config.OfferExpiryTicks, 5);
                                existingCount++;
                            }
                            break;
                        }
                        case 2: // Resupply (haul)
                        {
                            if (colonies.Length > 0)
                            {
                                var bestIndex = 0;
                                var bestStored = colonyData[0].StoredResources;
                                for (int j = 1; j < colonies.Length; j++)
                                {
                                    if (colonyData[j].StoredResources < bestStored)
                                    {
                                        bestStored = colonyData[j].StoredResources;
                                        bestIndex = j;
                                    }
                                }

                                var pos = colonyTransforms[bestIndex].Position;
                                var units = math.clamp(60f - bestStored * 0.1f + rng.NextFloat(0f, 40f), 20f, 140f);
                                var resourceIndex = (ushort)(hasResourceWeights
                                    ? Space4XResourceDistributionUtility.RollResource(Space4XResourceBand.Logistics, resourceWeights, ref rng)
                                    : Space4XResourceSelection.SelectMissionCargoResource(ref rng));
                                var reward = ResolveReward(config, Space4XMissionType.Resupply, pos, units);
                                var offerId = NextOfferId(ref boardState);
                                CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Resupply, colonies[bestIndex], pos,
                                    resourceIndex, units, reward, 0.03f, 0.06f, 0.1f, currentTick, config.OfferExpiryTicks, 4);
                                existingCount++;
                            }
                            break;
                        }
                        case 3: // Trade (haul)
                        {
                            if (colonies.Length > 0)
                            {
                                var bestIndex = 0;
                                var bestStored = colonyData[0].StoredResources;
                                for (int j = 1; j < colonies.Length; j++)
                                {
                                    if (colonyData[j].StoredResources > bestStored)
                                    {
                                        bestStored = colonyData[j].StoredResources;
                                        bestIndex = j;
                                    }
                                }

                                var pos = colonyTransforms[bestIndex].Position;
                                var units = math.clamp(bestStored * 0.1f + rng.NextFloat(10f, 40f), 20f, 160f);
                                var resourceIndex = (ushort)(hasResourceWeights
                                    ? Space4XResourceDistributionUtility.RollResource(Space4XResourceBand.Logistics, resourceWeights, ref rng)
                                    : Space4XResourceSelection.SelectMissionCargoResource(ref rng));
                                var reward = ResolveReward(config, Space4XMissionType.Trade, pos, units);
                                var offerId = NextOfferId(ref boardState);
                                CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Trade, colonies[bestIndex], pos,
                                    resourceIndex, units, reward, 0.03f, 0.06f, 0.08f, currentTick, config.OfferExpiryTicks, 4);
                                existingCount++;
                            }
                            break;
                        }
                        case 4: // Repair
                        {
                            var bestIndex = -1;
                            var bestScore = -1;
                            for (int j = 0; j < colonies.Length; j++)
                            {
                                var score = colonyData[j].Status switch
                                {
                                    Space4XColonyStatus.InCrisis => 2,
                                    Space4XColonyStatus.Besieged => 1,
                                    _ => 0
                                };
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestIndex = j;
                                }
                            }

                            if (bestIndex >= 0 && bestScore > 0)
                            {
                                var pos = colonyTransforms[bestIndex].Position;
                                var risk = bestScore == 2 ? 0.22f : 0.16f;
                                var reward = ResolveReward(config, Space4XMissionType.Repair, pos, 0f) * (1f + risk);
                                var offerId = NextOfferId(ref boardState);
                                CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Repair, colonies[bestIndex], pos,
                                    0, 0f, reward, 0.05f, 0.09f, risk, currentTick, config.OfferExpiryTicks, 6);
                                existingCount++;
                            }
                            break;
                        }
                        case 5: // Survey
                        {
                            if (systems.Length > 0)
                            {
                                var index = rng.NextInt(0, systems.Length);
                                var pos = systemTransforms[index].Position;
                                var reward = ResolveReward(config, Space4XMissionType.Survey, pos, 0f);
                                var offerId = NextOfferId(ref boardState);
                                CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Survey, systems[index], pos,
                                    0, 0f, reward, 0.02f, 0.05f, 0.06f, currentTick, config.OfferExpiryTicks, 3);
                                existingCount++;
                            }
                            break;
                        }
                        case 6: // Expedition
                        {
                            if (systems.Length > 0)
                            {
                                var bestIndex = -1;
                                var bestRing = -1;
                                for (int j = 0; j < systems.Length; j++)
                                {
                                    var ring = systemData[j].RingIndex;
                                    if (ring > bestRing)
                                    {
                                        bestRing = ring;
                                        bestIndex = j;
                                    }
                                }

                                if (bestIndex >= 0)
                                {
                                    var pos = systemTransforms[bestIndex].Position;
                                    var ringScale = 0.15f + bestRing * 0.05f;
                                    var reward = ResolveReward(config, Space4XMissionType.Expedition, pos, 0f) * (1f + bestRing * 0.2f);
                                    var offerId = NextOfferId(ref boardState);
                                    CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Expedition, systems[bestIndex], pos,
                                        0, 0f, reward, 0.05f + ringScale * 0.2f, 0.1f + ringScale * 0.3f, ringScale, currentTick, config.OfferExpiryTicks, 6);
                                    existingCount++;
                                }
                            }
                            break;
                        }
                        case 7: // Raid
                        {
                            if (anomalies.Length > 0)
                            {
                                var index = rng.NextInt(0, anomalies.Length);
                                var pos = anomalyTransforms[index].Position;
                                var severity = anomalyData[index].Severity;
                                var risk = severity switch
                                {
                                    Space4XAnomalySeverity.Low => 0.2f,
                                    Space4XAnomalySeverity.Moderate => 0.32f,
                                    Space4XAnomalySeverity.Severe => 0.45f,
                                    Space4XAnomalySeverity.Critical => 0.6f,
                                    _ => 0.18f
                                };
                                var reward = ResolveReward(config, Space4XMissionType.Raid, pos, 0f) * (1f + risk);
                                var offerId = NextOfferId(ref boardState);
                                CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Raid, anomalies[index], pos,
                                    0, 0f, reward, 0.07f + risk * 0.1f, 0.12f + risk * 0.2f, risk, currentTick, config.OfferExpiryTicks, 7);
                                existingCount++;
                            }
                            break;
                        }
                        case 8: // Destroy
                        {
                            if (anomalies.Length > 0)
                            {
                                var index = rng.NextInt(0, anomalies.Length);
                                var pos = anomalyTransforms[index].Position;
                                var severity = anomalyData[index].Severity;
                                var risk = severity switch
                                {
                                    Space4XAnomalySeverity.Low => 0.22f,
                                    Space4XAnomalySeverity.Moderate => 0.35f,
                                    Space4XAnomalySeverity.Severe => 0.5f,
                                    Space4XAnomalySeverity.Critical => 0.65f,
                                    _ => 0.2f
                                };
                                var reward = ResolveReward(config, Space4XMissionType.Destroy, pos, 0f) * (1f + risk * 1.1f);
                                var offerId = NextOfferId(ref boardState);
                                CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Destroy, anomalies[index], pos,
                                    0, 0f, reward, 0.08f + risk * 0.1f, 0.14f + risk * 0.2f, risk, currentTick, config.OfferExpiryTicks, 8);
                                existingCount++;
                            }
                            break;
                        }
                        case 9: // Acquire
                        {
                            if (pois.Length > 0)
                            {
                                var index = rng.NextInt(0, pois.Length);
                                var poi = poiData[index];
                                var pos = poiTransforms[index].Position;
                                var reward = ResolveReward(config, Space4XMissionType.Acquire, pos, 0f) * (1f + (float)poi.Reward * 0.5f);
                                var offerId = NextOfferId(ref boardState);
                                CreateOffer(ref ecb, offerId, factionEntity, factionId, Space4XMissionType.Acquire, pois[index], pos,
                                    0, 0f, reward, (float)poi.Reward * 0.05f, (float)poi.Reward * 0.1f, (float)poi.Risk * 0.8f, currentTick, config.OfferExpiryTicks, 5);
                                existingCount++;
                            }
                            break;
                        }
                    }
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
            anomalyData.Dispose();
            systems.Dispose();
            systemTransforms.Dispose();
            systemData.Dispose();
            colonies.Dispose();
            colonyTransforms.Dispose();
            colonyData.Dispose();
            colonyFactionMap.Dispose();
            colonyPositionMap.Dispose();
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

            var colonies = _colonyQuery.ToEntityArray(Allocator.Temp);
            var colonyTransforms = _colonyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var colonyData = _colonyQuery.ToComponentDataArray<Space4XColony>(Allocator.Temp);
            var colonyFactionMap = new NativeHashMap<Entity, ushort>(math.max(8, colonies.Length), Allocator.Temp);
            for (int i = 0; i < colonies.Length; i++)
            {
                var colonyEntity = colonies[i];
                if (_affiliationLookup.HasBuffer(colonyEntity))
                {
                    var affiliations = _affiliationLookup[colonyEntity];
                    for (int a = 0; a < affiliations.Length; a++)
                    {
                        var tag = affiliations[a];
                        if (tag.Type != AffiliationType.Faction || !IsValidEntity(tag.Target))
                        {
                            continue;
                        }

                        if (entityManager.HasComponent<Space4XFaction>(tag.Target))
                        {
                            var factionId = entityManager.GetComponentData<Space4XFaction>(tag.Target).FactionId;
                            colonyFactionMap[colonyEntity] = factionId;
                            break;
                        }
                    }
                }
            }

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

                if (offer.OfferId == 0)
                {
                    offer.OfferId = ResolveFallbackOfferId(offers[i], currentTick);
                    offerData[i] = offer;
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

                    if (!Space4XStandingUtility.PassesMissionStandingGate(
                            agents[a],
                            offer,
                            _factionLookup,
                            _empireMembershipLookup,
                            _carrierLookup,
                            _affiliationLookup,
                            _contactLookup,
                            _relationLookup,
                            _raceLookup,
                            _cultureLookup))
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
                var isHaul = offer.Type == Space4XMissionType.HaulDelivery
                             || offer.Type == Space4XMissionType.HaulProcure
                             || offer.Type == Space4XMissionType.Resupply
                             || offer.Type == Space4XMissionType.Trade;
                var safeOfferTarget = IsValidEntity(offer.TargetEntity) ? offer.TargetEntity : Entity.Null;
                var sourceEntity = safeOfferTarget;
                var sourcePos = offer.TargetPosition;
                var destPos = offer.TargetPosition;

                if (isHaul)
                {
                    ResolveHaulEndpoints(
                        colonies,
                        colonyTransforms,
                        colonyData,
                        colonyFactionMap,
                        offer.TargetEntity,
                        offer.TargetPosition,
                        offer.IssuerFactionId,
                        offer.Type == Space4XMissionType.HaulDelivery
                        || offer.Type == Space4XMissionType.Resupply
                        || offer.Type == Space4XMissionType.Trade,
                        out sourceEntity,
                        out sourcePos,
                        out destPos);
                }

                var order = agentOrders[bestIndex];
                order.Type = MapMissionToOrder(offer.Type);
                order.Status = CaptainOrderStatus.Received;
                order.TargetEntity = isHaul ? sourceEntity : safeOfferTarget;
                order.TargetPosition = isHaul ? sourcePos : offer.TargetPosition;
                order.Priority = offer.Priority;
                order.IssuedTick = currentTick;
                order.TimeoutTick = dueTick;
                ecb.SetComponent(agent, order);

                ecb.AddComponent(agent, new Space4XMissionAssignment
                {
                    OfferEntity = offers[i],
                    OfferId = offer.OfferId,
                    Type = offer.Type,
                    Status = Space4XMissionStatus.Assigned,
                    TargetEntity = safeOfferTarget,
                    TargetPosition = isHaul ? sourcePos : offer.TargetPosition,
                    SourceEntity = sourceEntity,
                    SourcePosition = sourcePos,
                    DestinationPosition = destPos,
                    Phase = isHaul ? Space4XMissionPhase.ToSource : Space4XMissionPhase.None,
                    CargoState = Space4XMissionCargoState.None,
                    ResourceTypeIndex = offer.ResourceTypeIndex,
                    Units = offer.Units,
                    CargoUnits = 0f,
                    RewardCredits = offer.RewardCredits,
                    RewardStanding = offer.RewardStanding,
                    RewardLp = offer.RewardLp,
                    IssuerFactionId = offer.IssuerFactionId,
                    StartedTick = currentTick,
                    DueTick = dueTick,
                    CompletedTick = 0,
                    AutoComplete = (byte)(isHaul ? 0 : 1)
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
            colonies.Dispose();
            colonyTransforms.Dispose();
            colonyData.Dispose();
            colonyFactionMap.Dispose();
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityInfoLookup.Exists(entity);
        }

        private bool TryGetResourceWeights(ref SystemState state, out DynamicBuffer<Space4XResourceWeightEntry> weights)
        {
            weights = default;
            if (!SystemAPI.TryGetSingletonEntity<Space4XResourceDistributionConfig>(out var entity))
            {
                return false;
            }

            if (!state.EntityManager.HasBuffer<Space4XResourceWeightEntry>(entity))
            {
                return false;
            }

            weights = state.EntityManager.GetBuffer<Space4XResourceWeightEntry>(entity);
            if (weights.Length != (int)ResourceType.Count)
            {
                Space4XResourceDistributionDefaults.PopulateDefaults(ref weights);
            }

            return weights.Length > 0;
        }

        private static bool IsAgentEligible(Entity entity, Space4XMissionType type, ComponentLookup<EntityDisposition> dispositionLookup, ComponentLookup<MiningVessel> miningLookup)
        {
            if (type == Space4XMissionType.Mine)
            {
                return miningLookup.HasComponent(entity)
                       || (dispositionLookup.HasComponent(entity) && (dispositionLookup[entity].Flags & EntityDispositionFlags.Mining) != 0);
            }

            if (type == Space4XMissionType.HaulDelivery
                || type == Space4XMissionType.HaulProcure
                || type == Space4XMissionType.Resupply
                || type == Space4XMissionType.Trade)
            {
                if (!dispositionLookup.HasComponent(entity))
                {
                    return true;
                }

                var flags = dispositionLookup[entity].Flags;
                return (flags & (EntityDispositionFlags.Hauler | EntityDispositionFlags.Trader | EntityDispositionFlags.Support)) != 0;
            }

            if (type == Space4XMissionType.Patrol
                || type == Space4XMissionType.Intercept
                || type == Space4XMissionType.Raid
                || type == Space4XMissionType.Destroy
                || type == Space4XMissionType.Escort)
            {
                return !dispositionLookup.HasComponent(entity) || EntityDispositionUtility.IsCombatant(dispositionLookup[entity].Flags);
            }

            if (type == Space4XMissionType.Repair)
            {
                if (!dispositionLookup.HasComponent(entity))
                {
                    return true;
                }

                return (dispositionLookup[entity].Flags & EntityDispositionFlags.Support) != 0;
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
                Space4XMissionType.Salvage => CaptainOrderType.Rescue,
                Space4XMissionType.Escort => CaptainOrderType.Escort,
                Space4XMissionType.Resupply => CaptainOrderType.Resupply,
                Space4XMissionType.Trade => CaptainOrderType.Trade,
                Space4XMissionType.Repair => CaptainOrderType.Repair,
                Space4XMissionType.Survey => CaptainOrderType.Survey,
                Space4XMissionType.Expedition => CaptainOrderType.Survey,
                Space4XMissionType.Raid => CaptainOrderType.Attack,
                Space4XMissionType.Destroy => CaptainOrderType.Attack,
                Space4XMissionType.Acquire => CaptainOrderType.MoveTo,
                _ => CaptainOrderType.MoveTo
            };
        }

        private static uint NextOfferId(ref Space4XMissionBoardState boardState)
        {
            var next = boardState.NextOfferId;
            if (next == 0)
            {
                next = 1u;
            }

            boardState.NextOfferId = next + 1u;
            if (boardState.NextOfferId == 0)
            {
                boardState.NextOfferId = 1u;
            }

            return next;
        }

        private static uint ResolveFallbackOfferId(Entity offerEntity, uint tick)
        {
            var seed = math.hash(new uint2((uint)offerEntity.Index + 1u, tick + 31u));
            return seed == 0 ? 1u : seed;
        }

        private static void ResolveHaulEndpoints(
            NativeArray<Entity> colonies,
            NativeArray<LocalTransform> colonyTransforms,
            NativeArray<Space4XColony> colonyData,
            NativeHashMap<Entity, ushort> colonyFactionMap,
            Entity destinationEntity,
            float3 destinationPosition,
            ushort issuerFactionId,
            bool preferIssuer,
            out Entity sourceEntity,
            out float3 sourcePosition,
            out float3 resolvedDestination)
        {
            sourceEntity = destinationEntity;
            sourcePosition = destinationPosition;
            resolvedDestination = destinationPosition;

            if (colonies.Length == 0)
            {
                return;
            }

            var bestIndex = -1;
            var bestScore = -1f;

            if (preferIssuer && issuerFactionId != 0)
            {
                for (int i = 0; i < colonies.Length; i++)
                {
                    if (colonies[i] == destinationEntity)
                    {
                        continue;
                    }

                    if (colonyFactionMap.TryGetValue(colonies[i], out var factionId) && factionId != issuerFactionId)
                    {
                        continue;
                    }

                    var score = colonyData[i].StoredResources;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }
            }

            if (bestIndex < 0)
            {
                for (int i = 0; i < colonies.Length; i++)
                {
                    if (colonies[i] == destinationEntity)
                    {
                        continue;
                    }

                    var score = colonyData[i].StoredResources;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }
            }

            if (bestIndex >= 0)
            {
                sourceEntity = colonies[bestIndex];
                sourcePosition = colonyTransforms[bestIndex].Position;
            }
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
                Space4XMissionType.Salvage => 1.3f,
                Space4XMissionType.Escort => 1.2f,
                Space4XMissionType.Resupply => 1.1f,
                Space4XMissionType.Trade => 1.1f,
                Space4XMissionType.Repair => 1.3f,
                Space4XMissionType.Survey => 1.1f,
                Space4XMissionType.Expedition => 2.2f,
                Space4XMissionType.Raid => 1.2f,
                Space4XMissionType.Destroy => 1.3f,
                Space4XMissionType.Acquire => 1.2f,
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
                Space4XMissionType.Salvage => 1.3f,
                Space4XMissionType.Escort => 1.1f,
                Space4XMissionType.Resupply => 1.0f,
                Space4XMissionType.Trade => 1.0f,
                Space4XMissionType.Repair => 1.2f,
                Space4XMissionType.Survey => 0.9f,
                Space4XMissionType.Expedition => 1.5f,
                Space4XMissionType.Raid => 1.5f,
                Space4XMissionType.Destroy => 1.6f,
                Space4XMissionType.Acquire => 1.2f,
                _ => 1f
            };
            return config.BaseReward * typeScale + config.RewardPerUnit * units + config.RewardPerRing * ringScale;
        }

        private static void CreateOffer(
            ref EntityCommandBuffer ecb,
            uint offerId,
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
                OfferId = offerId,
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
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4X.Registry.Space4XCaptainOrderSystem))]
    public partial struct Space4XMissionResolutionSystem : ISystem
    {
        private ComponentLookup<Space4XMissionOffer> _offerLookup;
        private ComponentLookup<FactionResources> _resourceLookup;
        private ComponentLookup<Reputation> _reputationLookup;
        private ComponentLookup<Space4XColony> _colonyLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private BufferLookup<Space4XContactStanding> _contactLookup;
        private EntityStorageInfoLookup _entityInfoLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _offerLookup = state.GetComponentLookup<Space4XMissionOffer>(false);
            _resourceLookup = state.GetComponentLookup<FactionResources>(false);
            _reputationLookup = state.GetComponentLookup<Reputation>(false);
            _colonyLookup = state.GetComponentLookup<Space4XColony>(false);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _contactLookup = state.GetBufferLookup<Space4XContactStanding>(false);
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
        }

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
            _colonyLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _contactLookup.Update(ref state);
            _entityInfoLookup.Update(ref state);

            var factionMap = new NativeHashMap<ushort, Entity>(16, Allocator.Temp);
            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                factionMap[faction.ValueRO.FactionId] = entity;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var currentTick = time.Tick;
            var contactConfig = EnsureContactConfig(ref state);

            foreach (var (assignment, order, transform, entity) in SystemAPI.Query<RefRW<Space4XMissionAssignment>, RefRW<CaptainOrder>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!IsValidEntity(assignment.ValueRO.OfferEntity) || !_offerLookup.HasComponent(assignment.ValueRO.OfferEntity))
                {
                    ecb.RemoveComponent<Space4XMissionAssignment>(entity);
                    continue;
                }

                var offer = _offerLookup[assignment.ValueRO.OfferEntity];
                if (offer.Status == Space4XMissionStatus.Completed || offer.Status == Space4XMissionStatus.Failed || offer.Status == Space4XMissionStatus.Expired)
                {
                    ecb.RemoveComponent<Space4XMissionAssignment>(entity);
                    continue;
                }

                var assignmentValue = assignment.ValueRW;
                var orderValue = order.ValueRW;
                var complete = false;
                var failed = false;
                var isHaul = assignmentValue.Type == Space4XMissionType.HaulDelivery
                             || assignmentValue.Type == Space4XMissionType.HaulProcure
                             || assignmentValue.Type == Space4XMissionType.Resupply
                             || assignmentValue.Type == Space4XMissionType.Trade;

                if (assignmentValue.DueTick > 0 && currentTick >= assignmentValue.DueTick)
                {
                    if (assignmentValue.AutoComplete != 0)
                    {
                        complete = true;
                    }
                    else
                    {
                        failed = true;
                    }
                }

                if (!complete && !failed)
                {
                    if (isHaul)
                    {
                        if (assignmentValue.Phase == Space4XMissionPhase.None)
                        {
                            assignmentValue.Phase = Space4XMissionPhase.ToSource;
                        }

                        var loadRadius = 80f;
                        var dropoffRadius = 90f;
                        if (assignmentValue.Phase == Space4XMissionPhase.ToSource)
                        {
                            var dist = math.length(transform.ValueRO.Position - assignmentValue.SourcePosition);
                            if (dist <= loadRadius)
                            {
                                var loadAmount = ResolveCargoLoad(ref assignmentValue);
                                assignmentValue.CargoUnits = loadAmount;
                                assignmentValue.CargoState = loadAmount > 0f ? Space4XMissionCargoState.Loaded : Space4XMissionCargoState.Loading;
                                assignmentValue.Phase = Space4XMissionPhase.ToDestination;
                                assignmentValue.TargetPosition = assignmentValue.DestinationPosition;
                                orderValue.TargetEntity = assignmentValue.TargetEntity;
                                orderValue.TargetPosition = assignmentValue.DestinationPosition;
                                orderValue.Status = CaptainOrderStatus.Received;
                            }
                        }
                        else if (assignmentValue.Phase == Space4XMissionPhase.ToDestination)
                        {
                            var dist = math.length(transform.ValueRO.Position - assignmentValue.DestinationPosition);
                            if (dist <= dropoffRadius)
                            {
                                var delivered = assignmentValue.CargoUnits > 0f ? assignmentValue.CargoUnits : assignmentValue.Units;
                                ApplyCargoDelivery(assignmentValue.TargetEntity, delivered);
                                assignmentValue.CargoState = Space4XMissionCargoState.Delivered;
                                complete = true;
                            }
                        }

                        if (orderValue.Status == CaptainOrderStatus.Failed || orderValue.Status == CaptainOrderStatus.Cancelled)
                        {
                            failed = true;
                        }
                    }
                    else
                    {
                        if (orderValue.Type == CaptainOrderType.None)
                        {
                            complete = true;
                        }

                        if (!complete)
                        {
                            var dist = math.length(transform.ValueRO.Position - assignmentValue.TargetPosition);
                            if (dist <= 80f)
                            {
                                complete = true;
                            }
                        }

                        if (orderValue.Status == CaptainOrderStatus.Failed || orderValue.Status == CaptainOrderStatus.Cancelled)
                        {
                            failed = true;
                        }
                    }
                }

                assignment.ValueRW = assignmentValue;
                order.ValueRW = orderValue;

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
                    var rewardScale = ResolveRewardScale(assignment.ValueRO);
                    ApplyRewards(ref state, ref factionMap, entity, assignment.ValueRO, rewardScale, contactConfig);
                }

                order.ValueRW.Type = CaptainOrderType.None;
                order.ValueRW.Status = CaptainOrderStatus.None;
                ecb.RemoveComponent<Space4XMissionAssignment>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            factionMap.Dispose();
        }

        private Space4XContactTierConfig EnsureContactConfig(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton(out Space4XContactTierConfig config))
            {
                return config;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XContactTierConfig));
            config = Space4XContactTierConfig.Default;
            state.EntityManager.SetComponentData(entity, config);
            return config;
        }

        private float ResolveCargoLoad(ref Space4XMissionAssignment assignment)
        {
            var loadAmount = assignment.Units;
            if (!IsValidEntity(assignment.SourceEntity) || !_colonyLookup.HasComponent(assignment.SourceEntity))
            {
                return loadAmount;
            }

            var colony = _colonyLookup[assignment.SourceEntity];
            var available = math.max(0f, colony.StoredResources);
            loadAmount = math.min(loadAmount, available);
            colony.StoredResources = math.max(0f, colony.StoredResources - loadAmount);
            _colonyLookup[assignment.SourceEntity] = colony;
            return loadAmount;
        }

        private void ApplyCargoDelivery(Entity destinationEntity, float units)
        {
            if (!IsValidEntity(destinationEntity) || units <= 0f)
            {
                return;
            }

            if (!_colonyLookup.HasComponent(destinationEntity))
            {
                return;
            }

            var colony = _colonyLookup[destinationEntity];
            colony.StoredResources += units;
            _colonyLookup[destinationEntity] = colony;
        }

        private static float ResolveRewardScale(in Space4XMissionAssignment assignment)
        {
            if (assignment.Type == Space4XMissionType.HaulDelivery
                || assignment.Type == Space4XMissionType.HaulProcure
                || assignment.Type == Space4XMissionType.Resupply
                || assignment.Type == Space4XMissionType.Trade)
            {
                if (assignment.Units > 0f)
                {
                    return math.saturate(assignment.CargoUnits / assignment.Units);
                }
            }

            return 1f;
        }

        private void ApplyRewards(ref SystemState state, ref NativeHashMap<ushort, Entity> factionMap, Entity agentEntity,
            in Space4XMissionAssignment assignment, float rewardScale, in Space4XContactTierConfig contactConfig)
        {
            if (assignment.IssuerFactionId == 0)
            {
                return;
            }

            if (!factionMap.TryGetValue(assignment.IssuerFactionId, out var issuerFaction))
            {
                return;
            }

            var recipientFaction = issuerFaction;
            if (TryResolveAgentFaction(ref state, agentEntity, out var agentFaction, out var agentFactionId))
            {
                recipientFaction = agentFaction;

                if (agentFactionId != 0 && agentFactionId != assignment.IssuerFactionId)
                {
                    var lpDelta = assignment.RewardLp * rewardScale;
                    if (lpDelta <= 0f)
                    {
                        lpDelta = assignment.RewardCredits * rewardScale * contactConfig.LpPerReward;
                    }

                    UpdateContactStanding(agentFaction, assignment.IssuerFactionId,
                        assignment.RewardStanding * rewardScale,
                        lpDelta,
                        contactConfig);
                }
            }

            if (_resourceLookup.HasComponent(recipientFaction))
            {
                var resources = _resourceLookup[recipientFaction];
                resources.Credits += assignment.RewardCredits * rewardScale;
                resources.Influence += assignment.RewardStanding * 2f * rewardScale;
                _resourceLookup[recipientFaction] = resources;
            }

            if (_reputationLookup.HasComponent(recipientFaction))
            {
                var rep = _reputationLookup[recipientFaction];
                rep.ReputationScore = (half)math.saturate((float)rep.ReputationScore + assignment.RewardStanding * rewardScale);
                _reputationLookup[recipientFaction] = rep;
            }
        }

        private bool TryResolveAgentFaction(ref SystemState state, Entity agent, out Entity factionEntity, out ushort factionId)
        {
            factionEntity = Entity.Null;
            factionId = 0;

            if (_carrierLookup.HasComponent(agent))
            {
                var carrier = _carrierLookup[agent];
                if (IsValidEntity(carrier.AffiliationEntity))
                {
                    factionEntity = carrier.AffiliationEntity;
                }
            }

            if (factionEntity == Entity.Null && _affiliationLookup.HasBuffer(agent))
            {
                var affiliations = _affiliationLookup[agent];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var tag = affiliations[i];
                    if (tag.Type == AffiliationType.Faction && IsValidEntity(tag.Target))
                    {
                        factionEntity = tag.Target;
                        break;
                    }
                }
            }

            if (factionEntity == Entity.Null || !state.EntityManager.HasComponent<Space4XFaction>(factionEntity))
            {
                return false;
            }

            factionId = state.EntityManager.GetComponentData<Space4XFaction>(factionEntity).FactionId;
            return true;
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityInfoLookup.Exists(entity);
        }

        private void UpdateContactStanding(Entity factionEntity, ushort contactFactionId, float standingDelta, float lpDelta, in Space4XContactTierConfig config)
        {
            if (!_contactLookup.HasBuffer(factionEntity))
            {
                return;
            }

            var buffer = _contactLookup[factionEntity];
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ContactFactionId != contactFactionId)
                {
                    continue;
                }

                var entry = buffer[i];
                var standing = math.saturate((float)entry.Standing + standingDelta * config.StandingGainScale);
                entry.Standing = (half)standing;
                entry.LoyaltyPoints += lpDelta;
                entry.Tier = Space4XContactTierUtility.ResolveTier(standing, config);
                buffer[i] = entry;
                return;
            }

            var baseStanding = math.saturate(0.2f + standingDelta * config.StandingGainScale);
            buffer.Add(new Space4XContactStanding
            {
                ContactFactionId = contactFactionId,
                Standing = (half)baseStanding,
                LoyaltyPoints = math.max(0f, lpDelta),
                Tier = Space4XContactTierUtility.ResolveTier(baseStanding, config)
            });
        }
    }
}

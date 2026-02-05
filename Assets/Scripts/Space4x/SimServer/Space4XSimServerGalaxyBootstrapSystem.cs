using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.WorldGen;
using Space4X.Orbitals;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.SimServer
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XSimServerBootstrapSystem))]
    public partial struct Space4XSimServerGalaxyBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!Space4XSimServerSettings.IsEnabled())
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XSimServerConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XSimServerGalaxyBootstrapped>(out _))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out Space4XSimServerConfig config))
            {
                return;
            }

            if (!SystemAPI.QueryBuilder().WithAll<Space4XFaction>().Build().IsEmptyIgnoreFilter)
            {
                MarkBootstrapped(ref state);
                return;
            }

            var factionRng = new Unity.Mathematics.Random(math.max(config.Seed, 1u));
            var contentSeed = math.max(config.Seed, 1u) ^ 0x9E3779B9u;
            if (contentSeed == 0u)
            {
                contentSeed = 1u;
            }
            var contentRng = new Unity.Mathematics.Random(contentSeed);
            var entityManager = state.EntityManager;
            const ushort resourceKindCount = (ushort)(ResourceType.Ore + 1);

            var generationConfig = GalaxyGenerationConfig.FromBase(
                config.Seed,
                config.FactionCount,
                config.SystemsPerFaction,
                config.ResourcesPerSystem,
                config.StartRadius,
                config.SystemSpacing,
                config.ResourceBaseUnits,
                config.ResourceRichnessGradient,
                resourceKindCount);

            var factionSeeds = new NativeList<GalaxyFactionSeed>(config.FactionCount, Allocator.Temp);
            var systemSeeds = new NativeList<GalaxySystemSeed>(math.max(1, config.FactionCount) * math.max(1, config.SystemsPerFaction), Allocator.Temp);
            var resourceSeeds = new NativeList<GalaxyResourceSeed>(math.max(1, config.FactionCount) * math.max(1, config.SystemsPerFaction) * config.ResourcesPerSystem, Allocator.Temp);
            var anomalySeeds = new NativeList<GalaxyAnomalySeed>(Allocator.Temp);

            GalaxyGeneration.Generate(generationConfig, ref factionSeeds, ref systemSeeds, ref resourceSeeds, ref anomalySeeds);

            var factionEntities = new NativeArray<Entity>(factionSeeds.Length + 1, Allocator.Temp);

            for (int i = 0; i < factionSeeds.Length; i++)
            {
                var factionId = factionSeeds[i].FactionId;
                var outlook = RandomOutlook(ref factionRng);
                var faction = Space4XFaction.Empire(factionId, outlook);
                faction.Aggression = (half)math.clamp(factionRng.NextFloat(0.2f, 0.7f), 0f, 1f);
                faction.RiskTolerance = (half)math.clamp(factionRng.NextFloat(0.3f, 0.7f), 0f, 1f);
                faction.ExpansionDrive = (half)math.clamp(factionRng.NextFloat(0.3f, 0.8f), 0f, 1f);
                faction.TradeFocus = (half)math.clamp(factionRng.NextFloat(0.2f, 0.8f), 0f, 1f);
                faction.ResearchFocus = (half)math.clamp(factionRng.NextFloat(0.2f, 0.8f), 0f, 1f);
                faction.MilitaryFocus = (half)math.clamp(factionRng.NextFloat(0.2f, 0.8f), 0f, 1f);

                var factionEntity = entityManager.CreateEntity(
                    typeof(AffiliationRelation),
                    typeof(Space4XFaction),
                    typeof(FactionResources),
                    typeof(Space4XTerritoryControl),
                    typeof(TechLevel),
                    typeof(TechDiffusionState),
                    typeof(Space4XSimServerTag));

                entityManager.SetComponentData(factionEntity, faction);
                entityManager.SetComponentData(factionEntity, new AffiliationRelation
                {
                    AffiliationName = new FixedString64Bytes($"Empire-{factionId:00}")
                });

                entityManager.SetComponentData(factionEntity, new FactionResources
                {
                    Credits = factionRng.NextFloat(600f, 1400f),
                    Materials = factionRng.NextFloat(400f, 900f),
                    Energy = factionRng.NextFloat(250f, 650f),
                    Influence = factionRng.NextFloat(50f, 150f),
                    Research = factionRng.NextFloat(10f, 40f),
                    IncomeRate = factionRng.NextFloat(2f, 6f),
                    ExpenseRate = factionRng.NextFloat(1f, 4f)
                });

                entityManager.SetComponentData(factionEntity, new Space4XTerritoryControl
                {
                    ControlledSystems = 1,
                    ColonyCount = 1,
                    OutpostCount = 0,
                    ContestedSectors = 0,
                    FleetStrength = factionRng.NextFloat(50f, 120f),
                    EconomicOutput = factionRng.NextFloat(20f, 60f),
                    Population = (uint)factionRng.NextInt(200_000, 700_000),
                    ExpansionRate = (half)0.05f
                });

                entityManager.AddBuffer<FactionRelationEntry>(factionEntity);
                entityManager.AddComponentData(factionEntity, new Space4XFactionDirective
                {
                    Security = (float)faction.MilitaryFocus,
                    Economy = (float)faction.TradeFocus,
                    Research = (float)faction.ResearchFocus,
                    Expansion = (float)faction.ExpansionDrive,
                    Diplomacy = 0.5f,
                    Production = (float)faction.TradeFocus,
                    Food = 0.5f,
                    Priority = 0.25f,
                    LastUpdatedTick = 0,
                    ExpiresAtTick = 0,
                    DirectiveId = new FixedString64Bytes("default")
                });

                CreateFactionLeadership(entityManager, factionEntity, faction, ref factionRng);

                entityManager.SetComponentData(factionEntity, new TechLevel
                {
                    MiningTech = 0,
                    CombatTech = 0,
                    HaulingTech = 0,
                    ProcessingTech = 0,
                    LastUpgradeTick = 0
                });

                entityManager.SetComponentData(factionEntity, new TechDiffusionState
                {
                    SourceEntity = factionEntity,
                    DiffusionProgressSeconds = 0f,
                    DiffusionDurationSeconds = math.max(60f, config.TechDiffusionDurationSeconds),
                    TargetMiningTech = 1,
                    TargetCombatTech = 0,
                    TargetHaulingTech = 1,
                    TargetProcessingTech = 1,
                    Active = 1,
                    DiffusionStartTick = 0
                });

                factionEntities[factionId] = factionEntity;
            }

            for (int i = 0; i < systemSeeds.Length; i++)
            {
                var seed = systemSeeds[i];
                CreateStarSystem(entityManager, seed.SystemId, seed.Position, seed.RingIndex, seed.HomeFactionId);

                if (seed.HomeFactionId != 0)
                {
                    var factionEntity = factionEntities[seed.HomeFactionId];
                    if (factionEntity != Entity.Null)
                    {
                        SpawnColony(entityManager, factionEntity, seed.Position + new float3(80f, 0f, 20f), $"colony-{seed.HomeFactionId:00}", ref contentRng);
                    }
                }

                SpawnSystemContent(entityManager, seed, ref contentRng, factionEntities);
            }

            var maxResourceKind = (ushort)ResourceType.Ore;
            var resourceKindModulo = (ushort)(maxResourceKind + 1);
            for (int i = 0; i < resourceSeeds.Length; i++)
            {
                var seed = resourceSeeds[i];
                var kindIndex = (ushort)(seed.KindIndex % resourceKindModulo);
                var resourceType = (ResourceType)kindIndex;
                var resourceId = resourceType.ToString();
                var asteroidId = $"ast-{seed.RingIndex}-{seed.LocalIndex}-{seed.RandomSuffix:0000}";
                CreateResourceDeposit(entityManager, seed.Position, resourceType, resourceId, asteroidId, seed.Units);
            }

            for (int i = 0; i < anomalySeeds.Length; i++)
            {
                var seed = anomalySeeds[i];
                var severity = seed.RingIndex >= 3 ? Space4XAnomalySeverity.Severe : Space4XAnomalySeverity.Moderate;
                CreateAnomaly(entityManager, seed.Position, severity, seed.RingIndex);
            }

            factionEntities.Dispose();
            factionSeeds.Dispose();
            systemSeeds.Dispose();
            resourceSeeds.Dispose();
            anomalySeeds.Dispose();

            MarkBootstrapped(ref state);
            Debug.Log($"[Space4XSimServer] Galaxy initialized: factions={config.FactionCount} systemsPerFaction={config.SystemsPerFaction} resourcesPerSystem={config.ResourcesPerSystem}.");
        }

        private static void MarkBootstrapped(ref SystemState state)
        {
            var entity = state.EntityManager.CreateEntity(typeof(Space4XSimServerGalaxyBootstrapped));
            state.EntityManager.AddComponent<Space4XSimServerTag>(entity);
        }

        private static Entity CreateStarSystem(EntityManager entityManager, ushort id, float3 position, byte ring, ushort ownerFactionId)
        {
            var entity = entityManager.CreateEntity(
                typeof(Space4XStarSystem),
                typeof(LocalTransform),
                typeof(SpatialIndexedTag),
                typeof(Space4XSimServerTag));

            entityManager.SetComponentData(entity, new Space4XStarSystem
            {
                SystemId = id,
                OwnerFactionId = ownerFactionId,
                RingIndex = ring
            });
            entityManager.SetComponentData(entity, LocalTransform.FromPosition(position));

            return entity;
        }

        private static void SpawnColony(EntityManager entityManager, Entity factionEntity, float3 position, string colonyId, ref Unity.Mathematics.Random rng)
        {
            var colony = entityManager.CreateEntity(
                typeof(Space4XColony),
                typeof(LocalTransform),
                typeof(SpatialIndexedTag),
                typeof(Space4XSimServerTag));

            entityManager.SetComponentData(colony, LocalTransform.FromPosition(position));
            entityManager.SetComponentData(colony, new Space4XColony
            {
                ColonyId = new FixedString64Bytes(colonyId),
                Population = 350_000f,
                StoredResources = 800f,
                Status = Space4XColonyStatus.Growing,
                SectorId = 0
            });

            var affiliation = entityManager.AddBuffer<AffiliationTag>(colony);
            affiliation.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = factionEntity,
                Loyalty = (half)1f
            });

            InitializeMarket(entityManager, colony, MarketLocationType.Colony, MarketSize.Medium, entityManager.GetComponentData<Space4XFaction>(factionEntity).FactionId, ref rng);
        }

        private static void CreateAnomaly(EntityManager entityManager, float3 position, Space4XAnomalySeverity severity, int ring)
        {
            var entity = entityManager.CreateEntity(
                typeof(Space4XAnomaly),
                typeof(LocalTransform),
                typeof(SpatialIndexedTag),
                typeof(Space4XSimServerTag));

            entityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
            entityManager.SetComponentData(entity, new Space4XAnomaly
            {
                AnomalyId = new FixedString64Bytes($"anomaly-{ring}-{math.abs((int)position.x):0000}"),
                Classification = new FixedString64Bytes("Hazard"),
                Severity = severity,
                State = Space4XAnomalyState.Active,
                Instability = math.clamp(0.4f + ring * 0.2f, 0.4f, 1f),
                SectorId = ring
            });
        }

        private static void CreateResourceDeposit(EntityManager entityManager, float3 position, ResourceType type, string resourceId, string asteroidId, float unitsRemaining)
        {
            var entity = entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(SpatialIndexedTag),
                typeof(Asteroid),
                typeof(ResourceSourceState),
                typeof(ResourceSourceConfig),
                typeof(ResourceTypeId),
                typeof(RewindableTag),
                typeof(LastRecordedTick),
                typeof(HistoryTier),
                typeof(Space4XAsteroidVolumeConfig),
                typeof(Space4XAsteroidCenter),
                typeof(Space4XSimServerTag));

            entityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
            entityManager.SetComponentData(entity, new Asteroid
            {
                AsteroidId = new FixedString64Bytes(asteroidId),
                ResourceAmount = unitsRemaining,
                MaxResourceAmount = unitsRemaining,
                ResourceType = type,
                MiningRate = 12f
            });

            entityManager.SetComponentData(entity, new ResourceSourceState
            {
                UnitsRemaining = unitsRemaining,
                LastHarvestTick = 0
            });

            entityManager.SetComponentData(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = 12f,
                MaxSimultaneousWorkers = 4,
                RespawnSeconds = 0f,
                Flags = 0
            });

            entityManager.SetComponentData(entity, new ResourceTypeId
            {
                Value = new FixedString64Bytes(resourceId)
            });

            entityManager.SetComponentData(entity, new LastRecordedTick { Tick = 0 });
            entityManager.SetComponentData(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.LowVisibility,
                OverrideStrideSeconds = 0f
            });

            var volume = Space4XAsteroidVolumeConfig.Default;
            volume.Radius = math.max(0.1f, volume.Radius);
            entityManager.SetComponentData(entity, volume);
            entityManager.SetComponentData(entity, new Space4XAsteroidCenter { Position = position });

            if (!entityManager.HasBuffer<ResourceHistorySample>(entity))
            {
                entityManager.AddBuffer<ResourceHistorySample>(entity);
            }
            if (!entityManager.HasBuffer<Space4XMiningLatchReservation>(entity))
            {
                entityManager.AddBuffer<Space4XMiningLatchReservation>(entity);
            }
        }

        private static void SpawnSystemContent(EntityManager entityManager, in GalaxySystemSeed seed, ref Unity.Mathematics.Random rng, NativeArray<Entity> factionEntities)
        {
            int orbitalCount = math.clamp(1 + seed.RingIndex, 1, 4);
            var ownerFactionId = seed.HomeFactionId;
            if (ownerFactionId == 0 && factionEntities.Length > 1)
            {
                ownerFactionId = (ushort)rng.NextInt(1, factionEntities.Length);
            }

            for (int i = 0; i < orbitalCount; i++)
            {
                var kind = RollOrbitalKind(ref rng, seed.RingIndex, i);
                var angle = rng.NextFloat(0f, math.PI * 2f);
                var distance = rng.NextFloat(150f, 320f) + seed.RingIndex * 60f + i * 30f;
                var position = seed.Position + new float3(math.cos(angle), 0f, math.sin(angle)) * distance;

                var orbital = entityManager.CreateEntity(
                    typeof(LocalTransform),
                    typeof(SpatialIndexedTag),
                    typeof(OrbitalObjectTag),
                    typeof(OrbitalObjectState),
                    typeof(Space4XSimServerTag));

                entityManager.SetComponentData(orbital, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 4f));

                var hidden = rng.NextFloat() < (seed.RingIndex >= 2 ? 0.5f : 0.25f);
                var state = new OrbitalObjectState
                {
                    Kind = kind,
                    Hidden = hidden && kind != OrbitalKind.Station,
                    CanDock = kind == OrbitalKind.Station,
                    OffersMission = kind == OrbitalKind.Station || kind == OrbitalKind.Derelict || kind == OrbitalKind.StrangeSatellite
                };
                entityManager.SetComponentData(orbital, state);

                switch (kind)
                {
                    case OrbitalKind.Station:
                        SetupStation(entityManager, orbital, ownerFactionId, ref rng, factionEntities);
                        break;
                    case OrbitalKind.Derelict:
                        SetupDerelict(entityManager, orbital, ownerFactionId, ref rng);
                        break;
                }
            }
        }

        private static OrbitalKind RollOrbitalKind(ref Unity.Mathematics.Random rng, byte ring, int index)
        {
            if (ring == 0 && index == 0 && rng.NextFloat() < 0.8f)
            {
                return OrbitalKind.Station;
            }

            float roll = rng.NextFloat();
            if (roll < 0.3f) return OrbitalKind.Asteroid;
            if (roll < 0.45f) return OrbitalKind.Comet;
            if (roll < 0.65f) return OrbitalKind.Derelict;
            if (roll < 0.85f) return OrbitalKind.StrangeSatellite;
            return OrbitalKind.Station;
        }

        private static void SetupDerelict(EntityManager entityManager, Entity entity, ushort ownerFactionId, ref Unity.Mathematics.Random rng)
        {
            if (!entityManager.HasComponent<DerelictTag>(entity))
            {
                entityManager.AddComponent<DerelictTag>(entity);
            }

            DerelictState state;
            if (rng.NextFloat() < 0.5f)
            {
                state = DerelictState.FromCombat(0u, ownerFactionId, (ushort)rng.NextInt(1, 4));
            }
            else
            {
                state = DerelictState.Ancient(0u);
                state.Condition = rng.NextFloat() < 0.3f ? DerelictCondition.Pristine : DerelictCondition.Ruined;
            }

            entityManager.AddComponentData(entity, state);
            entityManager.AddComponentData(entity, SalvageYield.FromCondition(state.Condition, math.max((ushort)1, state.OriginalClass)));
        }

        private static void SetupStation(EntityManager entityManager, Entity entity, ushort ownerFactionId, ref Unity.Mathematics.Random rng, NativeArray<Entity> factionEntities)
        {
            var stationId = rng.NextFloat() < 0.65f ? "outpost" : "starbase";
            if (!entityManager.HasComponent<StationId>(entity))
            {
                entityManager.AddComponentData(entity, new StationId
                {
                    Id = new FixedString64Bytes(stationId)
                });
            }

            if (!entityManager.HasComponent<DockingCapacity>(entity))
            {
                var capacity = stationId == "starbase"
                    ? new DockingCapacity
                    {
                        MaxSmallCraft = 18,
                        MaxMediumCraft = 8,
                        MaxLargeCraft = 4,
                        MaxExternalMooring = 2,
                        MaxUtility = 6
                    }
                    : new DockingCapacity
                    {
                        MaxSmallCraft = 8,
                        MaxMediumCraft = 3,
                        MaxLargeCraft = 1,
                        MaxExternalMooring = 1,
                        MaxUtility = 3
                    };

                entityManager.AddComponentData(entity, capacity);
            }

            if (!entityManager.HasBuffer<DockedEntity>(entity))
            {
                entityManager.AddBuffer<DockedEntity>(entity);
            }

            if (!entityManager.HasBuffer<AffiliationTag>(entity))
            {
                var affiliations = entityManager.AddBuffer<AffiliationTag>(entity);
                if (ownerFactionId != 0 && ownerFactionId < factionEntities.Length)
                {
                    var factionEntity = factionEntities[ownerFactionId];
                    if (factionEntity != Entity.Null)
                    {
                        affiliations.Add(new AffiliationTag
                        {
                            Type = AffiliationType.Faction,
                            Target = factionEntity,
                            Loyalty = (half)1f
                        });
                    }
                }
            }

            var marketSize = stationId == "starbase" ? MarketSize.Major : MarketSize.Small;
            InitializeMarket(entityManager, entity, MarketLocationType.Station, marketSize, ownerFactionId, ref rng);
        }

        private static void InitializeMarket(EntityManager entityManager, Entity entity, MarketLocationType locationType, MarketSize size, ushort ownerFactionId, ref Unity.Mathematics.Random rng)
        {
            if (entityManager.HasComponent<Space4XMarket>(entity))
            {
                return;
            }

            entityManager.AddComponentData(entity, new Space4XMarket
            {
                LocationType = locationType,
                Size = size,
                TaxRate = (half)math.clamp(rng.NextFloat(0.02f, 0.08f), 0f, 0.2f),
                BlackMarketAccess = (half)math.clamp(rng.NextFloat(0.05f, 0.35f), 0f, 1f),
                MarketHealth = (half)math.clamp(rng.NextFloat(0.7f, 0.95f), 0f, 1f),
                IsEmbargoed = 0,
                OwnerFactionId = ownerFactionId,
                LastUpdateTick = 0
            });

            var prices = entityManager.AddBuffer<MarketPriceEntry>(entity);
            _ = entityManager.AddBuffer<MarketEvent>(entity);
            var offers = entityManager.AddBuffer<TradeOffer>(entity);

            var sizeScale = size switch
            {
                MarketSize.Small => 0.8f,
                MarketSize.Medium => 1.0f,
                MarketSize.Large => 1.2f,
                MarketSize.Major => 1.4f,
                MarketSize.Capital => 1.7f,
                _ => 1.0f
            };

            for (byte i = 0; i <= (byte)MarketResourceType.Tech; i++)
            {
                var resourceType = (MarketResourceType)i;
                var basePrice = GetMarketBasePrice(resourceType) * sizeScale * rng.NextFloat(0.9f, 1.1f);
                var supply = rng.NextFloat(40f, 160f) * sizeScale;
                var demand = rng.NextFloat(40f, 160f) * sizeScale;
                var volatility = (half)rng.NextFloat(0.1f, 0.35f);

                prices.Add(new MarketPriceEntry
                {
                    ResourceType = resourceType,
                    BuyPrice = basePrice * 1.05f,
                    SellPrice = basePrice * 0.95f,
                    Supply = supply,
                    Demand = demand,
                    Volatility = volatility,
                    BasePrice = basePrice
                });

                if (rng.NextFloat() < 0.2f)
                {
                    offers.Add(new TradeOffer
                    {
                        Type = rng.NextBool() ? TradeOfferType.Buy : TradeOfferType.Sell,
                        ResourceType = resourceType,
                        Quantity = rng.NextFloat(20f, 120f),
                        PricePerUnit = basePrice * rng.NextFloat(0.9f, 1.1f),
                        CurrencyId = default,
                        OfferingEntity = entity,
                        OfferingFactionId = ownerFactionId,
                        ExpirationTick = 0,
                        IsFulfilled = 0
                    });
                }
            }
        }

        private static float GetMarketBasePrice(MarketResourceType resourceType)
        {
            return resourceType switch
            {
                MarketResourceType.Ore => 6f,
                MarketResourceType.RefinedMetal => 12f,
                MarketResourceType.RareEarth => 24f,
                MarketResourceType.Energy => 9f,
                MarketResourceType.Food => 7f,
                MarketResourceType.Water => 5f,
                MarketResourceType.Consumer => 16f,
                MarketResourceType.Industrial => 20f,
                MarketResourceType.Military => 30f,
                MarketResourceType.Luxury => 40f,
                MarketResourceType.Medical => 22f,
                MarketResourceType.Tech => 34f,
                _ => 10f
            };
        }

        private static void CreateFactionLeadership(EntityManager entityManager, Entity factionEntity, in Space4XFaction faction, ref Unity.Mathematics.Random rng)
        {
            if (entityManager.HasComponent<AuthorityBody>(factionEntity))
            {
                return;
            }

            var seats = entityManager.AddBuffer<AuthoritySeatRef>(factionEntity);
            var leaderSeat = entityManager.CreateEntity(typeof(AuthoritySeat), typeof(AuthoritySeatOccupant), typeof(Space4XSimServerTag));
            var roleId = new FixedString64Bytes("faction.leader");

            entityManager.SetComponentData(leaderSeat, AuthoritySeatDefaults.CreateExecutive(factionEntity, roleId, AgencyDomain.Governance));
            entityManager.SetComponentData(leaderSeat, AuthoritySeatDefaults.Vacant(0u));

            var leader = entityManager.CreateEntity(typeof(SimIndividualTag), typeof(Space4XSimServerTag));
            entityManager.AddComponentData(leader, new IndividualId { Value = (int)(faction.FactionId * 1000 + 1) });
            entityManager.AddComponentData(leader, new IndividualName { Name = new FixedString64Bytes($"Leader-{faction.FactionId:00}") });
            entityManager.AddComponentData(leader, new IndividualStats
            {
                Command = (half)math.clamp(rng.NextFloat(55f, 80f), 0f, 100f),
                Tactics = (half)math.clamp(rng.NextFloat(50f, 75f), 0f, 100f),
                Logistics = (half)math.clamp(rng.NextFloat(50f, 75f), 0f, 100f),
                Diplomacy = (half)math.clamp(rng.NextFloat(45f, 75f), 0f, 100f),
                Engineering = (half)math.clamp(rng.NextFloat(45f, 70f), 0f, 100f),
                Resolve = (half)math.clamp(rng.NextFloat(55f, 85f), 0f, 100f)
            });
            entityManager.AddComponentData(leader, new PhysiqueFinesseWill
            {
                Physique = (half)math.clamp(rng.NextFloat(45f, 75f), 0f, 100f),
                Finesse = (half)math.clamp(rng.NextFloat(45f, 75f), 0f, 100f),
                Will = (half)math.clamp(rng.NextFloat(45f, 80f), 0f, 100f),
                PhysiqueInclination = (byte)rng.NextInt(4, 8),
                FinesseInclination = (byte)rng.NextInt(4, 8),
                WillInclination = (byte)rng.NextInt(4, 8),
                GeneralXP = 0f
            });
            entityManager.AddComponentData(leader, AlignmentTriplet.FromFloats(0f, 0f, 0f));
            entityManager.AddComponentData(leader, PersonalityAxes.FromValues(0f, 0f, 0f, 0f, 0f));

            var directive = entityManager.GetComponentData<Space4XFactionDirective>(factionEntity);
            var disposition = Space4XSimServerProfileUtility.BuildLeaderDisposition(
                directive.Security,
                directive.Economy,
                directive.Research,
                directive.Expansion,
                directive.Diplomacy,
                math.saturate((float)faction.Aggression),
                math.saturate((float)faction.RiskTolerance),
                directive.Food);
            entityManager.AddComponentData(leader, disposition);

            var affiliations = entityManager.AddBuffer<AffiliationTag>(leader);
            affiliations.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = factionEntity,
                Loyalty = (half)1f
            });

            entityManager.SetComponentData(leaderSeat, new AuthoritySeatOccupant
            {
                OccupantEntity = leader,
                AssignedTick = 0,
                LastChangedTick = 0,
                IsActing = 0
            });

            entityManager.AddComponentData(factionEntity, new AuthorityBody
            {
                Mode = AuthorityBodyMode.SingleExecutive,
                ExecutiveSeat = leaderSeat,
                CreatedTick = 0
            });

            seats.Add(new AuthoritySeatRef { SeatEntity = leaderSeat });
        }

        private static FactionOutlook RandomOutlook(ref Unity.Mathematics.Random rng)
        {
            var outlook = FactionOutlook.None;
            if (rng.NextBool()) outlook |= FactionOutlook.Expansionist;
            if (rng.NextBool()) outlook |= FactionOutlook.Militarist;
            if (rng.NextBool()) outlook |= FactionOutlook.Materialist;
            if (rng.NextBool()) outlook |= FactionOutlook.Spiritualist;
            if (rng.NextBool()) outlook |= FactionOutlook.Xenophile;
            if (rng.NextBool()) outlook |= FactionOutlook.Honorable;
            return outlook;
        }
    }
}

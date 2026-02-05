using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
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

            var rng = new Unity.Mathematics.Random(math.max(config.Seed, 1u));
            var factionEntities = new NativeArray<Entity>(config.FactionCount, Allocator.Temp);
            var factionPositions = new NativeArray<float3>(config.FactionCount, Allocator.Temp);
            var entityManager = state.EntityManager;

            for (int i = 0; i < config.FactionCount; i++)
            {
                var factionId = (ushort)(i + 1);
                var outlook = RandomOutlook(ref rng);
                var faction = Space4XFaction.Empire(factionId, outlook);
                faction.Aggression = (half)math.clamp(rng.NextFloat(0.2f, 0.7f), 0f, 1f);
                faction.RiskTolerance = (half)math.clamp(rng.NextFloat(0.3f, 0.7f), 0f, 1f);
                faction.ExpansionDrive = (half)math.clamp(rng.NextFloat(0.3f, 0.8f), 0f, 1f);
                faction.TradeFocus = (half)math.clamp(rng.NextFloat(0.2f, 0.8f), 0f, 1f);
                faction.ResearchFocus = (half)math.clamp(rng.NextFloat(0.2f, 0.8f), 0f, 1f);
                faction.MilitaryFocus = (half)math.clamp(rng.NextFloat(0.2f, 0.8f), 0f, 1f);

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
                    Credits = rng.NextFloat(600f, 1400f),
                    Materials = rng.NextFloat(400f, 900f),
                    Energy = rng.NextFloat(250f, 650f),
                    Influence = rng.NextFloat(50f, 150f),
                    Research = rng.NextFloat(10f, 40f),
                    IncomeRate = rng.NextFloat(2f, 6f),
                    ExpenseRate = rng.NextFloat(1f, 4f)
                });

                entityManager.SetComponentData(factionEntity, new Space4XTerritoryControl
                {
                    ControlledSystems = 1,
                    ColonyCount = 1,
                    OutpostCount = 0,
                    ContestedSectors = 0,
                    FleetStrength = rng.NextFloat(50f, 120f),
                    EconomicOutput = rng.NextFloat(20f, 60f),
                    Population = (uint)rng.NextInt(200_000, 700_000),
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
                    LastUpdatedTick = 0,
                    DirectiveId = new FixedString64Bytes("default")
                });

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

                factionEntities[i] = factionEntity;

                var angle = math.PI * 2f * (i / math.max(1f, config.FactionCount));
                var basePos = new float3(math.cos(angle), 0f, math.sin(angle)) * config.StartRadius;
                factionPositions[i] = basePos;
            }

            ushort systemId = 1;
            for (int i = 0; i < config.FactionCount; i++)
            {
                var homePos = factionPositions[i];
                var factionEntity = factionEntities[i];
                var factionId = entityManager.GetComponentData<Space4XFaction>(factionEntity).FactionId;

                CreateStarSystem(entityManager, systemId++, homePos, 0, factionId);
                SpawnColony(entityManager, factionEntity, homePos + new float3(80f, 0f, 20f), $"colony-{factionId:00}");
                SpawnResourcesAroundSystem(entityManager, ref rng, homePos, 0, config);

                for (int r = 1; r < config.SystemsPerFaction; r++)
                {
                    var offsetAngle = rng.NextFloat(-0.35f, 0.35f);
                    var radius = config.StartRadius + r * config.SystemSpacing;
                    var dir = math.normalize(homePos);
                    var baseAngle = math.atan2(dir.z, dir.x);
                    var angle = baseAngle + offsetAngle;
                    var pos = new float3(math.cos(angle), 0f, math.sin(angle)) * radius;

                    _ = CreateStarSystem(entityManager, systemId++, pos, (byte)r, 0);
                    SpawnResourcesAroundSystem(entityManager, ref rng, pos, r, config);
                }
            }

            factionEntities.Dispose();
            factionPositions.Dispose();

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

        private static void SpawnColony(EntityManager entityManager, Entity factionEntity, float3 position, string colonyId)
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
        }

        private static void SpawnResourcesAroundSystem(EntityManager entityManager, ref Unity.Mathematics.Random rng, float3 center, int ring, Space4XSimServerConfig config)
        {
            var distanceFactor = config.StartRadius > 0f ? (math.length(center) / config.StartRadius) : 1f;
            var richness = config.ResourceBaseUnits * (1f + distanceFactor * config.ResourceRichnessGradient);

            for (int i = 0; i < config.ResourcesPerSystem; i++)
            {
                var offset = rng.NextFloat3Direction() * rng.NextFloat(120f, 420f);
                var position = center + offset;
                var units = richness * rng.NextFloat(0.6f, 1.4f);
                var resourceType = (ResourceType)rng.NextInt(0, 5);
                var resourceId = resourceType.ToString();
                var asteroidId = $"ast-{ring}-{i}-{rng.NextInt(0, 9999):0000}";
                CreateResourceDeposit(entityManager, position, resourceType, resourceId, asteroidId, units);
            }

            if (ring > 0 && rng.NextFloat() < math.min(0.2f + ring * 0.15f, 0.8f))
            {
                var severity = ring >= 3 ? Space4XAnomalySeverity.Severe : Space4XAnomalySeverity.Moderate;
                var anomalyOffset = rng.NextFloat3Direction() * rng.NextFloat(600f, 1200f);
                CreateAnomaly(entityManager, center + anomalyOffset, severity, ring);
            }
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

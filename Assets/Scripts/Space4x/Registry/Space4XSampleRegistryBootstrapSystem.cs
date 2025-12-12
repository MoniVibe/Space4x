using PureDOTS.Runtime.Aggregates;
using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.History;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using AggregateBandMember = PureDOTS.Runtime.Aggregates.BandMember;

namespace Space4X.Registry
{
    /// <summary>
    /// Seeds a minimal set of resource sources, storehouses, and bands so registry health systems
    /// have real data to process when the smoketest scene forgets to author them.
    /// Automatically no-ops once the world contains any real instances.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XSampleRegistryBootstrapSystem : ISystem
    {
        private static readonly FixedString64Bytes ResourceIdMinerals = "minerals";
        private static readonly FixedString64Bytes ResourceIdRareMetals = "rareMetals";
        private static readonly FixedString64Bytes SampleStorehouseLabel = "Smoketest Storehouse";
        private static readonly FixedString64Bytes SampleBandName = "Guardian Surveyors";

        private EntityQuery _resourceSourcesQuery;
        private EntityQuery _storehousesQuery;
        private EntityQuery _bandsQuery;
        private bool _completed;

        public void OnCreate(ref SystemState state)
        {
            _resourceSourcesQuery = state.GetEntityQuery(ComponentType.ReadOnly<ResourceSourceConfig>());
            _storehousesQuery = state.GetEntityQuery(ComponentType.ReadOnly<StorehouseConfig>());
            _bandsQuery = state.GetEntityQuery(ComponentType.ReadOnly<BandId>());

            state.RequireForUpdate<RegistryDirectory>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_completed)
            {
                state.Enabled = false;
                return;
            }

            var createResource = _resourceSourcesQuery.IsEmptyIgnoreFilter;
            var createStorehouse = _storehousesQuery.IsEmptyIgnoreFilter;
            var createBand = _bandsQuery.IsEmptyIgnoreFilter;

            if (!createResource && !createStorehouse && !createBand)
            {
                _completed = true;
                state.Enabled = false;
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            if (createResource)
            {
                CreateSampleResourceNode(ecb);
            }

            if (createStorehouse)
            {
                CreateSampleStorehouse(ecb);
            }

            if (createBand)
            {
                CreateSampleBand(ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            _completed = true;
            state.Enabled = false;
        }

        private static void CreateSampleResourceNode(EntityCommandBuffer ecb)
        {
            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, LocalTransform.FromPositionRotationScale(new float3(-24f, 0f, 12f), quaternion.identity, 1f));
            ecb.AddComponent<SpatialIndexedTag>(entity);
            ecb.AddComponent<RewindableTag>(entity);

            ecb.AddComponent(entity, new ResourceTypeId { Value = ResourceIdMinerals });
            ecb.AddComponent(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = 3.5f,
                MaxSimultaneousWorkers = 4,
                RespawnSeconds = 45f,
                Flags = 0
            });
            ecb.AddComponent(entity, new ResourceSourceState
            {
                UnitsRemaining = 800f
            });

            ecb.AddComponent(entity, new LastRecordedTick { Tick = 0 });
            ecb.AddComponent(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.Default,
                OverrideStrideSeconds = 0f
            });
            ecb.AddBuffer<ResourceHistorySample>(entity);
        }

        private static void CreateSampleStorehouse(EntityCommandBuffer ecb)
        {
            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, LocalTransform.FromPositionRotationScale(new float3(18f, 0f, -6f), quaternion.identity, 1.25f));
            ecb.AddComponent<SpatialIndexedTag>(entity);
            ecb.AddComponent<RewindableTag>(entity);

            var capacityBuffer = ecb.AddBuffer<StorehouseCapacityElement>(entity);
            capacityBuffer.Add(new StorehouseCapacityElement
            {
                ResourceTypeId = ResourceIdMinerals,
                MaxCapacity = 600f
            });
            capacityBuffer.Add(new StorehouseCapacityElement
            {
                ResourceTypeId = ResourceIdRareMetals,
                MaxCapacity = 250f
            });

            ecb.AddComponent(entity, new StorehouseConfig
            {
                ShredRate = 1f,
                MaxShredQueueSize = 8,
                InputRate = 20f,
                OutputRate = 18f,
                Label = SampleStorehouseLabel
            });
            ecb.AddComponent(entity, new StorehouseInventory
            {
                TotalStored = 0f,
                TotalCapacity = 850f,
                ItemTypeCount = capacityBuffer.Length,
                IsShredding = 0,
                LastUpdateTick = 0
            });

            ecb.AddBuffer<StorehouseInventoryItem>(entity);
            ecb.AddComponent(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.Default,
                OverrideStrideSeconds = 0f
            });
            ecb.AddBuffer<StorehouseHistorySample>(entity);
        }

        private static void CreateSampleBand(EntityCommandBuffer ecb)
        {
            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, LocalTransform.FromPositionRotationScale(new float3(-8f, 0f, 32f), quaternion.identity, 1f));
            ecb.AddComponent<SpatialIndexedTag>(entity);
            ecb.AddComponent<RewindableTag>(entity);

            ecb.AddComponent(entity, new BandId
            {
                Value = 501,
                FactionId = 7,
                Leader = Entity.Null
            });

            ecb.AddComponent(entity, new BandStats
            {
                MemberCount = 12,
                AverageDiscipline = 0.6f,
                Morale = 0.7f,
                Cohesion = 0.64f,
                Fatigue = 0.18f,
                Flags = BandStatusFlags.Idle,
                LastUpdateTick = 0
            });

            ecb.AddComponent(entity, new Band
            {
                BandName = SampleBandName,
                Purpose = BandPurpose.Military_Defense,
                LeaderEntity = Entity.Null,
                FormationTick = 0,
                MemberCount = 12,
                AverageMorale = 0.7f,
                AverageEnergy = 0.62f,
                AverageStrength = 0.58f
            });

            ecb.AddBuffer<AggregateBandMember>(entity);
        }
    }
}

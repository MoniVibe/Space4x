#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Resources;
using PureDOTS.Systems.Resources;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Tests.Logistics
{
    public class ResourceRequestSystemTests
    {
        [DisableAutoCreation]
        private sealed partial class RequestWrapperSystem : SystemBase
        {
            private ResourceRequestSystem _system;

            protected override void OnCreate()
            {
                base.OnCreate();
                _system = new ResourceRequestSystem();
                _system.OnCreate(ref CheckedStateRef);
            }

            protected override void OnUpdate()
            {
                _system.OnUpdate(ref CheckedStateRef);
            }
        }

        private World _world;
        private EntityManager _entityManager;
        private RequestWrapperSystem _system;
        private BlobAssetReference<ResourceTypeIndexBlob> _resourceCatalog;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ResourceRequestSystemTests");
            _entityManager = _world.EntityManager;
            World.DefaultGameObjectInjectionWorld = _world;
            _system = _world.GetOrCreateSystemManaged<RequestWrapperSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated)
            {
                if (_resourceCatalog.IsCreated)
                {
                    _resourceCatalog.Dispose();
                }

                _world.Dispose();
                if (World.DefaultGameObjectInjectionWorld == _world)
                {
                    World.DefaultGameObjectInjectionWorld = null;
                }
            }
        }

        [Test]
        public void RequestCreatesOrderWhenSupplyExists()
        {
            CreateSingletons(RewindMode.Record, tick: 10);
            CreateResourceTypeIndex("ore");

            var storehouse = _entityManager.CreateEntity();
            CreateStorehouseRegistry(storehouse, resourceTypeIndex: 0, stored: 25f, reserved: 0f);

            var requester = CreateRequesterWithRequest("ore", amount: 5f, requestId: 7, createdTick: 10);

            _system.Update();

            var requests = _entityManager.GetBuffer<NeedRequest>(requester);
            Assert.AreEqual(1, requests.Length);
            Assert.AreNotEqual(Entity.Null, requests[0].OrderEntity);

            var order = _entityManager.GetComponentData<LogisticsOrder>(requests[0].OrderEntity);
            Assert.AreEqual(LogisticsOrderStatus.Planning, order.Status);
            Assert.AreEqual(storehouse, order.SourceNode);
            Assert.AreEqual(requester, order.DestinationNode);
            Assert.AreEqual(7, order.OrderId);
        }

        [Test]
        public void RequestIsRemovedWhenOrderDelivered()
        {
            CreateSingletons(RewindMode.Record, tick: 10);
            CreateResourceTypeIndex("ore");

            var storehouse = _entityManager.CreateEntity();
            CreateStorehouseRegistry(storehouse, resourceTypeIndex: 0, stored: 25f, reserved: 0f);

            var requester = CreateRequesterWithRequest("ore", amount: 5f, requestId: 1, createdTick: 10);

            _system.Update();

            var requests = _entityManager.GetBuffer<NeedRequest>(requester);
            var orderEntity = requests[0].OrderEntity;
            var order = _entityManager.GetComponentData<LogisticsOrder>(orderEntity);
            order.Status = LogisticsOrderStatus.Delivered;
            _entityManager.SetComponentData(orderEntity, order);

            _system.Update();

            requests = _entityManager.GetBuffer<NeedRequest>(requester);
            Assert.AreEqual(0, requests.Length);
        }

        [Test]
        public void RequestFailureUpdatesWhenOrderFails()
        {
            CreateSingletons(RewindMode.Record, tick: 10);
            CreateResourceTypeIndex("ore");

            var storehouse = _entityManager.CreateEntity();
            CreateStorehouseRegistry(storehouse, resourceTypeIndex: 0, stored: 25f, reserved: 0f);

            var requester = CreateRequesterWithRequest("ore", amount: 5f, requestId: 2, createdTick: 10);

            _system.Update();

            var requests = _entityManager.GetBuffer<NeedRequest>(requester);
            var orderEntity = requests[0].OrderEntity;
            var order = _entityManager.GetComponentData<LogisticsOrder>(orderEntity);
            order.Status = LogisticsOrderStatus.Failed;
            order.FailureReason = ShipmentFailureReason.NoInventory;
            _entityManager.SetComponentData(orderEntity, order);

            _system.Update();

            requests = _entityManager.GetBuffer<NeedRequest>(requester);
            Assert.AreEqual(1, requests.Length);
            Assert.AreEqual(RequestFailureReason.NoSupply, requests[0].FailureReason);
            Assert.AreEqual(Entity.Null, requests[0].OrderEntity);
        }

        [Test]
        public void RequestSystemDoesNotMutateDuringPlayback()
        {
            CreateSingletons(RewindMode.Playback, tick: 10);
            CreateResourceTypeIndex("ore");

            var storehouse = _entityManager.CreateEntity();
            CreateStorehouseRegistry(storehouse, resourceTypeIndex: 0, stored: 25f, reserved: 0f);

            var requester = CreateRequesterWithRequest("ore", amount: 5f, requestId: 3, createdTick: 10);

            _system.Update();

            var requests = _entityManager.GetBuffer<NeedRequest>(requester);
            Assert.AreEqual(1, requests.Length);
            Assert.AreEqual(Entity.Null, requests[0].OrderEntity);
            Assert.AreEqual(RequestFailureReason.None, requests[0].FailureReason);
        }

        private void CreateSingletons(RewindMode mode, uint tick)
        {
            var scenarioEntity = _entityManager.CreateEntity(typeof(ScenarioState));
            _entityManager.SetComponentData(scenarioEntity, new ScenarioState
            {
                IsInitialized = true,
                EnableEconomy = true
            });

            var rewindEntity = _entityManager.CreateEntity(typeof(RewindState));
            _entityManager.SetComponentData(rewindEntity, new RewindState
            {
                Mode = mode
            });

            var tickEntity = _entityManager.CreateEntity(typeof(TickTimeState));
            _entityManager.SetComponentData(tickEntity, new TickTimeState
            {
                Tick = tick,
                FixedDeltaTime = 0.016f,
                CurrentSpeedMultiplier = 1f,
                IsPaused = false,
                IsPlaying = true
            });
        }

        private Entity CreateRequesterWithRequest(string resourceId, float amount, uint requestId, uint createdTick)
        {
            var requester = _entityManager.CreateEntity();
            var requests = _entityManager.AddBuffer<NeedRequest>(requester);
            requests.Add(new NeedRequest
            {
                ResourceTypeId = new FixedString32Bytes(resourceId),
                Amount = amount,
                RequesterEntity = requester,
                Priority = 128f,
                CreatedTick = createdTick,
                TargetEntity = Entity.Null,
                RequestId = requestId,
                OrderEntity = Entity.Null,
                FailureReason = RequestFailureReason.None
            });

            return requester;
        }

        private void CreateStorehouseRegistry(Entity storehouse, ushort resourceTypeIndex, float stored, float reserved)
        {
            var registryEntity = _entityManager.CreateEntity(typeof(StorehouseRegistry));
            _entityManager.SetComponentData(registryEntity, new StorehouseRegistry
            {
                TotalStorehouses = 1,
                TotalCapacity = stored,
                TotalStored = stored,
                LastUpdateTick = 0,
                LastSpatialVersion = 0,
                SpatialResolvedCount = 0,
                SpatialFallbackCount = 0,
                SpatialUnmappedCount = 0
            });

            var entries = _entityManager.AddBuffer<StorehouseRegistryEntry>(registryEntity);
            var summaries = new FixedList64Bytes<StorehouseRegistryCapacitySummary>();
            summaries.Add(new StorehouseRegistryCapacitySummary
            {
                ResourceTypeIndex = resourceTypeIndex,
                Capacity = stored,
                Stored = stored,
                Reserved = reserved,
                TierId = (byte)ResourceQualityTier.Unknown,
                AverageQuality = 0
            });

            entries.Add(new StorehouseRegistryEntry
            {
                StorehouseEntity = storehouse,
                Position = default,
                TotalCapacity = stored,
                TotalStored = stored,
                TypeSummaries = summaries,
                LastMutationTick = 0,
                CellId = -1,
                SpatialVersion = 0,
                DominantTier = ResourceQualityTier.Unknown,
                AverageQuality = 0
            });
        }

        private void CreateResourceTypeIndex(params string[] resourceIds)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ResourceTypeIndexBlob>();
            var ids = builder.Allocate(ref root.Ids, resourceIds.Length);
            var displayNames = builder.Allocate(ref root.DisplayNames, resourceIds.Length);
            var colors = builder.Allocate(ref root.Colors, resourceIds.Length);

            for (int i = 0; i < resourceIds.Length; i++)
            {
                var resourceId = new FixedString64Bytes(resourceIds[i]);
                ids[i] = resourceId;
                builder.AllocateString(ref displayNames[i], resourceIds[i]);
                colors[i] = new Color32(0, 0, 0, 0);
            }

            _resourceCatalog = builder.CreateBlobAssetReference<ResourceTypeIndexBlob>(Allocator.Persistent);
            var entity = _entityManager.CreateEntity(typeof(ResourceTypeIndex));
            _entityManager.SetComponentData(entity, new ResourceTypeIndex { Catalog = _resourceCatalog });
            builder.Dispose();
        }
    }
}
#endif

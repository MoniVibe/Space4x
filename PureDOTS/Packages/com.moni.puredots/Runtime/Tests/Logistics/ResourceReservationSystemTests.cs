#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Logistics.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Tests.Logistics
{
    public class ResourceReservationSystemTests
    {
        [DisableAutoCreation]
        private sealed partial class ReservationWrapperSystem : SystemBase
        {
            private ResourceReservationSystem _system;

            protected override void OnCreate()
            {
                base.OnCreate();
                _system = new ResourceReservationSystem();
                _system.OnCreate(ref CheckedStateRef);
            }

            protected override void OnUpdate()
            {
                _system.OnUpdate(ref CheckedStateRef);
            }
        }

        private World _world;
        private EntityManager _entityManager;
        private ReservationWrapperSystem _system;
        private Entity _tickEntity;
        private Entity _rewindEntity;
        private BlobAssetReference<ResourceTypeIndexBlob> _resourceCatalog;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ResourceReservationSystemTests");
            _entityManager = _world.EntityManager;
            World.DefaultGameObjectInjectionWorld = _world;
            _system = _world.GetOrCreateSystemManaged<ReservationWrapperSystem>();
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
        public void ReservationTtlUsesFixedDeltaTime()
        {
            CreateSingletons(fixedDeltaTime: 0.2f, tick: 100, mode: RewindMode.Record);
            CreateReservationPolicy(defaultTTLSeconds: 2.5f, allowPartialReservations: true);

            var storehouse = CreateStorehouse("ore", amount: 10f, reserved: 0f);
            CreateOrder(storehouse, "ore", requestedAmount: 5f);

            _system.Update();

            var reservation = GetSingleReservation();
            var expectedTicks = (uint)math.ceil(2.5f / 0.2f);

            Assert.AreEqual(expectedTicks, reservation.ExpiryTick - reservation.CreatedTick);
            Assert.AreEqual(ReservationCancelReason.None, reservation.CancelReason);
        }

        [Test]
        public void ReservationExpiryReleasesReservedInventory()
        {
            CreateSingletons(fixedDeltaTime: 0.1f, tick: 10, mode: RewindMode.Record);
            CreateReservationPolicy(defaultTTLSeconds: 0.5f, allowPartialReservations: true);

            var storehouse = CreateStorehouse("ore", amount: 10f, reserved: 2f);
            CreateOrder(storehouse, "ore", requestedAmount: 5f);

            _system.Update();

            var items = _entityManager.GetBuffer<StorehouseInventoryItem>(storehouse);
            Assert.AreEqual(7f, items[0].Reserved, 0.001f);

            var reservation = GetSingleReservation();
            SetTick(reservation.ExpiryTick + 1);
            _system.Update();

            items = _entityManager.GetBuffer<StorehouseInventoryItem>(storehouse);
            Assert.AreEqual(2f, items[0].Reserved, 0.001f);

            var reservationQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<InventoryReservation>());
            Assert.AreEqual(0, reservationQuery.CalculateEntityCount());
        }

        [Test]
        public void ReservationSystemDoesNotMutateDuringPlayback()
        {
            CreateSingletons(fixedDeltaTime: 0.1f, tick: 10, mode: RewindMode.Playback);
            CreateReservationPolicy(defaultTTLSeconds: 1.0f, allowPartialReservations: true);

            var storehouse = CreateStorehouse("ore", amount: 10f, reserved: 1f);
            CreateOrder(storehouse, "ore", requestedAmount: 5f);

            _system.Update();

            var items = _entityManager.GetBuffer<StorehouseInventoryItem>(storehouse);
            Assert.AreEqual(1f, items[0].Reserved, 0.001f);

            var reservationQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<InventoryReservation>());
            Assert.AreEqual(0, reservationQuery.CalculateEntityCount());
        }

        private void CreateSingletons(float fixedDeltaTime, uint tick, RewindMode mode)
        {
            var scenarioEntity = _entityManager.CreateEntity(typeof(ScenarioState));
            _entityManager.SetComponentData(scenarioEntity, new ScenarioState
            {
                IsInitialized = true,
                EnableEconomy = true
            });

            _rewindEntity = _entityManager.CreateEntity(typeof(RewindState));
            _entityManager.SetComponentData(_rewindEntity, new RewindState
            {
                Mode = mode
            });

            _tickEntity = _entityManager.CreateEntity(typeof(TickTimeState));
            _entityManager.SetComponentData(_tickEntity, new TickTimeState
            {
                Tick = tick,
                FixedDeltaTime = fixedDeltaTime,
                CurrentSpeedMultiplier = 1f,
                IsPaused = false,
                IsPlaying = true
            });

            CreateResourceTypeIndex("ore");
        }

        private void CreateReservationPolicy(float defaultTTLSeconds, bool allowPartialReservations)
        {
            var policyEntity = _entityManager.CreateEntity(typeof(ReservationPolicy));
            _entityManager.SetComponentData(policyEntity, new ReservationPolicy
            {
                DefaultTTLSeconds = defaultTTLSeconds,
                AllowPartialReservations = (byte)(allowPartialReservations ? 1 : 0),
                AutoCommitOnDispatch = 0,
                CancelOnOrderCancel = 0
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

        private Entity CreateStorehouse(string resourceId, float amount, float reserved)
        {
            var entity = _entityManager.CreateEntity(typeof(StorehouseInventory));
            _entityManager.SetComponentData(entity, new StorehouseInventory
            {
                TotalStored = amount,
                TotalCapacity = 100f,
                ItemTypeCount = 1
            });

            var items = _entityManager.AddBuffer<StorehouseInventoryItem>(entity);
            items.Add(new StorehouseInventoryItem
            {
                ResourceTypeId = new FixedString64Bytes(resourceId),
                Amount = amount,
                Reserved = reserved,
                TierId = 0,
                AverageQuality = 0
            });

            return entity;
        }

        private Entity CreateOrder(Entity sourceNode, string resourceId, float requestedAmount)
        {
            var entity = _entityManager.CreateEntity(typeof(LogisticsOrder));
            _entityManager.SetComponentData(entity, new LogisticsOrder
            {
                OrderId = 1,
                Kind = LogisticsJobKind.Delivery,
                SourceNode = sourceNode,
                DestinationNode = Entity.Null,
                ResourceId = new FixedString64Bytes(resourceId),
                ResourceTypeIndex = 0,
                RequestedAmount = requestedAmount,
                ReservedAmount = 0f,
                Status = LogisticsOrderStatus.Planning,
                FailureReason = ShipmentFailureReason.None
            });
            return entity;
        }

        private InventoryReservation GetSingleReservation()
        {
            var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<InventoryReservation>());
            return query.GetSingleton<InventoryReservation>();
        }

        private void SetTick(uint tick)
        {
            var state = _entityManager.GetComponentData<TickTimeState>(_tickEntity);
            state.Tick = tick;
            _entityManager.SetComponentData(_tickEntity, state);
        }
    }
}
#endif

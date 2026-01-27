#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Logistics.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Tests.Logistics
{
    public class ResourceLogisticsDeliverySystemTests
    {
        [DisableAutoCreation]
        private sealed partial class DeliveryWrapperSystem : SystemBase
        {
            private ResourceLogisticsDeliverySystem _system;

            protected override void OnCreate()
            {
                base.OnCreate();
                _system = new ResourceLogisticsDeliverySystem();
                _system.OnCreate(ref CheckedStateRef);
            }

            protected override void OnDestroy()
            {
                _system.OnDestroy(ref CheckedStateRef);
                base.OnDestroy();
            }

            protected override void OnUpdate()
            {
                _system.OnUpdate(ref CheckedStateRef);
            }
        }

        private World _world;
        private EntityManager _entityManager;
        private DeliveryWrapperSystem _system;
        private BlobAssetReference<ResourceTypeIndexBlob> _resourceCatalog;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ResourceLogisticsDeliverySystemTests");
            _entityManager = _world.EntityManager;
            World.DefaultGameObjectInjectionWorld = _world;

            _system = _world.GetOrCreateSystemManaged<DeliveryWrapperSystem>();
            CreateSingletons();
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
        public void DeliverySystemDepositsIntoStorehouse()
        {
            var resourceId = new FixedString64Bytes("ore");
            var destination = CreateStorehouse(resourceId, perTypeCapacity: 100f);
            var shipmentEntity = CreateShipment(resourceId, allocatedAmount: 20f, arrivalTick: 100);
            var orderEntity = CreateOrder(destination, resourceId, requestedAmount: 20f, shipmentEntity);
            CreateInventoryReservation(orderEntity, resourceId, amount: 20f);

            _system.Update();

            var order = _entityManager.GetComponentData<LogisticsOrder>(orderEntity);
            Assert.AreEqual(LogisticsOrderStatus.Delivered, order.Status);

            var shipment = _entityManager.GetComponentData<Shipment>(shipmentEntity);
            Assert.AreEqual(ShipmentStatus.Delivered, shipment.Status);

            var storehouse = _entityManager.GetComponentData<StorehouseInventory>(destination);
            Assert.AreEqual(20f, storehouse.TotalStored, 0.001f);

            var items = _entityManager.GetBuffer<StorehouseInventoryItem>(destination);
            Assert.AreEqual(1, items.Length);
            Assert.AreEqual(20f, items[0].Amount, 0.001f);
        }

        [Test]
        public void DeliverySystemFailsWhenStorehouseHasNoCapacity()
        {
            var resourceId = new FixedString64Bytes("ore");
            var destination = CreateStorehouse(resourceId, perTypeCapacity: 0f);
            var shipmentEntity = CreateShipment(resourceId, allocatedAmount: 10f, arrivalTick: 100);
            var orderEntity = CreateOrder(destination, resourceId, requestedAmount: 10f, shipmentEntity);
            CreateInventoryReservation(orderEntity, resourceId, amount: 10f);

            _system.Update();

            var order = _entityManager.GetComponentData<LogisticsOrder>(orderEntity);
            Assert.AreEqual(LogisticsOrderStatus.Failed, order.Status);

            var shipment = _entityManager.GetComponentData<Shipment>(shipmentEntity);
            Assert.AreEqual(ShipmentStatus.Failed, shipment.Status);

            var storehouse = _entityManager.GetComponentData<StorehouseInventory>(destination);
            Assert.AreEqual(0f, storehouse.TotalStored, 0.001f);
        }

        private void CreateSingletons()
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
                Mode = RewindMode.Record
            });

            var tickEntity = _entityManager.CreateEntity(typeof(TickTimeState));
            _entityManager.SetComponentData(tickEntity, new TickTimeState
            {
                Tick = 100,
                FixedDeltaTime = 0.016f,
                CurrentSpeedMultiplier = 1f,
                IsPaused = false,
                IsPlaying = true
            });

            CreateResourceTypeIndex("ore");
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

        private Entity CreateStorehouse(FixedString64Bytes resourceId, float perTypeCapacity)
        {
            var entity = _entityManager.CreateEntity(typeof(StorehouseInventory), typeof(StorehouseJobReservation));
            _entityManager.SetComponentData(entity, new StorehouseInventory
            {
                TotalCapacity = perTypeCapacity,
                TotalStored = 0f,
                ItemTypeCount = 0
            });
            _entityManager.SetComponentData(entity, new StorehouseJobReservation
            {
                ReservedCapacity = 0f,
                LastMutationTick = 0
            });
            _entityManager.AddBuffer<StorehouseInventoryItem>(entity);
            var capacities = _entityManager.AddBuffer<StorehouseCapacityElement>(entity);
            capacities.Add(new StorehouseCapacityElement
            {
                ResourceTypeId = resourceId,
                MaxCapacity = perTypeCapacity
            });
            _entityManager.AddBuffer<StorehouseReservationItem>(entity);
            return entity;
        }

        private Entity CreateShipment(FixedString64Bytes resourceId, float allocatedAmount, uint arrivalTick)
        {
            var resourceIndex = GetResourceTypeIndex(resourceId);
            var entity = _entityManager.CreateEntity(typeof(Shipment));
            _entityManager.SetComponentData(entity, new Shipment
            {
                ShipmentId = 1,
                Status = ShipmentStatus.InTransit,
                RepresentationMode = ShipmentRepresentationMode.Abstract,
                ResourceTypeIndex = resourceIndex,
                EstimatedArrivalTick = arrivalTick
            });

            var cargo = _entityManager.AddBuffer<ShipmentCargoAllocation>(entity);
            cargo.Add(new ShipmentCargoAllocation
            {
                ResourceId = resourceId,
                ResourceTypeIndex = resourceIndex,
                AllocatedAmount = allocatedAmount,
                ContainerEntity = Entity.Null,
                ContainerHandle = default,
                BatchEntity = Entity.Null
            });

            return entity;
        }

        private Entity CreateOrder(Entity destinationNode, FixedString64Bytes resourceId, float requestedAmount, Entity shipmentEntity)
        {
            var resourceIndex = GetResourceTypeIndex(resourceId);
            var entity = _entityManager.CreateEntity(typeof(LogisticsOrder));
            _entityManager.SetComponentData(entity, new LogisticsOrder
            {
                OrderId = 10,
                Kind = LogisticsJobKind.Delivery,
                SourceNode = Entity.Null,
                DestinationNode = destinationNode,
                ResourceId = resourceId,
                ResourceTypeIndex = resourceIndex,
                RequestedAmount = requestedAmount,
                ReservedAmount = requestedAmount,
                Status = LogisticsOrderStatus.InTransit,
                ShipmentEntity = shipmentEntity,
                CreatedTick = 0,
                EarliestDepartTick = 0,
                LatestArrivalTick = 0
            });
            return entity;
        }

        private Entity CreateInventoryReservation(Entity orderEntity, FixedString64Bytes resourceId, float amount)
        {
            var entity = _entityManager.CreateEntity(typeof(InventoryReservation));
            _entityManager.SetComponentData(entity, new InventoryReservation
            {
                ReservationId = 1,
                OrderEntity = orderEntity,
                ResourceId = resourceId,
                ReservedAmount = amount,
                Status = ReservationStatus.Committed
            });
            return entity;
        }

        private ushort GetResourceTypeIndex(FixedString64Bytes resourceId)
        {
            var index = _resourceCatalog.Value.LookupIndex(resourceId);
            return index < 0 ? ushort.MaxValue : (ushort)index;
        }
    }
}
#endif


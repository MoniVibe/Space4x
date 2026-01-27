#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Tests.Logistics
{
    public class StorehouseMutationServiceTests
    {
        private World _world;
        private EntityManager _entityManager;
        private BlobAssetReference<ResourceTypeIndexBlob> _resourceCatalog;

        [SetUp]
        public void SetUp()
        {
            _world = new World("StorehouseMutationServiceTests");
            _entityManager = _world.EntityManager;
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
        public void TryDepositWithPerTypeCapacity_RespectsTypeCaps()
        {
            CreateResourceTypeIndex("ore", "stone");
            var ore = new FixedString64Bytes("ore");
            var stone = new FixedString64Bytes("stone");

            var storehouse = CreateStorehouseWithCapacities(
                new[] { (ore, 10f), (stone, 5f) },
                new[] { (ore, 10f) });

            var inventory = _entityManager.GetComponentData<StorehouseInventory>(storehouse);
            var items = _entityManager.GetBuffer<StorehouseInventoryItem>(storehouse);
            var capacities = _entityManager.GetBuffer<StorehouseCapacityElement>(storehouse);
            var reservations = _entityManager.GetBuffer<StorehouseReservationItem>(storehouse);

            var oreResult = StorehouseMutationService.TryDepositWithPerTypeCapacity(
                0,
                1f,
                _resourceCatalog,
                ref inventory,
                items,
                capacities,
                reservations,
                out float oreDeposited);

            Assert.IsFalse(oreResult);
            Assert.AreEqual(0f, oreDeposited, 0.001f);

            var stoneResult = StorehouseMutationService.TryDepositWithPerTypeCapacity(
                1,
                3f,
                _resourceCatalog,
                ref inventory,
                items,
                capacities,
                reservations,
                out float stoneDeposited);

            Assert.IsTrue(stoneResult);
            Assert.AreEqual(3f, stoneDeposited, 0.001f);

            _entityManager.SetComponentData(storehouse, inventory);

            Assert.AreEqual(10f, GetItemAmount(items, ore), 0.001f);
            Assert.AreEqual(3f, GetItemAmount(items, stone), 0.001f);
        }

        [Test]
        public void ReserveCommitDeposit_KeepsInventoryConsistent()
        {
            CreateResourceTypeIndex("ore");
            var ore = new FixedString64Bytes("ore");

            var source = CreateStorehouse(ore, amount: 20f, perTypeCapacity: 50f);
            var destination = CreateStorehouse(ore, amount: 0f, perTypeCapacity: 50f);

            var sourceItems = _entityManager.GetBuffer<StorehouseInventoryItem>(source);
            var sourceInventory = _entityManager.GetComponentData<StorehouseInventory>(source);

            Assert.IsTrue(StorehouseMutationService.TryReserveOut(
                0,
                10f,
                allowPartial: false,
                _resourceCatalog,
                sourceItems,
                out float reservedAmount));
            Assert.AreEqual(10f, reservedAmount, 0.001f);
            Assert.AreEqual(10f, sourceItems[0].Reserved, 0.001f);

            Assert.IsTrue(StorehouseMutationService.CommitWithdrawReservedOut(
                0,
                reservedAmount,
                _resourceCatalog,
                ref sourceInventory,
                sourceItems,
                out float withdrawn));
            Assert.AreEqual(10f, withdrawn, 0.001f);
            _entityManager.SetComponentData(source, sourceInventory);

            var destinationItems = _entityManager.GetBuffer<StorehouseInventoryItem>(destination);
            var destinationInventory = _entityManager.GetComponentData<StorehouseInventory>(destination);
            var destinationCapacities = _entityManager.GetBuffer<StorehouseCapacityElement>(destination);
            var destinationReservations = _entityManager.GetBuffer<StorehouseReservationItem>(destination);

            Assert.IsTrue(StorehouseMutationService.TryDepositWithPerTypeCapacity(
                0,
                withdrawn,
                _resourceCatalog,
                ref destinationInventory,
                destinationItems,
                destinationCapacities,
                destinationReservations,
                out float deposited));
            Assert.AreEqual(10f, deposited, 0.001f);
            _entityManager.SetComponentData(destination, destinationInventory);

            Assert.AreEqual(10f, sourceItems[0].Amount, 0.001f);
            Assert.AreEqual(0f, sourceItems[0].Reserved, 0.001f);
            Assert.AreEqual(10f, destinationItems[0].Amount, 0.001f);
        }

        [Test]
        public void MutationSequence_ReplaysDeterministically()
        {
            CreateResourceTypeIndex("ore");
            var ore = new FixedString64Bytes("ore");

            var source = CreateStorehouse(ore, amount: 12f, perTypeCapacity: 20f);
            var destination = CreateStorehouse(ore, amount: 0f, perTypeCapacity: 20f);

            var sourceInitialInventory = _entityManager.GetComponentData<StorehouseInventory>(source);
            var destinationInitialInventory = _entityManager.GetComponentData<StorehouseInventory>(destination);

            var sourceInitialItems = CopyItems(_entityManager.GetBuffer<StorehouseInventoryItem>(source));
            var destinationInitialItems = CopyItems(_entityManager.GetBuffer<StorehouseInventoryItem>(destination));

            RunMutationSequence(source, destination, out var sourceFinalA, out var destinationFinalA);

            RestoreInventory(source, sourceInitialInventory, sourceInitialItems);
            RestoreInventory(destination, destinationInitialInventory, destinationInitialItems);

            RunMutationSequence(source, destination, out var sourceFinalB, out var destinationFinalB);

            Assert.AreEqual(sourceFinalA.Amount, sourceFinalB.Amount, 0.001f);
            Assert.AreEqual(sourceFinalA.Reserved, sourceFinalB.Reserved, 0.001f);
            Assert.AreEqual(destinationFinalA.Amount, destinationFinalB.Amount, 0.001f);
            Assert.AreEqual(destinationFinalA.Reserved, destinationFinalB.Reserved, 0.001f);
            Assert.GreaterOrEqual(sourceFinalB.Reserved, 0f);
            Assert.GreaterOrEqual(destinationFinalB.Reserved, 0f);
        }

        private void RunMutationSequence(
            Entity source,
            Entity destination,
            out StorehouseInventoryItem sourceFinal,
            out StorehouseInventoryItem destinationFinal)
        {
            var sourceItems = _entityManager.GetBuffer<StorehouseInventoryItem>(source);
            var sourceInventory = _entityManager.GetComponentData<StorehouseInventory>(source);

            var reserved = StorehouseMutationService.TryReserveOut(
                0,
                6f,
                allowPartial: false,
                _resourceCatalog,
                sourceItems,
                out float reservedAmount);
            Assert.IsTrue(reserved);

            var withdrawnSuccess = StorehouseMutationService.CommitWithdrawReservedOut(
                0,
                reservedAmount,
                _resourceCatalog,
                ref sourceInventory,
                sourceItems,
                out float withdrawn);
            Assert.IsTrue(withdrawnSuccess);
            _entityManager.SetComponentData(source, sourceInventory);

            var destinationItems = _entityManager.GetBuffer<StorehouseInventoryItem>(destination);
            var destinationInventory = _entityManager.GetComponentData<StorehouseInventory>(destination);
            var destinationCapacities = _entityManager.GetBuffer<StorehouseCapacityElement>(destination);
            var destinationReservations = _entityManager.GetBuffer<StorehouseReservationItem>(destination);

            var deposited = StorehouseMutationService.TryDepositWithPerTypeCapacity(
                0,
                withdrawn,
                _resourceCatalog,
                ref destinationInventory,
                destinationItems,
                destinationCapacities,
                destinationReservations,
                out _);
            Assert.IsTrue(deposited);
            _entityManager.SetComponentData(destination, destinationInventory);

            sourceFinal = sourceItems.Length > 0 ? sourceItems[0] : default;
            destinationFinal = destinationItems.Length > 0 ? destinationItems[0] : default;
        }

        private void RestoreInventory(
            Entity entity,
            StorehouseInventory inventory,
            StorehouseInventoryItem[] items)
        {
            _entityManager.SetComponentData(entity, inventory);
            var buffer = _entityManager.GetBuffer<StorehouseInventoryItem>(entity);
            buffer.Clear();
            for (int i = 0; i < items.Length; i++)
            {
                buffer.Add(items[i]);
            }
        }

        private static StorehouseInventoryItem[] CopyItems(DynamicBuffer<StorehouseInventoryItem> items)
        {
            var copy = new StorehouseInventoryItem[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                copy[i] = items[i];
            }

            return copy;
        }

        private Entity CreateStorehouse(FixedString64Bytes resourceId, float amount, float perTypeCapacity)
        {
            var entity = _entityManager.CreateEntity(typeof(StorehouseInventory), typeof(StorehouseJobReservation));
            _entityManager.SetComponentData(entity, new StorehouseInventory
            {
                TotalCapacity = perTypeCapacity,
                TotalStored = amount,
                ItemTypeCount = amount > 0f ? 1 : 0
            });
            _entityManager.SetComponentData(entity, new StorehouseJobReservation
            {
                ReservedCapacity = 0f,
                LastMutationTick = 0
            });

            var items = _entityManager.AddBuffer<StorehouseInventoryItem>(entity);
            if (amount > 0f)
            {
                items.Add(new StorehouseInventoryItem
                {
                    ResourceTypeId = resourceId,
                    Amount = amount,
                    Reserved = 0f,
                    TierId = 0,
                    AverageQuality = 0
                });
            }

            var capacities = _entityManager.AddBuffer<StorehouseCapacityElement>(entity);
            capacities.Add(new StorehouseCapacityElement
            {
                ResourceTypeId = resourceId,
                MaxCapacity = perTypeCapacity
            });

            _entityManager.AddBuffer<StorehouseReservationItem>(entity);
            return entity;
        }

        private Entity CreateStorehouseWithCapacities(
            (FixedString64Bytes resourceId, float capacity)[] capacities,
            (FixedString64Bytes resourceId, float amount)[] stored)
        {
            var entity = _entityManager.CreateEntity(typeof(StorehouseInventory), typeof(StorehouseJobReservation));
            _entityManager.SetComponentData(entity, new StorehouseInventory
            {
                TotalCapacity = 0f,
                TotalStored = 0f,
                ItemTypeCount = 0
            });
            _entityManager.SetComponentData(entity, new StorehouseJobReservation
            {
                ReservedCapacity = 0f,
                LastMutationTick = 0
            });

            var items = _entityManager.AddBuffer<StorehouseInventoryItem>(entity);
            for (int i = 0; i < stored.Length; i++)
            {
                items.Add(new StorehouseInventoryItem
                {
                    ResourceTypeId = stored[i].resourceId,
                    Amount = stored[i].amount,
                    Reserved = 0f,
                    TierId = 0,
                    AverageQuality = 0
                });
            }

            var capacityBuffer = _entityManager.AddBuffer<StorehouseCapacityElement>(entity);
            var capacitySum = 0f;
            for (int i = 0; i < capacities.Length; i++)
            {
                capacitySum += capacities[i].capacity;
                capacityBuffer.Add(new StorehouseCapacityElement
                {
                    ResourceTypeId = capacities[i].resourceId,
                    MaxCapacity = capacities[i].capacity
                });
            }

            _entityManager.AddBuffer<StorehouseReservationItem>(entity);

            var inventory = _entityManager.GetComponentData<StorehouseInventory>(entity);
            inventory.TotalStored = 0f;
            for (int i = 0; i < stored.Length; i++)
            {
                inventory.TotalStored += stored[i].amount;
            }

            inventory.TotalCapacity = capacitySum;
            inventory.ItemTypeCount = items.Length;
            _entityManager.SetComponentData(entity, inventory);

            return entity;
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

        private static float GetItemAmount(DynamicBuffer<StorehouseInventoryItem> items, FixedString64Bytes resourceId)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ResourceTypeId.Equals(resourceId))
                {
                    return items[i].Amount;
                }
            }

            return 0f;
        }
    }
}
#endif

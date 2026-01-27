#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Tests.Logistics
{
    public class StorehouseApiTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("StorehouseApiTests");
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated)
            {
                _world.Dispose();
                if (World.DefaultGameObjectInjectionWorld == _world)
                {
                    World.DefaultGameObjectInjectionWorld = null;
                }
            }
        }

        [Test]
        public void TryDeposit_AddsNewItemAndUpdatesTotals()
        {
            var storehouse = CreateStorehouse(totalCapacity: 100f, totalStored: 0f);
            var resourceId = new FixedString64Bytes("ore");
            var inventory = _entityManager.GetComponentData<StorehouseInventory>(storehouse);
            var items = _entityManager.GetBuffer<StorehouseInventoryItem>(storehouse);

            var success = StorehouseApi.TryDeposit(
                storehouse,
                resourceId,
                25f,
                ref inventory,
                items,
                out float deposited);

            Assert.IsTrue(success);
            Assert.AreEqual(25f, deposited);

            _entityManager.SetComponentData(storehouse, inventory);

            var updatedInventory = _entityManager.GetComponentData<StorehouseInventory>(storehouse);
            Assert.AreEqual(25f, updatedInventory.TotalStored);
            Assert.AreEqual(1, items.Length);
            Assert.AreEqual(25f, items[0].Amount);
            Assert.IsTrue(items[0].ResourceTypeId.Equals(resourceId));
        }

        [Test]
        public void TryDeposit_RespectsCapacityLimit()
        {
            var storehouse = CreateStorehouse(totalCapacity: 10f, totalStored: 8f);
            var resourceId = new FixedString64Bytes("ore");
            var inventory = _entityManager.GetComponentData<StorehouseInventory>(storehouse);
            var items = _entityManager.GetBuffer<StorehouseInventoryItem>(storehouse);
            items.Add(new StorehouseInventoryItem
            {
                ResourceTypeId = resourceId,
                Amount = 8f,
                Reserved = 0f
            });

            var success = StorehouseApi.TryDeposit(
                storehouse,
                resourceId,
                10f,
                ref inventory,
                items,
                out float deposited);

            Assert.IsTrue(success, "Partial deposit should still succeed when there is free capacity.");
            Assert.AreEqual(2f, deposited);

            _entityManager.SetComponentData(storehouse, inventory);
            var updatedInventory = _entityManager.GetComponentData<StorehouseInventory>(storehouse);
            Assert.AreEqual(10f, updatedInventory.TotalStored);
            Assert.AreEqual(10f, items[0].Amount);
        }

        private Entity CreateStorehouse(float totalCapacity, float totalStored)
        {
            var entity = _entityManager.CreateEntity(typeof(StorehouseInventory));
            _entityManager.SetComponentData(entity, new StorehouseInventory
            {
                TotalCapacity = totalCapacity,
                TotalStored = totalStored,
                ItemTypeCount = totalStored > 0f ? 1 : 0
            });
            _entityManager.AddBuffer<StorehouseInventoryItem>(entity);
            return entity;
        }
    }
}
#endif


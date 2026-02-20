#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XInventoryProjectionSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XInventoryProjectionSystemTests");
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated)
            {
                _world.Dispose();
            }
        }

        [Test]
        public void ProjectionAggregatesCargoAndEquipmentAndTracksRevision()
        {
            var flagship = _entityManager.CreateEntity(typeof(PlayerFlagshipTag), typeof(MiningVessel));
            _entityManager.SetComponentData(flagship, new MiningVessel
            {
                VesselId = new FixedString64Bytes("flagship-miner"),
                CarrierEntity = Entity.Null,
                MiningEfficiency = 1f,
                Speed = 12f,
                CargoCapacity = 40f,
                CurrentCargo = 10f,
                CargoResourceType = ResourceType.Ore
            });

            var storage = _entityManager.AddBuffer<ResourceStorage>(flagship);
            storage.Add(new ResourceStorage
            {
                Type = ResourceType.Fuel,
                Amount = 25f,
                Capacity = 100f
            });
            storage.Add(new ResourceStorage
            {
                Type = ResourceType.Water,
                Amount = 12f,
                Capacity = 20f
            });

            var module = _entityManager.CreateEntity(typeof(ModuleTypeId));
            _entityManager.SetComponentData(module, new ModuleTypeId
            {
                Value = new FixedString64Bytes("module-laser-a")
            });

            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(flagship);
            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                SlotSize = ModuleSlotSize.Medium,
                CurrentModule = module,
                TargetModule = Entity.Null,
                RefitProgress = 0f,
                State = ModuleSlotState.Active
            });
            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 1,
                SlotSize = ModuleSlotSize.Small,
                CurrentModule = Entity.Null,
                TargetModule = Entity.Null,
                RefitProgress = 0f,
                State = ModuleSlotState.Empty
            });

            var system = _world.GetOrCreateSystem<Space4XInventoryProjectionSystem>();
            system.Update(_world.Unmanaged);

            var projection = _entityManager.GetComponentData<Space4XInventoryProjection>(flagship);
            Assert.AreEqual(47f, projection.CargoUsed, 1e-4f);
            Assert.AreEqual(160f, projection.CargoCapacity, 1e-4f);
            Assert.AreEqual(47f / 160f, projection.CargoUtilization, 1e-4f);
            Assert.AreEqual(3, projection.CargoEntryCount);
            Assert.AreEqual(2, projection.EquipmentEntryCount);
            Assert.AreEqual(1u, projection.Revision);
            Assert.AreEqual(1, projection.Dirty);

            var cargo = _entityManager.GetBuffer<Space4XCargoProjectionEntry>(flagship);
            Assert.AreEqual(Space4XCargoSource.VesselHold, cargo[0].Source);
            Assert.AreEqual(ResourceType.Ore, cargo[0].ResourceType);
            Assert.AreEqual(10f, cargo[0].Amount, 1e-4f);
            Assert.AreEqual(40f, cargo[0].Capacity, 1e-4f);

            var equipment = _entityManager.GetBuffer<Space4XEquipmentProjectionEntry>(flagship);
            Assert.AreEqual(2, equipment.Length);
            Assert.AreEqual(module, equipment[0].ModuleEntity);
            Assert.AreEqual(new FixedString64Bytes("module-laser-a"), equipment[0].ModuleTypeId);

            var stableRevision = projection.Revision;
            system.Update(_world.Unmanaged);
            projection = _entityManager.GetComponentData<Space4XInventoryProjection>(flagship);
            Assert.AreEqual(stableRevision, projection.Revision);
            Assert.AreEqual(0, projection.Dirty);

            storage = _entityManager.GetBuffer<ResourceStorage>(flagship);
            var updated = storage[0];
            updated.Amount = 30f;
            storage[0] = updated;

            system.Update(_world.Unmanaged);
            projection = _entityManager.GetComponentData<Space4XInventoryProjection>(flagship);
            Assert.AreEqual(52f, projection.CargoUsed, 1e-4f);
            Assert.AreEqual(stableRevision + 1u, projection.Revision);
            Assert.AreEqual(1, projection.Dirty);
        }

        [Test]
        public void ProjectionBuffersAreCreatedForFlagshipWithoutCargoData()
        {
            var flagship = _entityManager.CreateEntity(typeof(PlayerFlagshipTag));
            var system = _world.GetOrCreateSystem<Space4XInventoryProjectionSystem>();
            system.Update(_world.Unmanaged);

            Assert.IsTrue(_entityManager.HasComponent<Space4XInventoryProjection>(flagship));
            Assert.IsTrue(_entityManager.HasBuffer<Space4XCargoProjectionEntry>(flagship));
            Assert.IsTrue(_entityManager.HasBuffer<Space4XEquipmentProjectionEntry>(flagship));
            Assert.IsTrue(_entityManager.HasComponent<Space4XShipFitStatusProjection>(flagship));
            Assert.IsTrue(_entityManager.HasBuffer<Space4XShipFitStatusFeedEntry>(flagship));

            var projection = _entityManager.GetComponentData<Space4XInventoryProjection>(flagship);
            Assert.AreEqual(0f, projection.CargoUsed, 1e-4f);
            Assert.AreEqual(0f, projection.CargoCapacity, 1e-4f);
            Assert.AreEqual(0, projection.CargoEntryCount);
            Assert.AreEqual(0, projection.EquipmentEntryCount);
            Assert.AreEqual(0u, projection.Revision);
            Assert.AreEqual(0, projection.Dirty);
        }

        [Test]
        public void ProjectionBuildsShipFitStatusAndEventFeed()
        {
            var flagship = _entityManager.CreateEntity(typeof(PlayerFlagshipTag), typeof(Space4XShipFitLastResult));
            _entityManager.SetComponentData(flagship, new Space4XShipFitLastResult
            {
                Revision = 5u,
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleSocket,
                TargetIndex = 0,
                Code = Space4XShipFitResultCode.MountTypeMismatch
            });

            var fitEvents = _entityManager.AddBuffer<Space4XShipFitResultEvent>(flagship);
            fitEvents.Add(new Space4XShipFitResultEvent
            {
                Revision = 4u,
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleInventory,
                TargetIndex = 0,
                Code = Space4XShipFitResultCode.Success
            });
            fitEvents.Add(new Space4XShipFitResultEvent
            {
                Revision = 5u,
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleSocket,
                TargetIndex = 0,
                Code = Space4XShipFitResultCode.MountTypeMismatch
            });

            var system = _world.GetOrCreateSystem<Space4XInventoryProjectionSystem>();
            system.Update(_world.Unmanaged);

            var status = _entityManager.GetComponentData<Space4XShipFitStatusProjection>(flagship);
            Assert.AreEqual(1u, status.Revision);
            Assert.AreEqual(5u, status.LastConsumedResultRevision);
            Assert.AreEqual(5u, status.LastConsumedEventRevision);
            Assert.AreEqual(Space4XShipFitResultCode.MountTypeMismatch, status.Code);
            Assert.AreEqual(Space4XShipFitUiTone.Warning, status.Tone);
            Assert.AreEqual(new FixedString128Bytes("Module mount type does not match socket."), status.Message);
            Assert.AreEqual(1, status.Dirty);

            var feed = _entityManager.GetBuffer<Space4XShipFitStatusFeedEntry>(flagship);
            Assert.AreEqual(2, feed.Length);
            Assert.AreEqual(4u, feed[0].Revision);
            Assert.AreEqual(Space4XShipFitResultCode.Success, feed[0].Code);
            Assert.AreEqual(Space4XShipFitUiTone.Positive, feed[0].Tone);
            Assert.AreEqual(5u, feed[1].Revision);
            Assert.AreEqual(Space4XShipFitResultCode.MountTypeMismatch, feed[1].Code);
            Assert.AreEqual(Space4XShipFitUiTone.Warning, feed[1].Tone);

            system.Update(_world.Unmanaged);
            status = _entityManager.GetComponentData<Space4XShipFitStatusProjection>(flagship);
            feed = _entityManager.GetBuffer<Space4XShipFitStatusFeedEntry>(flagship);
            Assert.AreEqual(1u, status.Revision);
            Assert.AreEqual(0, status.Dirty);
            Assert.AreEqual(2, feed.Length);

            _entityManager.SetComponentData(flagship, new Space4XShipFitLastResult
            {
                Revision = 6u,
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleInventory,
                TargetIndex = 1,
                Code = Space4XShipFitResultCode.Success
            });
            fitEvents = _entityManager.GetBuffer<Space4XShipFitResultEvent>(flagship);
            fitEvents.Add(new Space4XShipFitResultEvent
            {
                Revision = 6u,
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleInventory,
                TargetIndex = 1,
                Code = Space4XShipFitResultCode.Success
            });

            system.Update(_world.Unmanaged);
            status = _entityManager.GetComponentData<Space4XShipFitStatusProjection>(flagship);
            feed = _entityManager.GetBuffer<Space4XShipFitStatusFeedEntry>(flagship);
            Assert.AreEqual(2u, status.Revision);
            Assert.AreEqual(6u, status.LastConsumedResultRevision);
            Assert.AreEqual(6u, status.LastConsumedEventRevision);
            Assert.AreEqual(Space4XShipFitResultCode.Success, status.Code);
            Assert.AreEqual(Space4XShipFitUiTone.Positive, status.Tone);
            Assert.AreEqual(new FixedString128Bytes("Loadout updated."), status.Message);
            Assert.AreEqual(1, status.Dirty);
            Assert.AreEqual(3, feed.Length);
            Assert.AreEqual(6u, feed[2].Revision);
            Assert.AreEqual(Space4XShipFitResultCode.Success, feed[2].Code);
        }
    }
}
#endif

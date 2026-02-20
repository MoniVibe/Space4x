#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XShipFitInteractionSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XShipFitInteractionSystemTests");
            _entityManager = _world.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(_entityManager);
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
        public void ClickSwapFlowSwapsModuleAndSegmentTargets()
        {
            var flagship = _entityManager.CreateEntity(typeof(PlayerFlagshipTag), typeof(Space4XShipFitCursorState));
            _entityManager.SetComponentData(flagship, Space4XShipFitCursorState.Empty);

            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(flagship);
            var segments = _entityManager.AddBuffer<CarrierHullSegment>(flagship);
            var moduleInventory = _entityManager.AddBuffer<Space4XModuleInventoryEntry>(flagship);
            var segmentInventory = _entityManager.AddBuffer<Space4XSegmentInventoryEntry>(flagship);
            var requests = _entityManager.AddBuffer<Space4XShipFitRequest>(flagship);
            _entityManager.AddBuffer<Space4XCarrierModuleSocketLayout>(flagship);

            var moduleInSlot = _entityManager.CreateEntity(typeof(ModuleSlotRequirement));
            _entityManager.SetComponentData(moduleInSlot, new ModuleSlotRequirement
            {
                SlotSize = ModuleSlotSize.Small
            });

            var moduleInBag = _entityManager.CreateEntity(typeof(ModuleSlotRequirement));
            _entityManager.SetComponentData(moduleInBag, new ModuleSlotRequirement
            {
                SlotSize = ModuleSlotSize.Small
            });

            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                SlotSize = ModuleSlotSize.Small,
                CurrentModule = moduleInSlot,
                TargetModule = moduleInSlot,
                RefitProgress = 0f,
                State = ModuleSlotState.Active
            });

            segments.Add(new CarrierHullSegment
            {
                SegmentIndex = 0,
                SegmentId = new FixedString64Bytes("carrier-bridge-m1")
            });

            moduleInventory.Add(new Space4XModuleInventoryEntry { Module = moduleInBag });
            segmentInventory.Add(new Space4XSegmentInventoryEntry
            {
                SegmentId = new FixedString64Bytes("carrier-stern-m1")
            });

            var system = _world.GetOrCreateSystem<Space4XShipFitInteractionSystem>();

            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleInventory,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);
            var lastResult = _entityManager.GetComponentData<Space4XShipFitLastResult>(flagship);
            Assert.AreEqual(Space4XShipFitResultCode.Success, lastResult.Code);

            var cursor = _entityManager.GetComponentData<Space4XShipFitCursorState>(flagship);
            Assert.AreEqual(Space4XShipFitItemKind.Module, cursor.HeldKind);
            Assert.AreEqual(moduleInBag, cursor.HeldModule);
            Assert.AreEqual(0, _entityManager.GetBuffer<Space4XModuleInventoryEntry>(flagship).Length);

            requests = _entityManager.GetBuffer<Space4XShipFitRequest>(flagship);
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleSocket,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);
            lastResult = _entityManager.GetComponentData<Space4XShipFitLastResult>(flagship);
            Assert.AreEqual(Space4XShipFitResultCode.Success, lastResult.Code);

            slots = _entityManager.GetBuffer<CarrierModuleSlot>(flagship);
            cursor = _entityManager.GetComponentData<Space4XShipFitCursorState>(flagship);
            Assert.AreEqual(moduleInBag, slots[0].CurrentModule);
            Assert.AreEqual(moduleInSlot, cursor.HeldModule);

            requests = _entityManager.GetBuffer<Space4XShipFitRequest>(flagship);
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleInventory,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);
            lastResult = _entityManager.GetComponentData<Space4XShipFitLastResult>(flagship);
            Assert.AreEqual(Space4XShipFitResultCode.Success, lastResult.Code);

            cursor = _entityManager.GetComponentData<Space4XShipFitCursorState>(flagship);
            moduleInventory = _entityManager.GetBuffer<Space4XModuleInventoryEntry>(flagship);
            Assert.AreEqual(Space4XShipFitItemKind.None, cursor.HeldKind);
            Assert.AreEqual(1, moduleInventory.Length);
            Assert.AreEqual(moduleInSlot, moduleInventory[0].Module);

            requests = _entityManager.GetBuffer<Space4XShipFitRequest>(flagship);
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.SegmentInventory,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);
            lastResult = _entityManager.GetComponentData<Space4XShipFitLastResult>(flagship);
            Assert.AreEqual(Space4XShipFitResultCode.Success, lastResult.Code);

            cursor = _entityManager.GetComponentData<Space4XShipFitCursorState>(flagship);
            Assert.AreEqual(Space4XShipFitItemKind.Segment, cursor.HeldKind);
            Assert.AreEqual(new FixedString64Bytes("carrier-stern-m1"), cursor.HeldSegmentId);

            requests = _entityManager.GetBuffer<Space4XShipFitRequest>(flagship);
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.SegmentSocket,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);
            lastResult = _entityManager.GetComponentData<Space4XShipFitLastResult>(flagship);
            Assert.AreEqual(Space4XShipFitResultCode.Success, lastResult.Code);

            cursor = _entityManager.GetComponentData<Space4XShipFitCursorState>(flagship);
            segments = _entityManager.GetBuffer<CarrierHullSegment>(flagship);
            Assert.AreEqual(new FixedString64Bytes("carrier-stern-m1"), segments[0].SegmentId);
            Assert.AreEqual(new FixedString64Bytes("carrier-bridge-m1"), cursor.HeldSegmentId);

            requests = _entityManager.GetBuffer<Space4XShipFitRequest>(flagship);
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.SegmentInventory,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);
            lastResult = _entityManager.GetComponentData<Space4XShipFitLastResult>(flagship);
            Assert.AreEqual(Space4XShipFitResultCode.Success, lastResult.Code);

            cursor = _entityManager.GetComponentData<Space4XShipFitCursorState>(flagship);
            segmentInventory = _entityManager.GetBuffer<Space4XSegmentInventoryEntry>(flagship);
            Assert.AreEqual(Space4XShipFitItemKind.None, cursor.HeldKind);
            Assert.AreEqual(1, segmentInventory.Length);
            Assert.AreEqual(new FixedString64Bytes("carrier-bridge-m1"), segmentInventory[0].SegmentId);
        }

        [Test]
        public void SocketLayoutDerivesMountTypeAndSegmentMapping()
        {
            var bootstrap = _world.GetOrCreateSystem<ModuleCatalogBootstrapSystem>();
            bootstrap.Update(_world.Unmanaged);

            var flagship = _entityManager.CreateEntity(typeof(PlayerFlagshipTag), typeof(CarrierHullId));
            _entityManager.SetComponentData(flagship, new CarrierHullId
            {
                HullId = new FixedString64Bytes("cv-mule")
            });

            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(flagship);
            slots.Add(new CarrierModuleSlot { SlotIndex = 0, SlotSize = ModuleSlotSize.Medium, State = ModuleSlotState.Empty });
            slots.Add(new CarrierModuleSlot { SlotIndex = 1, SlotSize = ModuleSlotSize.Medium, State = ModuleSlotState.Empty });
            slots.Add(new CarrierModuleSlot { SlotIndex = 2, SlotSize = ModuleSlotSize.Medium, State = ModuleSlotState.Empty });

            var segments = _entityManager.AddBuffer<CarrierHullSegment>(flagship);
            segments.Add(new CarrierHullSegment { SegmentIndex = 10, SegmentId = new FixedString64Bytes("carrier-bridge-m1") });
            segments.Add(new CarrierHullSegment { SegmentIndex = 20, SegmentId = new FixedString64Bytes("carrier-stern-m1") });

            var system = _world.GetOrCreateSystem<Space4XShipFitInteractionSystem>();
            system.Update(_world.Unmanaged);

            var layout = _entityManager.GetBuffer<Space4XCarrierModuleSocketLayout>(flagship);
            Assert.AreEqual(3, layout.Length);
            Assert.AreEqual(MountType.Core, layout[0].MountType);
            Assert.AreEqual(MountType.Engine, layout[1].MountType);
            Assert.AreEqual(MountType.Hangar, layout[2].MountType);
            Assert.AreEqual(10, layout[0].SegmentIndex);
            Assert.AreEqual(20, layout[1].SegmentIndex);
            Assert.AreEqual(10, layout[2].SegmentIndex);
            Assert.AreEqual(0, layout[0].SegmentSocketIndex);
            Assert.AreEqual(0, layout[1].SegmentSocketIndex);
            Assert.AreEqual(1, layout[2].SegmentSocketIndex);
        }

        [Test]
        public void ModuleSocketRejectsMountMismatchAndPublishesFailure()
        {
            var bootstrap = _world.GetOrCreateSystem<ModuleCatalogBootstrapSystem>();
            bootstrap.Update(_world.Unmanaged);

            var flagship = _entityManager.CreateEntity(typeof(PlayerFlagshipTag), typeof(CarrierHullId));
            _entityManager.SetComponentData(flagship, new CarrierHullId
            {
                HullId = new FixedString64Bytes("cv-mule")
            });

            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(flagship);
            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                SlotSize = ModuleSlotSize.Medium,
                CurrentModule = Entity.Null,
                TargetModule = Entity.Null,
                RefitProgress = 0f,
                State = ModuleSlotState.Empty
            });

            var module = _entityManager.CreateEntity(typeof(ModuleTypeId));
            _entityManager.SetComponentData(module, new ModuleTypeId
            {
                Value = new FixedString64Bytes("missile-m-1")
            });

            var inventory = _entityManager.AddBuffer<Space4XModuleInventoryEntry>(flagship);
            inventory.Add(new Space4XModuleInventoryEntry { Module = module });

            var requests = _entityManager.AddBuffer<Space4XShipFitRequest>(flagship);
            _entityManager.AddBuffer<Space4XCarrierModuleSocketLayout>(flagship);

            var system = _world.GetOrCreateSystem<Space4XShipFitInteractionSystem>();

            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleInventory,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);

            requests = _entityManager.GetBuffer<Space4XShipFitRequest>(flagship);
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleSocket,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);

            var lastResult = _entityManager.GetComponentData<Space4XShipFitLastResult>(flagship);
            Assert.AreEqual(Space4XShipFitResultCode.MountTypeMismatch, lastResult.Code);

            slots = _entityManager.GetBuffer<CarrierModuleSlot>(flagship);
            Assert.AreEqual(Entity.Null, slots[0].CurrentModule);
            var cursor = _entityManager.GetComponentData<Space4XShipFitCursorState>(flagship);
            Assert.AreEqual(module, cursor.HeldModule);
        }

        [Test]
        public void StationHostProfileWithoutHullAllowsMountAgnosticEquip()
        {
            var bootstrap = _world.GetOrCreateSystem<ModuleCatalogBootstrapSystem>();
            bootstrap.Update(_world.Unmanaged);

            var flagship = _entityManager.CreateEntity(typeof(PlayerFlagshipTag), typeof(Space4XModuleHostProfile));
            _entityManager.SetComponentData(flagship, new Space4XModuleHostProfile
            {
                Kind = Space4XModuleHostKind.Station,
                HostId = new FixedString64Bytes("trade-hub"),
                UsesHullSlots = 0,
                ValidateMountType = 0,
                ValidateSegments = 0
            });

            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(flagship);
            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                SlotSize = ModuleSlotSize.Medium,
                CurrentModule = Entity.Null,
                TargetModule = Entity.Null,
                State = ModuleSlotState.Empty
            });

            var module = _entityManager.CreateEntity(typeof(ModuleTypeId));
            _entityManager.SetComponentData(module, new ModuleTypeId
            {
                Value = new FixedString64Bytes("missile-m-1")
            });

            var inventory = _entityManager.AddBuffer<Space4XModuleInventoryEntry>(flagship);
            inventory.Add(new Space4XModuleInventoryEntry { Module = module });
            var requests = _entityManager.AddBuffer<Space4XShipFitRequest>(flagship);
            _entityManager.AddBuffer<Space4XCarrierModuleSocketLayout>(flagship);

            var system = _world.GetOrCreateSystem<Space4XShipFitInteractionSystem>();
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleInventory,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);

            requests = _entityManager.GetBuffer<Space4XShipFitRequest>(flagship);
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleSocket,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);

            var lastResult = _entityManager.GetComponentData<Space4XShipFitLastResult>(flagship);
            Assert.AreEqual(Space4XShipFitResultCode.Success, lastResult.Code);
            slots = _entityManager.GetBuffer<CarrierModuleSlot>(flagship);
            Assert.AreEqual(module, slots[0].CurrentModule);
        }

        [Test]
        public void StationHostProfileWithHullUsesHullMountLayoutAndRejectsMismatch()
        {
            var bootstrap = _world.GetOrCreateSystem<ModuleCatalogBootstrapSystem>();
            bootstrap.Update(_world.Unmanaged);

            var flagship = _entityManager.CreateEntity(typeof(PlayerFlagshipTag), typeof(Space4XModuleHostProfile));
            _entityManager.SetComponentData(flagship, new Space4XModuleHostProfile
            {
                Kind = Space4XModuleHostKind.Station,
                HostId = new FixedString64Bytes("cv-mule"),
                UsesHullSlots = 1,
                ValidateMountType = 1,
                ValidateSegments = 0
            });

            var slots = _entityManager.AddBuffer<CarrierModuleSlot>(flagship);
            slots.Add(new CarrierModuleSlot
            {
                SlotIndex = 0,
                SlotSize = ModuleSlotSize.Medium,
                CurrentModule = Entity.Null,
                TargetModule = Entity.Null,
                State = ModuleSlotState.Empty
            });

            var module = _entityManager.CreateEntity(typeof(ModuleTypeId));
            _entityManager.SetComponentData(module, new ModuleTypeId
            {
                Value = new FixedString64Bytes("missile-m-1")
            });

            var inventory = _entityManager.AddBuffer<Space4XModuleInventoryEntry>(flagship);
            inventory.Add(new Space4XModuleInventoryEntry { Module = module });
            var requests = _entityManager.AddBuffer<Space4XShipFitRequest>(flagship);
            _entityManager.AddBuffer<Space4XCarrierModuleSocketLayout>(flagship);

            var system = _world.GetOrCreateSystem<Space4XShipFitInteractionSystem>();
            system.Update(_world.Unmanaged);

            var layout = _entityManager.GetBuffer<Space4XCarrierModuleSocketLayout>(flagship);
            Assert.AreEqual(1, layout.Length);
            Assert.AreEqual(MountType.Core, layout[0].MountType);

            requests = _entityManager.GetBuffer<Space4XShipFitRequest>(flagship);
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleInventory,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);

            requests = _entityManager.GetBuffer<Space4XShipFitRequest>(flagship);
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.LeftClick,
                TargetKind = Space4XShipFitTargetKind.ModuleSocket,
                TargetIndex = 0
            });
            system.Update(_world.Unmanaged);

            var lastResult = _entityManager.GetComponentData<Space4XShipFitLastResult>(flagship);
            Assert.AreEqual(Space4XShipFitResultCode.MountTypeMismatch, lastResult.Code);
            slots = _entityManager.GetBuffer<CarrierModuleSlot>(flagship);
            Assert.AreEqual(Entity.Null, slots[0].CurrentModule);
        }

        [Test]
        public void CancelHeldWithoutCursorPublishesNoHeldItemResult()
        {
            var flagship = _entityManager.CreateEntity(typeof(PlayerFlagshipTag), typeof(Space4XShipFitCursorState));
            _entityManager.SetComponentData(flagship, Space4XShipFitCursorState.Empty);
            _entityManager.AddBuffer<Space4XShipFitRequest>(flagship);
            _entityManager.AddBuffer<Space4XCarrierModuleSocketLayout>(flagship);

            var system = _world.GetOrCreateSystem<Space4XShipFitInteractionSystem>();
            var requests = _entityManager.GetBuffer<Space4XShipFitRequest>(flagship);
            requests.Add(new Space4XShipFitRequest
            {
                RequestType = Space4XShipFitRequestType.CancelHeld,
                TargetKind = Space4XShipFitTargetKind.None,
                TargetIndex = -1
            });

            system.Update(_world.Unmanaged);

            var lastResult = _entityManager.GetComponentData<Space4XShipFitLastResult>(flagship);
            Assert.AreEqual(Space4XShipFitResultCode.NoHeldItem, lastResult.Code);
            Assert.AreEqual(Space4XShipFitRequestType.CancelHeld, lastResult.RequestType);
            Assert.AreEqual(Space4XShipFitTargetKind.None, lastResult.TargetKind);
            Assert.AreEqual(-1, lastResult.TargetIndex);

            var events = _entityManager.GetBuffer<Space4XShipFitResultEvent>(flagship);
            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(Space4XShipFitResultCode.NoHeldItem, events[0].Code);
            Assert.AreEqual(Space4XShipFitRequestType.CancelHeld, events[0].RequestType);
            Assert.AreEqual(Space4XShipFitTargetKind.None, events[0].TargetKind);
            Assert.AreEqual(-1, events[0].TargetIndex);
        }
    }
}
#endif

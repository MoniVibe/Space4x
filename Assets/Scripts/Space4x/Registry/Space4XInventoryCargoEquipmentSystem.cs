using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Builds inventory/cargo/equipment projection buffers for player flagship entities.
    /// This is a backend skeleton intended for upcoming UI binding work.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XInventoryProjectionSystem : ISystem
    {
        private const int MaxShipFitStatusFeedEntries = 32;

        private ComponentLookup<MiningVessel> _miningVesselLookup;
        private ComponentLookup<ModuleTypeId> _moduleTypeIdLookup;
        private ComponentLookup<Space4XShipFitLastResult> _shipFitLastResultLookup;
        private BufferLookup<ResourceStorage> _resourceStorageLookup;
        private BufferLookup<CarrierModuleSlot> _carrierModuleSlotLookup;
        private BufferLookup<Space4XCarrierModuleSocketLayout> _socketLayoutLookup;
        private BufferLookup<Space4XShipFitResultEvent> _shipFitResultEventLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerFlagshipTag>();
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _moduleTypeIdLookup = state.GetComponentLookup<ModuleTypeId>(true);
            _shipFitLastResultLookup = state.GetComponentLookup<Space4XShipFitLastResult>(true);
            _resourceStorageLookup = state.GetBufferLookup<ResourceStorage>(true);
            _carrierModuleSlotLookup = state.GetBufferLookup<CarrierModuleSlot>(true);
            _socketLayoutLookup = state.GetBufferLookup<Space4XCarrierModuleSocketLayout>(true);
            _shipFitResultEventLookup = state.GetBufferLookup<Space4XShipFitResultEvent>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _miningVesselLookup.Update(ref state);
            _moduleTypeIdLookup.Update(ref state);
            _shipFitLastResultLookup.Update(ref state);
            _resourceStorageLookup.Update(ref state);
            _carrierModuleSlotLookup.Update(ref state);
            _socketLayoutLookup.Update(ref state);
            _shipFitResultEventLookup.Update(ref state);

            EnsureProjectionComponents(ref state);

            foreach (var (projectionRef, shipFitStatusRef, cargoBuffer, equipmentBuffer, shipFitFeedBuffer, entity) in SystemAPI
                         .Query<RefRW<Space4XInventoryProjection>, RefRW<Space4XShipFitStatusProjection>, DynamicBuffer<Space4XCargoProjectionEntry>, DynamicBuffer<Space4XEquipmentProjectionEntry>, DynamicBuffer<Space4XShipFitStatusFeedEntry>>()
                         .WithAll<PlayerFlagshipTag>()
                         .WithEntityAccess())
            {
                BuildCargoProjection(entity, cargoBuffer, out var cargoUsed, out var cargoCapacity);
                BuildEquipmentProjection(entity, equipmentBuffer);
                var shipFitStatus = shipFitStatusRef.ValueRO;
                BuildShipFitStatusProjection(entity, ref shipFitStatus, shipFitFeedBuffer);
                shipFitStatusRef.ValueRW = shipFitStatus;

                var previous = projectionRef.ValueRO;
                var cargoCount = cargoBuffer.Length;
                var equipmentCount = equipmentBuffer.Length;
                var utilization = cargoCapacity > 1e-4f ? math.saturate(cargoUsed / cargoCapacity) : 0f;
                var dirty =
                    math.abs(previous.CargoUsed - cargoUsed) > 1e-4f ||
                    math.abs(previous.CargoCapacity - cargoCapacity) > 1e-4f ||
                    previous.CargoEntryCount != cargoCount ||
                    previous.EquipmentEntryCount != equipmentCount;

                projectionRef.ValueRW = new Space4XInventoryProjection
                {
                    CargoUsed = cargoUsed,
                    CargoCapacity = cargoCapacity,
                    CargoUtilization = utilization,
                    CargoEntryCount = cargoCount,
                    EquipmentEntryCount = equipmentCount,
                    Revision = dirty ? previous.Revision + 1u : previous.Revision,
                    Dirty = dirty ? (byte)1 : (byte)0
                };
            }
        }

        private void EnsureProjectionComponents(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlayerFlagshipTag>>().WithEntityAccess())
            {
                if (!entityManager.HasComponent<Space4XInventoryProjection>(entity))
                {
                    ecb.AddComponent(entity, new Space4XInventoryProjection
                    {
                        CargoUsed = 0f,
                        CargoCapacity = 0f,
                        CargoUtilization = 0f,
                        CargoEntryCount = 0,
                        EquipmentEntryCount = 0,
                        Revision = 0u,
                        Dirty = 1
                    });
                }

                if (!entityManager.HasBuffer<Space4XCargoProjectionEntry>(entity))
                {
                    ecb.AddBuffer<Space4XCargoProjectionEntry>(entity);
                }

                if (!entityManager.HasBuffer<Space4XEquipmentProjectionEntry>(entity))
                {
                    ecb.AddBuffer<Space4XEquipmentProjectionEntry>(entity);
                }

                if (!entityManager.HasComponent<Space4XShipFitStatusProjection>(entity))
                {
                    ecb.AddComponent(entity, new Space4XShipFitStatusProjection
                    {
                        Revision = 0u,
                        LastConsumedResultRevision = 0u,
                        LastConsumedEventRevision = 0u,
                        RequestType = Space4XShipFitRequestType.LeftClick,
                        TargetKind = Space4XShipFitTargetKind.None,
                        TargetIndex = -1,
                        Code = Space4XShipFitResultCode.None,
                        Tone = Space4XShipFitUiTone.None,
                        Message = default,
                        Dirty = 0
                    });
                }

                if (!entityManager.HasBuffer<Space4XShipFitStatusFeedEntry>(entity))
                {
                    ecb.AddBuffer<Space4XShipFitStatusFeedEntry>(entity);
                }
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        private void BuildCargoProjection(
            Entity entity,
            DynamicBuffer<Space4XCargoProjectionEntry> cargoBuffer,
            out float totalUsed,
            out float totalCapacity)
        {
            cargoBuffer.Clear();
            totalUsed = 0f;
            totalCapacity = 0f;

            if (_miningVesselLookup.HasComponent(entity))
            {
                var vessel = _miningVesselLookup[entity];
                var used = math.max(0f, vessel.CurrentCargo);
                var capacity = math.max(0f, vessel.CargoCapacity);
                cargoBuffer.Add(new Space4XCargoProjectionEntry
                {
                    Source = Space4XCargoSource.VesselHold,
                    ResourceType = vessel.CargoResourceType,
                    Label = ResolveResourceLabel(vessel.CargoResourceType),
                    Amount = used,
                    Capacity = capacity
                });

                totalUsed += used;
                totalCapacity += capacity;
            }

            if (_resourceStorageLookup.HasBuffer(entity))
            {
                var storage = _resourceStorageLookup[entity];
                for (var i = 0; i < storage.Length; i++)
                {
                    var entry = storage[i];
                    var used = math.max(0f, entry.Amount);
                    var capacity = math.max(0f, entry.Capacity);
                    cargoBuffer.Add(new Space4XCargoProjectionEntry
                    {
                        Source = Space4XCargoSource.CarrierStorage,
                        ResourceType = entry.Type,
                        Label = ResolveResourceLabel(entry.Type),
                        Amount = used,
                        Capacity = capacity
                    });

                    totalUsed += used;
                    totalCapacity += capacity;
                }
            }
        }

        private void BuildEquipmentProjection(
            Entity entity,
            DynamicBuffer<Space4XEquipmentProjectionEntry> equipmentBuffer)
        {
            equipmentBuffer.Clear();
            if (!_carrierModuleSlotLookup.HasBuffer(entity))
            {
                return;
            }

            var slots = _carrierModuleSlotLookup[entity];
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var moduleType = default(FixedString64Bytes);
                if (slot.CurrentModule != Entity.Null && _moduleTypeIdLookup.HasComponent(slot.CurrentModule))
                {
                    moduleType = _moduleTypeIdLookup[slot.CurrentModule].Value;
                }

                var segmentIndex = (byte)0;
                var segmentSocketIndex = (byte)0;
                var mountType = MountType.Utility;
                if (_socketLayoutLookup.HasBuffer(entity))
                {
                    var layout = _socketLayoutLookup[entity];
                    if (TryGetSocketLayout(layout, slot.SlotIndex, out var layoutEntry))
                    {
                        segmentIndex = layoutEntry.SegmentIndex;
                        segmentSocketIndex = layoutEntry.SegmentSocketIndex;
                        mountType = layoutEntry.MountType;
                    }
                }

                equipmentBuffer.Add(new Space4XEquipmentProjectionEntry
                {
                    SlotIndex = slot.SlotIndex,
                    SlotSize = slot.SlotSize,
                    SlotState = slot.State,
                    ModuleEntity = slot.CurrentModule,
                    ModuleTypeId = moduleType,
                    SegmentIndex = segmentIndex,
                    SegmentSocketIndex = segmentSocketIndex,
                    MountType = mountType
                });
            }
        }

        private static bool TryGetSocketLayout(
            DynamicBuffer<Space4XCarrierModuleSocketLayout> layout,
            int slotIndex,
            out Space4XCarrierModuleSocketLayout entry)
        {
            for (var i = 0; i < layout.Length; i++)
            {
                if (layout[i].SlotIndex == slotIndex)
                {
                    entry = layout[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        private static FixedString64Bytes ResolveResourceLabel(ResourceType type)
        {
            return new FixedString64Bytes(type.ToString());
        }

        private void BuildShipFitStatusProjection(
            Entity entity,
            ref Space4XShipFitStatusProjection projection,
            DynamicBuffer<Space4XShipFitStatusFeedEntry> feedBuffer)
        {
            var dirty = false;

            if (_shipFitLastResultLookup.HasComponent(entity))
            {
                var lastResult = _shipFitLastResultLookup[entity];
                if (lastResult.Revision > projection.LastConsumedResultRevision)
                {
                    projection.Revision += 1u;
                    projection.LastConsumedResultRevision = lastResult.Revision;
                    projection.RequestType = lastResult.RequestType;
                    projection.TargetKind = lastResult.TargetKind;
                    projection.TargetIndex = lastResult.TargetIndex;
                    projection.Code = lastResult.Code;
                    projection.Tone = ResolveShipFitTone(lastResult.Code);
                    projection.Message = ResolveShipFitMessage(lastResult.Code);
                    dirty = true;
                }
            }

            if (_shipFitResultEventLookup.HasBuffer(entity))
            {
                var events = _shipFitResultEventLookup[entity];
                for (var i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    if (evt.Revision <= projection.LastConsumedEventRevision)
                    {
                        continue;
                    }

                    feedBuffer.Add(new Space4XShipFitStatusFeedEntry
                    {
                        Revision = evt.Revision,
                        RequestType = evt.RequestType,
                        TargetKind = evt.TargetKind,
                        TargetIndex = evt.TargetIndex,
                        Code = evt.Code,
                        Tone = ResolveShipFitTone(evt.Code),
                        Message = ResolveShipFitMessage(evt.Code)
                    });
                    projection.LastConsumedEventRevision = evt.Revision;
                    dirty = true;
                }
            }

            while (feedBuffer.Length > MaxShipFitStatusFeedEntries)
            {
                feedBuffer.RemoveAt(0);
            }

            projection.Dirty = dirty ? (byte)1 : (byte)0;
        }

        private static Space4XShipFitUiTone ResolveShipFitTone(Space4XShipFitResultCode code)
        {
            return code switch
            {
                Space4XShipFitResultCode.Success => Space4XShipFitUiTone.Positive,
                Space4XShipFitResultCode.None => Space4XShipFitUiTone.None,
                Space4XShipFitResultCode.NoHeldItem => Space4XShipFitUiTone.Neutral,
                Space4XShipFitResultCode.EmptySource => Space4XShipFitUiTone.Neutral,
                Space4XShipFitResultCode.ModuleSpecMissing => Space4XShipFitUiTone.Error,
                _ => Space4XShipFitUiTone.Warning
            };
        }

        private static FixedString128Bytes ResolveShipFitMessage(Space4XShipFitResultCode code)
        {
            return code switch
            {
                Space4XShipFitResultCode.Success => new FixedString128Bytes("Loadout updated."),
                Space4XShipFitResultCode.NoHeldItem => new FixedString128Bytes("No item is currently held."),
                Space4XShipFitResultCode.EmptySource => new FixedString128Bytes("Nothing to pick up from that slot."),
                Space4XShipFitResultCode.InvalidTarget => new FixedString128Bytes("Invalid target for this item."),
                Space4XShipFitResultCode.ItemTypeMismatch => new FixedString128Bytes("Held item type does not match target."),
                Space4XShipFitResultCode.SlotSizeMismatch => new FixedString128Bytes("Module size does not match socket."),
                Space4XShipFitResultCode.MountTypeMismatch => new FixedString128Bytes("Module mount type does not match socket."),
                Space4XShipFitResultCode.SegmentAssemblyInvalid => new FixedString128Bytes("Segment change would invalidate hull assembly."),
                Space4XShipFitResultCode.ModuleSpecMissing => new FixedString128Bytes("Module catalog entry missing for equipped module."),
                _ => default
            };
        }
    }
}

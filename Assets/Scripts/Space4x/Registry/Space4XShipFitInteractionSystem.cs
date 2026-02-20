using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XInventoryProjectionSystem))]
    public partial struct Space4XShipFitInteractionSystem : ISystem
    {
        private const int MaxResultEvents = 32;

        private BufferLookup<CarrierModuleSlot> _moduleSlotLookup;
        private BufferLookup<CarrierHullSegment> _segmentLookup;
        private BufferLookup<Space4XModuleInventoryEntry> _moduleInventoryLookup;
        private BufferLookup<Space4XSegmentInventoryEntry> _segmentInventoryLookup;
        private BufferLookup<Space4XCarrierModuleSocketLayout> _socketLayoutLookup;
        private BufferLookup<Space4XShipFitRequest> _requestLookup;
        private BufferLookup<Space4XShipFitResultEvent> _resultEventLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerFlagshipTag>();
            _moduleSlotLookup = state.GetBufferLookup<CarrierModuleSlot>(false);
            _segmentLookup = state.GetBufferLookup<CarrierHullSegment>(false);
            _moduleInventoryLookup = state.GetBufferLookup<Space4XModuleInventoryEntry>(false);
            _segmentInventoryLookup = state.GetBufferLookup<Space4XSegmentInventoryEntry>(false);
            _socketLayoutLookup = state.GetBufferLookup<Space4XCarrierModuleSocketLayout>(false);
            _requestLookup = state.GetBufferLookup<Space4XShipFitRequest>(false);
            _resultEventLookup = state.GetBufferLookup<Space4XShipFitResultEvent>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            _moduleSlotLookup.Update(ref state);
            _segmentLookup.Update(ref state);
            _moduleInventoryLookup.Update(ref state);
            _segmentInventoryLookup.Update(ref state);
            _socketLayoutLookup.Update(ref state);
            _requestLookup.Update(ref state);
            _resultEventLookup.Update(ref state);

            EnsureFitComponents(ref state);

            _moduleSlotLookup.Update(ref state);
            _segmentLookup.Update(ref state);
            _moduleInventoryLookup.Update(ref state);
            _segmentInventoryLookup.Update(ref state);
            _socketLayoutLookup.Update(ref state);
            _requestLookup.Update(ref state);
            _resultEventLookup.Update(ref state);

            var em = state.EntityManager;
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlayerFlagshipTag>>().WithEntityAccess())
            {
                if (!_socketLayoutLookup.HasBuffer(entity))
                {
                    continue;
                }

                var layout = _socketLayoutLookup[entity];
                RebuildSocketLayout(em, entity, layout);

                if (!_requestLookup.HasBuffer(entity) || !_resultEventLookup.HasBuffer(entity))
                {
                    continue;
                }

                var cursor = em.GetComponentData<Space4XShipFitCursorState>(entity);
                var lastResult = em.GetComponentData<Space4XShipFitLastResult>(entity);
                var requests = _requestLookup[entity];
                var events = _resultEventLookup[entity];
                var moduleInventory = _moduleInventoryLookup[entity];
                var segmentInventory = _segmentInventoryLookup[entity];
                var slots = _moduleSlotLookup.HasBuffer(entity) ? _moduleSlotLookup[entity] : default;
                var segments = _segmentLookup.HasBuffer(entity) ? _segmentLookup[entity] : default;

                for (var i = 0; i < requests.Length; i++)
                {
                    var request = requests[i];
                    var code = ProcessRequest(
                        em,
                        entity,
                        request,
                        ref cursor,
                        ref moduleInventory,
                        slots,
                        ref segmentInventory,
                        segments,
                        layout);
                    PublishResult(ref lastResult, ref events, request, code);
                }

                requests.Clear();
                em.SetComponentData(entity, cursor);
                em.SetComponentData(entity, lastResult);
            }
        }

        private void EnsureFitComponents(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var em = state.EntityManager;
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PlayerFlagshipTag>>().WithEntityAccess())
            {
                if (!em.HasComponent<Space4XShipFitCursorState>(entity))
                {
                    ecb.AddComponent(entity, Space4XShipFitCursorState.Empty);
                }

                if (!em.HasComponent<Space4XShipFitLastResult>(entity))
                {
                    ecb.AddComponent(entity, new Space4XShipFitLastResult
                    {
                        Revision = 0u,
                        RequestType = Space4XShipFitRequestType.LeftClick,
                        TargetKind = Space4XShipFitTargetKind.None,
                        TargetIndex = -1,
                        Code = Space4XShipFitResultCode.None
                    });
                }

                if (!em.HasBuffer<Space4XModuleInventoryEntry>(entity)) ecb.AddBuffer<Space4XModuleInventoryEntry>(entity);
                if (!em.HasBuffer<Space4XSegmentInventoryEntry>(entity)) ecb.AddBuffer<Space4XSegmentInventoryEntry>(entity);
                if (!em.HasBuffer<Space4XCarrierModuleSocketLayout>(entity)) ecb.AddBuffer<Space4XCarrierModuleSocketLayout>(entity);
                if (!em.HasBuffer<Space4XShipFitRequest>(entity)) ecb.AddBuffer<Space4XShipFitRequest>(entity);
                if (!em.HasBuffer<Space4XShipFitResultEvent>(entity)) ecb.AddBuffer<Space4XShipFitResultEvent>(entity);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void PublishResult(
            ref Space4XShipFitLastResult lastResult,
            ref DynamicBuffer<Space4XShipFitResultEvent> events,
            in Space4XShipFitRequest request,
            Space4XShipFitResultCode code)
        {
            var revision = lastResult.Revision + 1u;
            lastResult = new Space4XShipFitLastResult
            {
                Revision = revision,
                RequestType = request.RequestType,
                TargetKind = request.TargetKind,
                TargetIndex = request.TargetIndex,
                Code = code
            };
            events.Add(new Space4XShipFitResultEvent
            {
                Revision = revision,
                RequestType = request.RequestType,
                TargetKind = request.TargetKind,
                TargetIndex = request.TargetIndex,
                Code = code
            });
            while (events.Length > MaxResultEvents) events.RemoveAt(0);
        }

        private Space4XShipFitResultCode ProcessRequest(
            EntityManager em,
            Entity carrier,
            in Space4XShipFitRequest request,
            ref Space4XShipFitCursorState cursor,
            ref DynamicBuffer<Space4XModuleInventoryEntry> moduleInventory,
            DynamicBuffer<CarrierModuleSlot> slots,
            ref DynamicBuffer<Space4XSegmentInventoryEntry> segmentInventory,
            DynamicBuffer<CarrierHullSegment> segments,
            DynamicBuffer<Space4XCarrierModuleSocketLayout> layout)
        {
            if (request.RequestType == Space4XShipFitRequestType.CancelHeld)
            {
                return CancelHeld(em, carrier, ref cursor, ref moduleInventory, slots, ref segmentInventory, segments, layout);
            }

            if (request.RequestType != Space4XShipFitRequestType.LeftClick) return Space4XShipFitResultCode.InvalidTarget;

            switch (request.TargetKind)
            {
                case Space4XShipFitTargetKind.ModuleInventory:
                    return HandleModuleInventoryClick(ref cursor, ref moduleInventory, request.TargetIndex);
                case Space4XShipFitTargetKind.ModuleSocket:
                    return slots.IsCreated ? HandleModuleSocketClick(em, carrier, ref cursor, slots, layout, request.TargetIndex) : Space4XShipFitResultCode.InvalidTarget;
                case Space4XShipFitTargetKind.SegmentInventory:
                    return HandleSegmentInventoryClick(ref cursor, ref segmentInventory, request.TargetIndex);
                case Space4XShipFitTargetKind.SegmentSocket:
                    return segments.IsCreated ? HandleSegmentSocketClick(em, carrier, ref cursor, segments, request.TargetIndex) : Space4XShipFitResultCode.InvalidTarget;
                default:
                    return Space4XShipFitResultCode.InvalidTarget;
            }
        }

        private void RebuildSocketLayout(EntityManager em, Entity entity, DynamicBuffer<Space4XCarrierModuleSocketLayout> layout)
        {
            layout.Clear();
            if (!_moduleSlotLookup.HasBuffer(entity)) return;

            var slots = _moduleSlotLookup[entity];
            var hasSegments = _segmentLookup.HasBuffer(entity);
            var segments = hasSegments ? _segmentLookup[entity] : default;
            var hostProfile = Space4XModuleCompatibilityUtility.ResolveHostProfile(em, entity);

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var segmentIndex = hasSegments && segments.Length > 0 ? segments[i % segments.Length].SegmentIndex : (byte)0;
                byte segmentSocketIndex = 0;
                for (var j = 0; j < layout.Length; j++) if (layout[j].SegmentIndex == segmentIndex) segmentSocketIndex++;
                var mount = MountType.Utility;
                if (Space4XModuleCompatibilityUtility.TryResolveSlotMountType(em, entity, slot.SlotIndex, in hostProfile, out var resolvedMount))
                    mount = resolvedMount;

                layout.Add(new Space4XCarrierModuleSocketLayout
                {
                    SlotIndex = slot.SlotIndex,
                    SegmentIndex = segmentIndex,
                    SegmentSocketIndex = segmentSocketIndex,
                    MountType = mount
                });
            }
        }

        private static bool HasHeld(in Space4XShipFitCursorState cursor) => cursor.HeldKind != Space4XShipFitItemKind.None;
        private static void ClearCursor(ref Space4XShipFitCursorState cursor) => cursor = Space4XShipFitCursorState.Empty;

        private static Space4XShipFitResultCode HandleModuleInventoryClick(ref Space4XShipFitCursorState cursor, ref DynamicBuffer<Space4XModuleInventoryEntry> inventory, int index)
        {
            if (!HasHeld(cursor))
            {
                if (index < 0 || index >= inventory.Length) return Space4XShipFitResultCode.InvalidTarget;
                var module = inventory[index].Module;
                if (module == Entity.Null) return Space4XShipFitResultCode.EmptySource;
                inventory.RemoveAt(index);
                cursor = new Space4XShipFitCursorState { HeldKind = Space4XShipFitItemKind.Module, HeldModule = module, OriginKind = Space4XShipFitTargetKind.ModuleInventory, OriginIndex = index };
                return Space4XShipFitResultCode.Success;
            }

            if (cursor.HeldKind != Space4XShipFitItemKind.Module || cursor.HeldModule == Entity.Null) return Space4XShipFitResultCode.ItemTypeMismatch;
            if (index < 0 || index >= inventory.Length)
            {
                inventory.Add(new Space4XModuleInventoryEntry { Module = cursor.HeldModule });
                ClearCursor(ref cursor);
                return Space4XShipFitResultCode.Success;
            }

            var swap = inventory[index].Module;
            inventory[index] = new Space4XModuleInventoryEntry { Module = cursor.HeldModule };
            if (swap == Entity.Null) { ClearCursor(ref cursor); return Space4XShipFitResultCode.Success; }
            cursor.HeldModule = swap;
            return Space4XShipFitResultCode.Success;
        }

        private Space4XShipFitResultCode HandleModuleSocketClick(
            EntityManager em,
            Entity carrier,
            ref Space4XShipFitCursorState cursor,
            DynamicBuffer<CarrierModuleSlot> slots,
            DynamicBuffer<Space4XCarrierModuleSocketLayout> layout,
            int slotIndex)
        {
            if (!TryFindModuleSlotByIndex(slots, slotIndex, out var i)) return Space4XShipFitResultCode.InvalidTarget;
            var slot = slots[i];

            if (!HasHeld(cursor))
            {
                if (slot.CurrentModule == Entity.Null) return Space4XShipFitResultCode.EmptySource;
                var picked = slot.CurrentModule;
                slot.CurrentModule = Entity.Null;
                slot.TargetModule = Entity.Null;
                slot.RefitProgress = 0f;
                slot.State = ModuleSlotState.Empty;
                slots[i] = slot;
                cursor = new Space4XShipFitCursorState { HeldKind = Space4XShipFitItemKind.Module, HeldModule = picked, OriginKind = Space4XShipFitTargetKind.ModuleSocket, OriginIndex = slotIndex };
                return Space4XShipFitResultCode.Success;
            }

            if (cursor.HeldKind != Space4XShipFitItemKind.Module || cursor.HeldModule == Entity.Null) return Space4XShipFitResultCode.ItemTypeMismatch;
            var compat = ValidateModuleCompatibility(em, carrier, cursor.HeldModule, slot, layout);
            if (compat != Space4XShipFitResultCode.Success) return compat;

            var swap = slot.CurrentModule;
            slot.CurrentModule = cursor.HeldModule;
            slot.TargetModule = cursor.HeldModule;
            slot.RefitProgress = 0f;
            slot.State = ModuleSlotState.Active;
            slots[i] = slot;
            if (swap == Entity.Null) { ClearCursor(ref cursor); return Space4XShipFitResultCode.Success; }
            cursor.HeldModule = swap;
            return Space4XShipFitResultCode.Success;
        }

        private static Space4XShipFitResultCode ValidateModuleCompatibility(
            EntityManager em,
            Entity host,
            Entity module,
            in CarrierModuleSlot slot,
            DynamicBuffer<Space4XCarrierModuleSocketLayout> layout)
        {
            var compatibility = Space4XModuleCompatibilityUtility.ValidateModuleForSlot(em, host, module, slot, layout);
            return compatibility switch
            {
                Space4XModuleCompatibilityCode.Success => Space4XShipFitResultCode.Success,
                Space4XModuleCompatibilityCode.SlotSizeMismatch => Space4XShipFitResultCode.SlotSizeMismatch,
                Space4XModuleCompatibilityCode.MountTypeMismatch => Space4XShipFitResultCode.MountTypeMismatch,
                Space4XModuleCompatibilityCode.ModuleSpecMissing => Space4XShipFitResultCode.ModuleSpecMissing,
                _ => Space4XShipFitResultCode.InvalidTarget
            };
        }

        private static Space4XShipFitResultCode HandleSegmentInventoryClick(ref Space4XShipFitCursorState cursor, ref DynamicBuffer<Space4XSegmentInventoryEntry> inventory, int index)
        {
            if (!HasHeld(cursor))
            {
                if (index < 0 || index >= inventory.Length) return Space4XShipFitResultCode.InvalidTarget;
                var id = inventory[index].SegmentId;
                if (id.Length == 0) return Space4XShipFitResultCode.EmptySource;
                inventory.RemoveAt(index);
                cursor = new Space4XShipFitCursorState { HeldKind = Space4XShipFitItemKind.Segment, HeldSegmentId = id, OriginKind = Space4XShipFitTargetKind.SegmentInventory, OriginIndex = index };
                return Space4XShipFitResultCode.Success;
            }

            if (cursor.HeldKind != Space4XShipFitItemKind.Segment || cursor.HeldSegmentId.Length == 0) return Space4XShipFitResultCode.ItemTypeMismatch;
            if (index < 0 || index >= inventory.Length)
            {
                inventory.Add(new Space4XSegmentInventoryEntry { SegmentId = cursor.HeldSegmentId });
                ClearCursor(ref cursor);
                return Space4XShipFitResultCode.Success;
            }

            var swap = inventory[index].SegmentId;
            inventory[index] = new Space4XSegmentInventoryEntry { SegmentId = cursor.HeldSegmentId };
            if (swap.Length == 0) { ClearCursor(ref cursor); return Space4XShipFitResultCode.Success; }
            cursor.HeldSegmentId = swap;
            return Space4XShipFitResultCode.Success;
        }

        private Space4XShipFitResultCode HandleSegmentSocketClick(EntityManager em, Entity carrier, ref Space4XShipFitCursorState cursor, DynamicBuffer<CarrierHullSegment> segments, int targetIndex)
        {
            if (!TryFindSegmentSlotByIndex(segments, targetIndex, out var i)) return Space4XShipFitResultCode.InvalidTarget;
            var slot = segments[i];
            if (!HasHeld(cursor))
            {
                if (slot.SegmentId.Length == 0) return Space4XShipFitResultCode.EmptySource;
                var picked = slot.SegmentId;
                slot.SegmentId = default;
                segments[i] = slot;
                cursor = new Space4XShipFitCursorState { HeldKind = Space4XShipFitItemKind.Segment, HeldSegmentId = picked, OriginKind = Space4XShipFitTargetKind.SegmentSocket, OriginIndex = targetIndex };
                return Space4XShipFitResultCode.Success;
            }

            if (cursor.HeldKind != Space4XShipFitItemKind.Segment || cursor.HeldSegmentId.Length == 0) return Space4XShipFitResultCode.ItemTypeMismatch;
            var swap = slot.SegmentId;
            slot.SegmentId = cursor.HeldSegmentId;
            segments[i] = slot;
            if (!ValidateSegments(em, carrier, segments))
            {
                slot.SegmentId = swap;
                segments[i] = slot;
                return Space4XShipFitResultCode.SegmentAssemblyInvalid;
            }
            if (swap.Length == 0) { ClearCursor(ref cursor); return Space4XShipFitResultCode.Success; }
            cursor.HeldSegmentId = swap;
            return Space4XShipFitResultCode.Success;
        }

        private Space4XShipFitResultCode CancelHeld(EntityManager em, Entity carrier, ref Space4XShipFitCursorState cursor, ref DynamicBuffer<Space4XModuleInventoryEntry> moduleInventory, DynamicBuffer<CarrierModuleSlot> slots, ref DynamicBuffer<Space4XSegmentInventoryEntry> segmentInventory, DynamicBuffer<CarrierHullSegment> segments, DynamicBuffer<Space4XCarrierModuleSocketLayout> layout)
        {
            if (!HasHeld(cursor)) return Space4XShipFitResultCode.NoHeldItem;

            if (cursor.HeldKind == Space4XShipFitItemKind.Module && cursor.HeldModule != Entity.Null)
            {
                if (cursor.OriginKind == Space4XShipFitTargetKind.ModuleSocket && slots.IsCreated && TryFindModuleSlotByIndex(slots, cursor.OriginIndex, out var i))
                {
                    var slot = slots[i];
                    if (slot.CurrentModule == Entity.Null && ValidateModuleCompatibility(em, carrier, cursor.HeldModule, slot, layout) == Space4XShipFitResultCode.Success)
                    {
                        slot.CurrentModule = cursor.HeldModule;
                        slot.TargetModule = cursor.HeldModule;
                        slot.State = ModuleSlotState.Active;
                        slot.RefitProgress = 0f;
                        slots[i] = slot;
                        ClearCursor(ref cursor);
                        return Space4XShipFitResultCode.Success;
                    }
                }

                InsertModule(ref moduleInventory, cursor.OriginKind == Space4XShipFitTargetKind.ModuleInventory ? cursor.OriginIndex : moduleInventory.Length, cursor.HeldModule);
                ClearCursor(ref cursor);
                return Space4XShipFitResultCode.Success;
            }

            if (cursor.HeldKind == Space4XShipFitItemKind.Segment && cursor.HeldSegmentId.Length > 0)
            {
                if (cursor.OriginKind == Space4XShipFitTargetKind.SegmentSocket && segments.IsCreated && TryFindSegmentSlotByIndex(segments, cursor.OriginIndex, out var i))
                {
                    var slot = segments[i];
                    if (slot.SegmentId.Length == 0)
                    {
                        slot.SegmentId = cursor.HeldSegmentId;
                        segments[i] = slot;
                        if (ValidateSegments(em, carrier, segments))
                        {
                            ClearCursor(ref cursor);
                            return Space4XShipFitResultCode.Success;
                        }

                        slot.SegmentId = default;
                        segments[i] = slot;
                    }
                }

                InsertSegment(ref segmentInventory, cursor.OriginKind == Space4XShipFitTargetKind.SegmentInventory ? cursor.OriginIndex : segmentInventory.Length, cursor.HeldSegmentId);
                ClearCursor(ref cursor);
                return Space4XShipFitResultCode.Success;
            }

            ClearCursor(ref cursor);
            return Space4XShipFitResultCode.NoHeldItem;
        }

        private bool ValidateSegments(EntityManager em, Entity carrier, DynamicBuffer<CarrierHullSegment> segments)
        {
            var hostProfile = Space4XModuleCompatibilityUtility.ResolveHostProfile(em, carrier);
            if (hostProfile.ValidateSegments == 0) return true;
            if (!Space4XModuleCompatibilityUtility.TryResolveHullId(em, carrier, in hostProfile, out var hullId)) return true;
            return ModuleCatalogUtility.TryValidateHullSegmentAssembly(em, hullId, segments, out _);
        }

        private static bool TryFindModuleSlotByIndex(DynamicBuffer<CarrierModuleSlot> slots, int slotIndex, out int i)
        {
            for (i = 0; i < slots.Length; i++) if (slots[i].SlotIndex == slotIndex) return true;
            if (slotIndex >= 0 && slotIndex < slots.Length) { i = slotIndex; return true; }
            i = -1;
            return false;
        }

        private static bool TryFindSegmentSlotByIndex(DynamicBuffer<CarrierHullSegment> slots, int index, out int i)
        {
            for (i = 0; i < slots.Length; i++) if (slots[i].SegmentIndex == (byte)index) return true;
            if (index >= 0 && index < slots.Length) { i = index; return true; }
            i = -1;
            return false;
        }

        private static void InsertModule(ref DynamicBuffer<Space4XModuleInventoryEntry> buffer, int index, Entity module)
        {
            var clamped = index < 0 || index > buffer.Length ? buffer.Length : index;
            if (clamped == buffer.Length) buffer.Add(new Space4XModuleInventoryEntry { Module = module });
            else buffer.Insert(clamped, new Space4XModuleInventoryEntry { Module = module });
        }

        private static void InsertSegment(ref DynamicBuffer<Space4XSegmentInventoryEntry> buffer, int index, FixedString64Bytes segmentId)
        {
            var clamped = index < 0 || index > buffer.Length ? buffer.Length : index;
            if (clamped == buffer.Length) buffer.Add(new Space4XSegmentInventoryEntry { SegmentId = segmentId });
            else buffer.Insert(clamped, new Space4XSegmentInventoryEntry { SegmentId = segmentId });
        }
    }
}

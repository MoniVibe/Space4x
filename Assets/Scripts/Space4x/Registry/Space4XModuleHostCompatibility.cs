using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public enum Space4XModuleHostKind : byte
    {
        None = 0,
        Ship = 1,
        Station = 2,
        // Reserved for future gameplay. Currently behaves like a ship host profile.
        Titan = 3
    }

    public struct Space4XModuleHostProfile : IComponentData
    {
        public Space4XModuleHostKind Kind;
        public FixedString64Bytes HostId;
        public byte UsesHullSlots;
        public byte ValidateMountType;
        public byte ValidateSegments;

        public static Space4XModuleHostProfile Ship(in FixedString64Bytes hullId)
        {
            return new Space4XModuleHostProfile
            {
                Kind = Space4XModuleHostKind.Ship,
                HostId = hullId,
                UsesHullSlots = 1,
                ValidateMountType = 1,
                ValidateSegments = 1
            };
        }

        public static Space4XModuleHostProfile Station(in FixedString64Bytes stationId, bool usesHullSlots, in FixedString64Bytes hullId)
        {
            return new Space4XModuleHostProfile
            {
                Kind = Space4XModuleHostKind.Station,
                HostId = usesHullSlots ? hullId : stationId,
                UsesHullSlots = usesHullSlots ? (byte)1 : (byte)0,
                ValidateMountType = usesHullSlots ? (byte)1 : (byte)0,
                ValidateSegments = usesHullSlots ? (byte)1 : (byte)0
            };
        }

        public static Space4XModuleHostProfile Titan(in FixedString64Bytes hullId)
        {
            // Titan is a dormant concept for now: same validation semantics as ships.
            var profile = Ship(hullId);
            profile.Kind = Space4XModuleHostKind.Titan;
            return profile;
        }
    }

    public enum Space4XModuleCompatibilityCode : byte
    {
        Success = 0,
        SlotSizeMismatch = 1,
        MountTypeMismatch = 2,
        ModuleSpecMissing = 3
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XShipFitInteractionSystem))]
    public partial struct Space4XModuleHostProfileBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CarrierModuleSlot>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (stationId, entity) in SystemAPI.Query<RefRO<StationId>>()
                         .WithAll<CarrierModuleSlot>()
                         .WithNone<Space4XModuleHostProfile>()
                         .WithEntityAccess())
            {
                var hasHull = Space4XModuleCompatibilityUtility.TryResolveHullId(em, entity, out var hullId);
                var profile = Space4XModuleHostProfile.Station(stationId.ValueRO.Id, hasHull, hullId);
                ecb.AddComponent(entity, profile);
            }

            foreach (var (carrierHullId, entity) in SystemAPI.Query<RefRO<CarrierHullId>>()
                         .WithAll<CarrierModuleSlot>()
                         .WithNone<Space4XModuleHostProfile, StationId>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, Space4XModuleHostProfile.Ship(carrierHullId.ValueRO.HullId));
            }

            foreach (var (hullId, entity) in SystemAPI.Query<RefRO<HullId>>()
                         .WithAll<CarrierModuleSlot>()
                         .WithNone<Space4XModuleHostProfile, StationId, CarrierHullId>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, Space4XModuleHostProfile.Ship(hullId.ValueRO.Id));
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }

    public static class Space4XModuleCompatibilityUtility
    {
        public static Space4XModuleHostProfile ResolveHostProfile(EntityManager entityManager, Entity host)
        {
            if (host == Entity.Null || !entityManager.Exists(host))
            {
                return default;
            }

            if (entityManager.HasComponent<Space4XModuleHostProfile>(host))
            {
                return entityManager.GetComponentData<Space4XModuleHostProfile>(host);
            }

            if (entityManager.HasComponent<StationId>(host))
            {
                var stationId = entityManager.GetComponentData<StationId>(host).Id;
                var hasHull = TryResolveHullId(entityManager, host, out var hullId);
                return Space4XModuleHostProfile.Station(stationId, hasHull, hullId);
            }

            if (TryResolveHullId(entityManager, host, out var shipHullId))
            {
                return Space4XModuleHostProfile.Ship(shipHullId);
            }

            return default;
        }

        public static bool TryResolveHullId(EntityManager entityManager, Entity host, out FixedString64Bytes hullId)
        {
            hullId = default;
            if (host == Entity.Null || !entityManager.Exists(host))
            {
                return false;
            }

            if (entityManager.HasComponent<Space4XModuleHostProfile>(host))
            {
                var profile = entityManager.GetComponentData<Space4XModuleHostProfile>(host);
                if (TryResolveHullId(entityManager, host, in profile, out hullId))
                {
                    return true;
                }
            }

            if (entityManager.HasComponent<CarrierHullId>(host))
            {
                hullId = entityManager.GetComponentData<CarrierHullId>(host).HullId;
                return hullId.Length > 0;
            }

            if (entityManager.HasComponent<HullId>(host))
            {
                hullId = entityManager.GetComponentData<HullId>(host).Id;
                return hullId.Length > 0;
            }

            return false;
        }

        public static bool TryResolveHullId(EntityManager entityManager, Entity host, in Space4XModuleHostProfile profile, out FixedString64Bytes hullId)
        {
            hullId = default;
            if (host == Entity.Null || !entityManager.Exists(host))
            {
                return false;
            }

            if (profile.UsesHullSlots != 0 && profile.HostId.Length > 0)
            {
                hullId = profile.HostId;
                return true;
            }

            // Titan currently uses the same hull-id resolution contract as ships.
            if ((profile.Kind == Space4XModuleHostKind.Ship || profile.Kind == Space4XModuleHostKind.Titan) && profile.HostId.Length > 0)
            {
                hullId = profile.HostId;
                return true;
            }

            if (entityManager.HasComponent<CarrierHullId>(host))
            {
                hullId = entityManager.GetComponentData<CarrierHullId>(host).HullId;
                return hullId.Length > 0;
            }

            if (entityManager.HasComponent<HullId>(host))
            {
                hullId = entityManager.GetComponentData<HullId>(host).Id;
                return hullId.Length > 0;
            }

            return false;
        }

        public static bool TryResolveSlotMountType(EntityManager entityManager, Entity host, int slotIndex, in Space4XModuleHostProfile profile, out MountType mountType)
        {
            mountType = MountType.Utility;
            if (slotIndex < 0)
            {
                return false;
            }

            if (!TryResolveHullId(entityManager, host, in profile, out var hullId))
            {
                return false;
            }

            if (!ModuleCatalogUtility.TryGetHullSpec(entityManager, hullId, out var hullCatalogRef, out var hullIndex))
            {
                return false;
            }

            ref var hullSlots = ref hullCatalogRef.Value.Hulls[hullIndex].Slots;
            if (slotIndex >= hullSlots.Length)
            {
                return false;
            }

            mountType = hullSlots[slotIndex].Type;
            return true;
        }

        public static Space4XModuleCompatibilityCode ValidateModuleForSlot(
            EntityManager entityManager,
            Entity host,
            Entity module,
            in CarrierModuleSlot slot,
            DynamicBuffer<Space4XCarrierModuleSocketLayout> layout)
        {
            if (module == Entity.Null || !entityManager.Exists(module))
            {
                return Space4XModuleCompatibilityCode.ModuleSpecMissing;
            }

            if (entityManager.HasComponent<ModuleSlotRequirement>(module) &&
                entityManager.GetComponentData<ModuleSlotRequirement>(module).SlotSize != slot.SlotSize)
            {
                return Space4XModuleCompatibilityCode.SlotSizeMismatch;
            }

            if (!entityManager.HasComponent<ModuleTypeId>(module))
            {
                return Space4XModuleCompatibilityCode.Success;
            }

            var moduleTypeId = entityManager.GetComponentData<ModuleTypeId>(module).Value;
            if (!ModuleCatalogUtility.TryGetModuleSpec(entityManager, moduleTypeId, out var spec))
            {
                return Space4XModuleCompatibilityCode.ModuleSpecMissing;
            }

            if (ConvertMountSize(spec.RequiredSize) != slot.SlotSize)
            {
                return Space4XModuleCompatibilityCode.SlotSizeMismatch;
            }

            var hostProfile = ResolveHostProfile(entityManager, host);
            if (hostProfile.ValidateMountType == 0)
            {
                return Space4XModuleCompatibilityCode.Success;
            }

            if (TryGetSocketLayout(layout, slot.SlotIndex, out var layoutEntry))
            {
                return layoutEntry.MountType == spec.RequiredMount
                    ? Space4XModuleCompatibilityCode.Success
                    : Space4XModuleCompatibilityCode.MountTypeMismatch;
            }

            if (TryResolveSlotMountType(entityManager, host, slot.SlotIndex, in hostProfile, out var mountType))
            {
                return mountType == spec.RequiredMount
                    ? Space4XModuleCompatibilityCode.Success
                    : Space4XModuleCompatibilityCode.MountTypeMismatch;
            }

            return Space4XModuleCompatibilityCode.Success;
        }

        private static bool TryGetSocketLayout(DynamicBuffer<Space4XCarrierModuleSocketLayout> layout, int slotIndex, out Space4XCarrierModuleSocketLayout entry)
        {
            if (layout.IsCreated)
            {
                for (var i = 0; i < layout.Length; i++)
                {
                    if (layout[i].SlotIndex == slotIndex)
                    {
                        entry = layout[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        private static ModuleSlotSize ConvertMountSize(MountSize size)
        {
            return size switch
            {
                MountSize.S => ModuleSlotSize.Small,
                MountSize.M => ModuleSlotSize.Medium,
                MountSize.L => ModuleSlotSize.Large,
                _ => ModuleSlotSize.Medium
            };
        }
    }
}

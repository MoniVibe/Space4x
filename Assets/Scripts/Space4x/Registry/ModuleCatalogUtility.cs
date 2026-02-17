using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public static class ModuleCatalogUtility
    {
        public static bool TryGetModuleSpec(in ModuleCatalogSingleton catalogSingleton, in FixedString64Bytes moduleId, out ModuleSpec spec)
        {
            spec = default;
            if (!catalogSingleton.Catalog.IsCreated)
            {
                return false;
            }

            ref var modules = ref catalogSingleton.Catalog.Value.Modules;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].Id == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetModuleSpec(EntityQuery query, in FixedString64Bytes moduleId, out ModuleSpec spec)
        {
            spec = default;
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var catalogSingleton = query.GetSingleton<ModuleCatalogSingleton>();
            return TryGetModuleSpec(in catalogSingleton, moduleId, out spec);
        }

        public static bool TryGetTuning(EntityQuery query, out RefitRepairTuning tuning)
        {
            tuning = default;
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var tuningSingleton = query.GetSingleton<RefitRepairTuningSingleton>();
            if (!tuningSingleton.Tuning.IsCreated)
            {
                return false;
            }

            tuning = tuningSingleton.Tuning.Value;
            return true;
        }

        [BurstDiscard]
        public static bool TryGetModuleSpec(ref SystemState state, in FixedString64Bytes moduleId, out ModuleSpec spec)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<ModuleCatalogSingleton>());
            return TryGetModuleSpec(query, moduleId, out spec);
        }

        public static bool TryGetModuleSpec(EntityManager entityManager, in FixedString64Bytes moduleId, out ModuleSpec spec)
        {
            spec = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ModuleCatalogSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var catalog = query.GetSingleton<ModuleCatalogSingleton>();
            if (!catalog.Catalog.IsCreated)
            {
                return false;
            }

            ref var modules = ref catalog.Catalog.Value.Modules;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i].Id == moduleId)
                {
                    spec = modules[i];
                    return true;
                }
            }

            return false;
        }

        [BurstDiscard]
        public static bool TryGetHullSpec(ref SystemState state, in FixedString64Bytes hullId, out BlobAssetReference<HullCatalogBlob> catalogRef, out int hullIndex)
        {
            catalogRef = default;
            hullIndex = -1;
            var query = state.GetEntityQuery(ComponentType.ReadOnly<HullCatalogSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var catalog = query.GetSingleton<HullCatalogSingleton>();
            if (!catalog.Catalog.IsCreated)
            {
                return false;
            }

            ref var hulls = ref catalog.Catalog.Value.Hulls;
            for (int i = 0; i < hulls.Length; i++)
            {
                if (hulls[i].Id == hullId)
                {
                    catalogRef = catalog.Catalog;
                    hullIndex = i;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetHullSpec(EntityManager entityManager, in FixedString64Bytes hullId, out BlobAssetReference<HullCatalogBlob> catalogRef, out int hullIndex)
        {
            catalogRef = default;
            hullIndex = -1;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<HullCatalogSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var catalog = query.GetSingleton<HullCatalogSingleton>();
            if (!catalog.Catalog.IsCreated)
            {
                return false;
            }

            ref var hulls = ref catalog.Catalog.Value.Hulls;
            for (int i = 0; i < hulls.Length; i++)
            {
                if (hulls[i].Id == hullId)
                {
                    catalogRef = catalog.Catalog;
                    hullIndex = i;
                    return true;
                }
            }

            return false;
        }

        [BurstDiscard]
        public static bool TryGetTuning(ref SystemState state, out RefitRepairTuning tuning)
        {
            tuning = default;
            var query = state.GetEntityQuery(ComponentType.ReadOnly<RefitRepairTuningSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var tuningSingleton = query.GetSingleton<RefitRepairTuningSingleton>();
            if (!tuningSingleton.Tuning.IsCreated)
            {
                return false;
            }

            tuning = tuningSingleton.Tuning.Value;
            return true;
        }

        public static bool TryGetTuning(EntityManager entityManager, out RefitRepairTuning tuning)
        {
            tuning = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RefitRepairTuningSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var tuningSingleton = query.GetSingleton<RefitRepairTuningSingleton>();
            if (!tuningSingleton.Tuning.IsCreated)
            {
                return false;
            }

            tuning = tuningSingleton.Tuning.Value;
            return true;
        }

        public static float GetSizeMultiplier(RefitRepairTuning tuning, MountSize size)
        {
            return size switch
            {
                MountSize.S => tuning.SizeMultS,
                MountSize.M => tuning.SizeMultM,
                MountSize.L => tuning.SizeMultL,
                _ => 1f
            };
        }

        public static float CalculateRefitTime(
            RefitRepairTuning tuning,
            ModuleSpec module,
            bool inFacility,
            bool changedTypeOrSize)
        {
            var sizeMult = GetSizeMultiplier(tuning, module.RequiredSize);
            var locationMult = inFacility ? tuning.StationTimeMult : tuning.FieldTimeMult;
            var baseTime = tuning.BaseRefitSeconds;
            var massTime = tuning.MassSecPerTon * module.MassTons * sizeMult * locationMult;
            var rewireTime = changedTypeOrSize ? tuning.RewirePenaltySeconds : 0f;
            return baseTime + massTime + rewireTime;
        }

        [BurstDiscard]
        public static bool CanPerformFieldRefit(ref SystemState state, in FixedString64Bytes hullId, bool hasInFacilityTag)
        {
            if (hasInFacilityTag)
            {
                return true;
            }

            if (!TryGetTuning(ref state, out var tuning))
            {
                return false;
            }

            if (!tuning.GlobalFieldRefitEnabled)
            {
                return false;
            }

            if (!TryGetHullSpec(ref state, hullId, out var catalogRef, out var hullIndex))
            {
                return false;
            }

            ref var hulls = ref catalogRef.Value.Hulls;
            ref var hullSpec = ref hulls[hullIndex];
            return hullSpec.FieldRefitAllowed;
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public static class ModuleCatalogUtility
    {
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
            spec = default;
            var query = state.GetEntityQuery(ComponentType.ReadOnly<ModuleCatalogSingleton>());
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
        public static bool TryGetHullSegmentSpec(ref SystemState state, in FixedString64Bytes segmentId, out HullSegmentSpec spec)
        {
            spec = default;
            var query = state.GetEntityQuery(ComponentType.ReadOnly<HullSegmentCatalogSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var catalog = query.GetSingleton<HullSegmentCatalogSingleton>();
            if (!catalog.Catalog.IsCreated)
            {
                return false;
            }

            ref var segments = ref catalog.Catalog.Value.Segments;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Id == segmentId)
                {
                    spec = segments[i];
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetHullSegmentSpec(EntityManager entityManager, in FixedString64Bytes segmentId, out HullSegmentSpec spec)
        {
            spec = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<HullSegmentCatalogSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var catalog = query.GetSingleton<HullSegmentCatalogSingleton>();
            if (!catalog.Catalog.IsCreated)
            {
                return false;
            }

            ref var segments = ref catalog.Catalog.Value.Segments;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Id == segmentId)
                {
                    spec = segments[i];
                    return true;
                }
            }

            return false;
        }

        [BurstDiscard]
        public static bool TryGetStationSpec(ref SystemState state, in FixedString64Bytes stationId, out StationSpec spec)
        {
            spec = default;
            var query = state.GetEntityQuery(ComponentType.ReadOnly<StationCatalogSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var catalog = query.GetSingleton<StationCatalogSingleton>();
            if (!catalog.Catalog.IsCreated)
            {
                return false;
            }

            ref var stations = ref catalog.Catalog.Value.Stations;
            for (int i = 0; i < stations.Length; i++)
            {
                if (stations[i].Id == stationId)
                {
                    spec = stations[i];
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetStationSpec(EntityManager entityManager, in FixedString64Bytes stationId, out StationSpec spec)
        {
            spec = default;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<StationCatalogSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var catalog = query.GetSingleton<StationCatalogSingleton>();
            if (!catalog.Catalog.IsCreated)
            {
                return false;
            }

            ref var stations = ref catalog.Catalog.Value.Stations;
            for (int i = 0; i < stations.Length; i++)
            {
                if (stations[i].Id == stationId)
                {
                    spec = stations[i];
                    return true;
                }
            }

            return false;
        }

        [BurstDiscard]
        public static bool TryValidateHullSegmentAssembly(
            ref SystemState state,
            in FixedString64Bytes hullId,
            DynamicBuffer<CarrierHullSegment> segments,
            out HullSegmentValidationError error)
        {
            error = HullSegmentValidationError.None;
            if (!TryGetHullSpec(ref state, hullId, out var hullCatalogRef, out var hullIndex))
            {
                error = HullSegmentValidationError.HullSpecMissing;
                return false;
            }

            return TryValidateHullSegmentAssembly(hullCatalogRef, hullIndex, segments, ref state, out error);
        }

        public static bool TryValidateHullSegmentAssembly(
            EntityManager entityManager,
            in FixedString64Bytes hullId,
            DynamicBuffer<CarrierHullSegment> segments,
            out HullSegmentValidationError error)
        {
            error = HullSegmentValidationError.None;
            if (!TryGetHullSpec(entityManager, hullId, out var hullCatalogRef, out var hullIndex))
            {
                error = HullSegmentValidationError.HullSpecMissing;
                return false;
            }

            return TryValidateHullSegmentAssembly(hullCatalogRef, hullIndex, segments, entityManager, out error);
        }

        [BurstDiscard]
        private static bool TryValidateHullSegmentAssembly(
            BlobAssetReference<HullCatalogBlob> hullCatalogRef,
            int hullIndex,
            DynamicBuffer<CarrierHullSegment> segments,
            ref SystemState state,
            out HullSegmentValidationError error)
        {
            error = HullSegmentValidationError.None;
            ref var hullSpec = ref hullCatalogRef.Value.Hulls[hullIndex];

            if (!ValidateSegmentCount(ref hullSpec, segments.Length))
            {
                error = HullSegmentValidationError.SegmentCountOutOfRange;
                return false;
            }

            for (int i = 0; i < segments.Length; i++)
            {
                var segmentId = segments[i].SegmentId;
                if (!TryGetHullSegmentSpec(ref state, segmentId, out var segmentSpec))
                {
                    error = HullSegmentValidationError.SegmentSpecMissing;
                    return false;
                }

                if (!IsSegmentFamilyAllowed(ref hullSpec, segmentSpec))
                {
                    error = HullSegmentValidationError.SegmentFamilyNotAllowed;
                    return false;
                }
            }

            ref var roleRules = ref hullSpec.RequiredSegmentRoles;
            for (int ruleIndex = 0; ruleIndex < roleRules.Length; ruleIndex++)
            {
                var rule = roleRules[ruleIndex];
                int roleCount = CountRoleMatches(ref state, segments, rule.Role);
                if (roleCount < rule.MinCount)
                {
                    error = HullSegmentValidationError.RequiredRoleMissing;
                    return false;
                }

                int maxCount = rule.MaxCount == 0 ? int.MaxValue : rule.MaxCount;
                if (roleCount > maxCount)
                {
                    error = HullSegmentValidationError.RequiredRoleExceeded;
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidateHullSegmentAssembly(
            BlobAssetReference<HullCatalogBlob> hullCatalogRef,
            int hullIndex,
            DynamicBuffer<CarrierHullSegment> segments,
            EntityManager entityManager,
            out HullSegmentValidationError error)
        {
            error = HullSegmentValidationError.None;
            ref var hullSpec = ref hullCatalogRef.Value.Hulls[hullIndex];

            if (!ValidateSegmentCount(ref hullSpec, segments.Length))
            {
                error = HullSegmentValidationError.SegmentCountOutOfRange;
                return false;
            }

            for (int i = 0; i < segments.Length; i++)
            {
                var segmentId = segments[i].SegmentId;
                if (!TryGetHullSegmentSpec(entityManager, segmentId, out var segmentSpec))
                {
                    error = HullSegmentValidationError.SegmentSpecMissing;
                    return false;
                }

                if (!IsSegmentFamilyAllowed(ref hullSpec, segmentSpec))
                {
                    error = HullSegmentValidationError.SegmentFamilyNotAllowed;
                    return false;
                }
            }

            ref var roleRules = ref hullSpec.RequiredSegmentRoles;
            for (int ruleIndex = 0; ruleIndex < roleRules.Length; ruleIndex++)
            {
                var rule = roleRules[ruleIndex];
                int roleCount = CountRoleMatches(entityManager, segments, rule.Role);
                if (roleCount < rule.MinCount)
                {
                    error = HullSegmentValidationError.RequiredRoleMissing;
                    return false;
                }

                int maxCount = rule.MaxCount == 0 ? int.MaxValue : rule.MaxCount;
                if (roleCount > maxCount)
                {
                    error = HullSegmentValidationError.RequiredRoleExceeded;
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateSegmentCount(ref HullSpec hullSpec, int count)
        {
            int minCount = hullSpec.MinSegmentCount;
            int maxCount = hullSpec.MaxSegmentCount == 0 ? int.MaxValue : hullSpec.MaxSegmentCount;
            return count >= minCount && count <= maxCount;
        }

        private static bool IsSegmentFamilyAllowed(ref HullSpec hullSpec, in HullSegmentSpec segmentSpec)
        {
            if (segmentSpec.IsGeneralPurpose != 0)
            {
                return true;
            }

            if (hullSpec.AllowedSegmentFamilies.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < hullSpec.AllowedSegmentFamilies.Length; i++)
            {
                if (hullSpec.AllowedSegmentFamilies[i] == segmentSpec.FamilyId)
                {
                    return true;
                }
            }

            return false;
        }

        [BurstDiscard]
        private static int CountRoleMatches(ref SystemState state, DynamicBuffer<CarrierHullSegment> segments, HullSegmentRole role)
        {
            int count = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                if (!TryGetHullSegmentSpec(ref state, segments[i].SegmentId, out var segmentSpec))
                {
                    continue;
                }

                if (segmentSpec.Role == role)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountRoleMatches(EntityManager entityManager, DynamicBuffer<CarrierHullSegment> segments, HullSegmentRole role)
        {
            int count = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                if (!TryGetHullSegmentSpec(entityManager, segments[i].SegmentId, out var segmentSpec))
                {
                    continue;
                }

                if (segmentSpec.Role == role)
                {
                    count++;
                }
            }

            return count;
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

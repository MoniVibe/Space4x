using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Shelter
{
    /// <summary>
    /// Static helpers for shelter and occlusion calculations.
    /// </summary>
    [BurstCompile]
    public static class ShelterHelpers
    {
        /// <summary>
        /// Finds nearest shelter from a hazard direction.
        /// </summary>
        public static bool FindNearestShelter(
            float3 seekerPosition,
            float3 hazardDirection,
            in DynamicBuffer<ShelterEntry> shelters,
            float maxDistance,
            out Entity shelterEntity,
            out float3 bestPosition,
            out float shelterLevel)
        {
            shelterEntity = Entity.Null;
            bestPosition = seekerPosition;
            shelterLevel = 0;

            float bestScore = -1;
            float3 normalizedHazard = math.normalizesafe(hazardDirection);

            for (int i = 0; i < shelters.Length; i++)
            {
                var entry = shelters[i];
                if (entry.IsActive == 0)
                    continue;

                float distance = math.length(entry.Position - seekerPosition);
                if (distance > maxDistance)
                    continue;

                // Calculate how well this shelter blocks the hazard
                float3 toShelter = math.normalizesafe(entry.Position - seekerPosition);
                float alignmentToHazard = math.dot(toShelter, normalizedHazard);

                // Good shelter is between us and the hazard
                if (alignmentToHazard < 0.3f)
                    continue;

                // Score based on distance and alignment
                float distanceScore = 1f - (distance / maxDistance);
                float alignmentScore = alignmentToHazard;
                float qualityScore = entry.ShelterQuality;
                float totalScore = distanceScore * 0.3f + alignmentScore * 0.4f + qualityScore * 0.3f;

                if (totalScore > bestScore)
                {
                    bestScore = totalScore;
                    shelterEntity = entry.Entity;
                    // Position just behind the shelter from hazard direction
                    bestPosition = entry.Position - normalizedHazard * (entry.CoverageRadius * 0.5f);
                    shelterLevel = entry.ShelterQuality;
                }
            }

            return shelterEntity != Entity.Null;
        }

        /// <summary>
        /// Calculates shelter factor at a position from a hazard direction.
        /// </summary>
        public static float CalculateShelterFactor(
            float3 position,
            float3 hazardDirection,
            in ShelterProvider provider,
            float3 providerPosition)
        {
            float3 toProvider = providerPosition - position;
            float distance = math.length(toProvider);

            if (distance > provider.CoverageRadius)
                return 0;

            float3 toProviderNorm = toProvider / math.max(0.001f, distance);
            float3 hazardNorm = math.normalizesafe(hazardDirection);

            // Provider must be between us and hazard
            float alignment = math.dot(toProviderNorm, hazardNorm);
            if (alignment < 0)
                return 0;

            // Check if within angular coverage
            if (provider.ProvidesFull360 == 0)
            {
                float3 coverDir = math.normalizesafe(provider.CoverageDirection);
                float coverAngle = math.acos(math.clamp(math.dot(-hazardNorm, coverDir), -1f, 1f));
                if (coverAngle > provider.CoverageAngle * 0.5f)
                    return 0;
            }

            // Distance falloff
            float distanceFactor = 1f - (distance / provider.CoverageRadius);

            // Height check for overhead protection
            float heightFactor = 1f;
            if (provider.ProvidesOverhead != 0)
            {
                heightFactor = math.saturate(provider.CoverageHeight / 10f);
            }

            return alignment * distanceFactor * provider.Opacity * heightFactor;
        }

        /// <summary>
        /// Checks if line of sight is blocked between two points.
        /// </summary>
        public static bool IsInShadow(
            float3 targetPosition,
            float3 lightPosition,
            in OcclusionData occluder,
            float3 occluderPosition,
            out float shadowFactor)
        {
            shadowFactor = 1f;

            float3 toLight = lightPosition - targetPosition;
            float lightDist = math.length(toLight);

            if (lightDist < 0.001f)
                return false;

            float3 lightDir = toLight / lightDist;
            float3 toOccluder = occluderPosition - targetPosition;

            // Project occluder onto light ray
            float projection = math.dot(toOccluder, lightDir);

            // Occluder must be between target and light
            if (projection <= 0 || projection >= lightDist)
                return false;

            // Closest point on ray to occluder
            float3 closestPoint = targetPosition + lightDir * projection;
            float distFromRay = math.length(occluderPosition - closestPoint);

            // Check against occluder radius
            float occluderRadius = occluder.Radius > 0 ? occluder.Radius : 
                math.max(occluder.Size.x, math.max(occluder.Size.y, occluder.Size.z)) * 0.5f;

            if (distFromRay >= occluderRadius)
                return false;

            // Calculate shadow factor based on how centered we are in shadow
            float shadowDepth = 1f - (distFromRay / occluderRadius);
            shadowFactor = 1f - shadowDepth;

            return true;
        }

        /// <summary>
        /// Gets best shelter position around a provider.
        /// </summary>
        public static float3 GetBestShelterPosition(
            float3 seekerPosition,
            float3 hazardDirection,
            float3 providerPosition,
            float coverageRadius)
        {
            float3 hazardNorm = math.normalizesafe(hazardDirection);

            // Best position is on opposite side from hazard
            float3 idealOffset = -hazardNorm * (coverageRadius * 0.7f);
            float3 idealPosition = providerPosition + idealOffset;

            // Ensure we're not too far from current position
            float3 toIdeal = idealPosition - seekerPosition;
            float distToIdeal = math.length(toIdeal);

            if (distToIdeal > coverageRadius * 2f)
            {
                // Clamp to reasonable distance
                idealPosition = seekerPosition + (toIdeal / distToIdeal) * coverageRadius * 2f;
            }

            return idealPosition;
        }

        /// <summary>
        /// Analyzes cover from multiple directions.
        /// </summary>
        public static void AnalyzeDirectionalCover(
            float3 position,
            in DynamicBuffer<ShelterEntry> shelters,
            ref DynamicBuffer<DirectionalCover> directionalCover)
        {
            directionalCover.Clear();

            // Check 8 cardinal/ordinal directions
            float3[] directions = new float3[]
            {
                new float3(1, 0, 0),
                new float3(-1, 0, 0),
                new float3(0, 0, 1),
                new float3(0, 0, -1),
                math.normalize(new float3(1, 0, 1)),
                math.normalize(new float3(-1, 0, 1)),
                math.normalize(new float3(1, 0, -1)),
                math.normalize(new float3(-1, 0, -1))
            };

            for (int d = 0; d < 8; d++)
            {
                float3 dir = directions[d];
                float bestCover = 0;
                Entity bestProvider = Entity.Null;
                float bestDistance = float.MaxValue;

                for (int i = 0; i < shelters.Length; i++)
                {
                    var entry = shelters[i];
                    if (entry.IsActive == 0)
                        continue;

                    float3 toShelter = entry.Position - position;
                    float distance = math.length(toShelter);

                    if (distance > entry.CoverageRadius * 2f)
                        continue;

                    float alignment = math.dot(math.normalizesafe(toShelter), dir);
                    if (alignment < 0.5f)
                        continue;

                    float coverLevel = alignment * entry.ShelterQuality * 
                        (1f - distance / (entry.CoverageRadius * 2f));

                    if (coverLevel > bestCover)
                    {
                        bestCover = coverLevel;
                        bestProvider = entry.Entity;
                        bestDistance = distance;
                    }
                }

                directionalCover.Add(new DirectionalCover
                {
                    Direction = dir,
                    CoverLevel = bestCover,
                    NearestProvider = bestProvider,
                    Distance = bestDistance < float.MaxValue ? bestDistance : 0
                });
            }
        }

        /// <summary>
        /// Samples occlusion map at position.
        /// </summary>
        public static float SampleOcclusionMap(
            in OcclusionMap map,
            in DynamicBuffer<OcclusionCell> cells,
            float3 position)
        {
            if (cells.Length == 0)
                return 0;

            float3 local = position - map.WorldMin;
            int2 cell = new int2(
                (int)(local.x / map.CellSize),
                (int)(local.z / map.CellSize));

            cell = math.clamp(cell, int2.zero, map.Resolution - 1);
            int index = cell.y * map.Resolution.x + cell.x;

            if (index < 0 || index >= cells.Length)
                return 0;

            return cells[index].ShelterLevel;
        }

        /// <summary>
        /// Checks if position has overhead cover.
        /// </summary>
        public static bool HasOverheadCover(
            in OcclusionMap map,
            in DynamicBuffer<OcclusionCell> cells,
            float3 position)
        {
            if (cells.Length == 0)
                return false;

            float3 local = position - map.WorldMin;
            int2 cell = new int2(
                (int)(local.x / map.CellSize),
                (int)(local.z / map.CellSize));

            cell = math.clamp(cell, int2.zero, map.Resolution - 1);
            int index = cell.y * map.Resolution.x + cell.x;

            if (index < 0 || index >= cells.Length)
                return false;

            return cells[index].OverheadCover > 0.5f;
        }

        /// <summary>
        /// Updates shelter state for an entity.
        /// </summary>
        public static void UpdateShelterState(
            ref ShelterState state,
            float3 position,
            float3 hazardDirection,
            in DynamicBuffer<ShelterEntry> shelters,
            uint currentTick)
        {
            float bestLevel = 0;
            Entity bestProvider = Entity.Null;
            float3 bestDirection = float3.zero;

            for (int i = 0; i < shelters.Length; i++)
            {
                var entry = shelters[i];
                if (entry.IsActive == 0)
                    continue;

                float distance = math.length(entry.Position - position);
                if (distance > entry.CoverageRadius)
                    continue;

                float3 toProvider = math.normalizesafe(entry.Position - position);
                float alignment = math.dot(toProvider, math.normalizesafe(hazardDirection));

                if (alignment > 0.3f)
                {
                    float level = entry.ShelterQuality * (1f - distance / entry.CoverageRadius) * alignment;
                    if (level > bestLevel)
                    {
                        bestLevel = level;
                        bestProvider = entry.Entity;
                        bestDirection = toProvider;
                    }
                }
            }

            if (bestProvider != state.ShelterProvider)
            {
                state.EnteredTick = currentTick;
            }

            state.ShelterProvider = bestProvider;
            state.ShelterLevel = bestLevel;
            state.ShelterDirection = bestDirection;
            state.IsFullyCovered = (byte)(bestLevel > 0.9f ? 1 : 0);
            state.IsPartialCover = (byte)(bestLevel > 0.3f && bestLevel <= 0.9f ? 1 : 0);
        }

        /// <summary>
        /// Gets shelter quality for occluder type.
        /// </summary>
        public static float GetDefaultShelterQuality(OccluderType type)
        {
            return type switch
            {
                OccluderType.Structure => 0.95f,
                OccluderType.Shield => 0.9f,
                OccluderType.Asteroid => 0.85f,
                OccluderType.Terrain => 0.8f,
                OccluderType.Vehicle => 0.75f,
                OccluderType.Artificial => 0.7f,
                OccluderType.Vegetation => 0.5f,
                _ => 0.5f
            };
        }
    }
}


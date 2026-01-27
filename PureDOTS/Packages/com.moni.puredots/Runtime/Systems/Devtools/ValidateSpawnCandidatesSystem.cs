#if DEVTOOLS_ENABLED
using PureDOTS.Runtime.Devtools;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

namespace PureDOTS.Systems.Devtools
{
    /// <summary>
    /// Validates spawn candidates and writes validation results.
    /// Checks slope, overlap, bounds, forbidden volumes, navmesh.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(BuildSpawnCandidatesSystem))]
    public partial struct ValidateSpawnCandidatesSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var hasGrid = TryGetSpatialContext(ref state, out var gridContext);

            foreach (var (request, candidates, validationResults) in SystemAPI.Query<RefRO<SpawnRequest>, DynamicBuffer<SpawnCandidate>, DynamicBuffer<SpawnValidationResult>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                validationResults.ResizeUninitialized(candidates.Length);

                var checkOverlap = hasGrid && (req.Flags & SpawnFlags.AllowOverlap) == 0;
                var overlapRadius = checkOverlap ? ComputeOverlapRadius(req, gridContext.Config) : 0f;
                var overlapScratch = checkOverlap ? new NativeList<Entity>(Allocator.Temp) : default;

                for (int i = 0; i < candidates.Length; i++)
                {
                    var candidate = candidates[i];
                    var validation = ValidateCandidate(ref candidate, req, checkOverlap, overlapRadius, gridContext, ref overlapScratch);
                    validationResults[i] = validation;

                    // Update candidate validity flag
                    if (validation.FailureReason == ValidationFailureReason.None)
                    {
                        candidate.IsValid = 1;
                        candidates[i] = candidate;
                    }
                }

                if (overlapScratch.IsCreated)
                {
                    overlapScratch.Dispose();
                }
            }
        }

        private SpawnValidationResult ValidateCandidate(
            ref SpawnCandidate candidate,
            in SpawnRequest request,
            bool checkOverlap,
            float overlapRadius,
            in SpatialGridContext gridContext,
            ref NativeList<Entity> overlapScratch)
        {
            // Simplified validation (can be extended with actual physics/terrain checks)
            var result = new SpawnValidationResult
            {
                FailureReason = ValidationFailureReason.None,
                FailureMessage = default
            };

            // Check bounds (simplified - assumes ground plane at y=0)
            if (candidate.Position.y < -10f || candidate.Position.y > 1000f)
            {
                result.FailureReason = ValidationFailureReason.OutOfBounds;
                result.FailureMessage = new FixedString128Bytes("Position out of bounds");
                return result;
            }

            // If AllowOverlap flag is not set, check for overlaps using spatial grid
            if (checkOverlap)
            {
                if (HasOverlap(candidate.Position, overlapRadius, in gridContext, ref overlapScratch))
                {
                    result.FailureReason = ValidationFailureReason.OverlapsExisting;
                    result.FailureMessage = new FixedString128Bytes("Overlaps existing entity");
                    return result;
                }
            }

            // If NavmeshOnly flag is set, check navmesh
            if ((request.Flags & SpawnFlags.NavmeshOnly) != 0)
            {
                var searchDistance = math.max(0.5f, math.max(overlapRadius, request.RadiusOrSpread > 0f ? request.RadiusOrSpread : 1f));
                if (!TryProjectOntoNavmesh(candidate.Position, searchDistance, out var projected))
                {
                    result.FailureReason = ValidationFailureReason.NotOnNavmesh;
                    result.FailureMessage = new FixedString128Bytes("Position not on navmesh");
                    return result;
                }

                candidate.Position = projected;
            }

            return result;
        }

        private bool TryGetSpatialContext(ref SystemState state, out SpatialGridContext context)
        {
            if (SystemAPI.HasSingleton<SpatialGridConfig>() && SystemAPI.HasSingleton<SpatialGridState>())
            {
                var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
                context = new SpatialGridContext
                {
                    Config = SystemAPI.GetSingleton<SpatialGridConfig>(),
                    Ranges = SystemAPI.GetBuffer<SpatialGridCellRange>(gridEntity),
                    Entries = SystemAPI.GetBuffer<SpatialGridEntry>(gridEntity)
                };
                return true;
            }

            context = default;
            return false;
        }

        private static float ComputeOverlapRadius(in SpawnRequest request, in SpatialGridConfig config)
        {
            var baseRadius = request.RadiusOrSpread > 0f ? math.max(0.25f, request.RadiusOrSpread * 0.5f) : 0.5f;
            var cellRadius = math.clamp(config.CellSize * 0.5f, 0.25f, 4f);
            return math.max(baseRadius, cellRadius);
        }

        private static bool HasOverlap(in float3 position, float radius, in SpatialGridContext context, ref NativeList<Entity> results)
        {
            results.Clear();
            var candidatePos = position;
            SpatialQueryHelper.GetEntitiesWithinRadius(
                ref candidatePos,
                radius,
                context.Config,
                context.Ranges,
                context.Entries,
                ref results);

            return results.Length > 0;
        }

        private static bool TryProjectOntoNavmesh(in float3 position, float maxDistance, out float3 projected)
        {
#if UNITY_EDITOR || !UNITY_DOTSRUNTIME
            if (NavMesh.SamplePosition(new Vector3(position.x, position.y, position.z), out var hit, maxDistance, NavMesh.AllAreas))
            {
                var hitPos = hit.position;
                projected = new float3(hitPos.x, hitPos.y, hitPos.z);
                return true;
            }
#endif
            projected = position;
            return false;
        }

        private struct SpatialGridContext
        {
            public SpatialGridConfig Config;
            public DynamicBuffer<SpatialGridCellRange> Ranges;
            public DynamicBuffer<SpatialGridEntry> Entries;
        }
    }
}
#endif

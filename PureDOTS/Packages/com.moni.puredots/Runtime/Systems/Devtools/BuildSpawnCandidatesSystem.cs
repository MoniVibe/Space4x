#if DEVTOOLS_ENABLED
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Devtools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Devtools
{
    /// <summary>
    /// Expands SpawnRequest entities into candidate poses (no instantiation).
    /// Runs after CopyInputToEcsSystem to read cursor hit cache.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Input.CopyInputToEcsSystem))]
    public partial struct BuildSpawnCandidatesSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get cursor hit cache if AtCursor flag is set
            float3 cursorPosition = float3.zero;
            bool hasCursorHit = false;
            using (var cursorQuery = SystemAPI.QueryBuilder().WithAll<CursorHitCache>().Build())
            {
                if (!cursorQuery.IsEmptyIgnoreFilter)
                {
                    var cursorHit = SystemAPI.GetSingleton<CursorHitCache>();
                    if (cursorHit.HasHit)
                    {
                        cursorPosition = cursorHit.HitPoint;
                        hasCursorHit = true;
                    }
                }
            }

            foreach (var (request, candidates, entity) in SystemAPI.Query<RefRO<SpawnRequest>, DynamicBuffer<SpawnCandidate>>().WithEntityAccess())
            {
                candidates.Clear();

                var req = request.ValueRO;
                float3 basePosition = req.Position;
                quaternion baseRotation = req.Rotation;

                // If AtCursor flag is set, use cursor position
                if ((req.Flags & SpawnFlags.AtCursor) != 0 && hasCursorHit)
                {
                    basePosition = cursorPosition;
                }

                // Generate candidate poses based on pattern
                var random = new Unity.Mathematics.Random(req.Seed);
                GenerateCandidates(ref candidates, req, basePosition, baseRotation, ref random);
            }
        }

        private void GenerateCandidates(DynamicBuffer<SpawnCandidate> candidates, in SpawnRequest request, float3 basePosition, quaternion baseRotation, ref Unity.Mathematics.Random random)
        {
            switch (request.Pattern)
            {
                case SpawnPattern.Point:
                    GeneratePointPattern(candidates, request, basePosition, baseRotation);
                    break;
                case SpawnPattern.Line:
                    GenerateLinePattern(candidates, request, basePosition, baseRotation, ref random);
                    break;
                case SpawnPattern.Circle:
                    GenerateCirclePattern(candidates, request, basePosition, baseRotation, ref random);
                    break;
                case SpawnPattern.Grid:
                    GenerateGridPattern(candidates, request, basePosition, baseRotation);
                    break;
                case SpawnPattern.Scatter:
                    GenerateScatterPattern(candidates, request, basePosition, baseRotation, ref random);
                    break;
            }
        }

        private void GeneratePointPattern(DynamicBuffer<SpawnCandidate> candidates, in SpawnRequest request, float3 basePosition, quaternion baseRotation)
        {
            for (int i = 0; i < request.Count; i++)
            {
                candidates.Add(new SpawnCandidate
                {
                    Position = basePosition,
                    Rotation = baseRotation,
                    PrototypeId = request.PrototypeId,
                    IsValid = 0
                });
            }
        }

        private void GenerateLinePattern(DynamicBuffer<SpawnCandidate> candidates, in SpawnRequest request, float3 basePosition, quaternion baseRotation, ref Unity.Mathematics.Random random)
        {
            float step = request.RadiusOrSpread / math.max(1, request.Count - 1);
            float3 forward = math.mul(baseRotation, new float3(0, 0, 1));
            float3 right = math.mul(baseRotation, new float3(1, 0, 0));

            for (int i = 0; i < request.Count; i++)
            {
                float offset = (i - (request.Count - 1) * 0.5f) * step;
                float3 pos = basePosition + right * offset;
                candidates.Add(new SpawnCandidate
                {
                    Position = pos,
                    Rotation = baseRotation,
                    PrototypeId = request.PrototypeId,
                    IsValid = 0
                });
            }
        }

        private void GenerateCirclePattern(DynamicBuffer<SpawnCandidate> candidates, in SpawnRequest request, float3 basePosition, quaternion baseRotation, ref Unity.Mathematics.Random random)
        {
            // Circle pattern in local XZ plane, transformed by baseRotation for 3D-aware spawning
            float angleStep = (2f * math.PI) / request.Count;
            for (int i = 0; i < request.Count; i++)
            {
                float angle = i * angleStep;
                // Local offset in XZ plane (Y=0 in local space)
                float3 localOffset = new float3(math.cos(angle), 0f, math.sin(angle)) * request.RadiusOrSpread;
                // Transform by base rotation for proper 3D orientation
                float3 worldOffset = math.mul(baseRotation, localOffset);
                float3 pos = basePosition + worldOffset;
                candidates.Add(new SpawnCandidate
                {
                    Position = pos,
                    Rotation = baseRotation,
                    PrototypeId = request.PrototypeId,
                    IsValid = 0
                });
            }
        }

        private void GenerateGridPattern(DynamicBuffer<SpawnCandidate> candidates, in SpawnRequest request, float3 basePosition, quaternion baseRotation)
        {
            int cols = request.Columns > 0 ? request.Columns : (int)math.ceil(math.sqrt(request.Count));
            int rows = (int)math.ceil((float)request.Count / cols);
            float spacing = request.RadiusOrSpread;

            float3 forward = math.mul(baseRotation, new float3(0, 0, 1));
            float3 right = math.mul(baseRotation, new float3(1, 0, 0));

            int spawned = 0;
            for (int row = 0; row < rows && spawned < request.Count; row++)
            {
                for (int col = 0; col < cols && spawned < request.Count; col++)
                {
                    float3 offset = (row - (rows - 1) * 0.5f) * spacing * forward + (col - (cols - 1) * 0.5f) * spacing * right;
                    candidates.Add(new SpawnCandidate
                    {
                        Position = basePosition + offset,
                        Rotation = baseRotation,
                        PrototypeId = request.PrototypeId,
                        IsValid = 0
                    });
                    spawned++;
                }
            }
        }

        private void GenerateScatterPattern(DynamicBuffer<SpawnCandidate> candidates, in SpawnRequest request, float3 basePosition, quaternion baseRotation, ref Unity.Mathematics.Random random)
        {
            // Blue-noise-ish scatter within radius, transformed by baseRotation for 3D-aware spawning
            for (int i = 0; i < request.Count; i++)
            {
                float angle = random.NextFloat() * 2f * math.PI;
                float distance = random.NextFloat() * request.RadiusOrSpread;
                // Local offset in XZ plane (Y=0 in local space)
                float3 localOffset = new float3(math.cos(angle), 0f, math.sin(angle)) * distance;
                // Transform by base rotation for proper 3D orientation
                float3 worldOffset = math.mul(baseRotation, localOffset);
                candidates.Add(new SpawnCandidate
                {
                    Position = basePosition + worldOffset,
                    Rotation = baseRotation,
                    PrototypeId = request.PrototypeId,
                    IsValid = 0
                });
            }
        }
    }
}
#endif
























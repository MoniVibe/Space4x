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
    /// Processes AggregateSpawnRequest by expanding members and creating group entity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(BuildSpawnCandidatesSystem))]
    public partial struct ProcessAggregateSpawnSystem : ISystem
    {
        private EndFixedStepSimulationEntityCommandBufferSystem.Singleton _ecbSingleton;

        public void OnCreate(ref SystemState state)
        {
            _ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<AggregateSpawnRequest>();
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

            var ecb = _ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (request, entity) in SystemAPI.Query<RefRO<AggregateSpawnRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;

                // Find preset blob (simplified - would need registry lookup)
                // For now, get first preset blob found
                BlobAssetReference<AggregatePresetBlob> presetBlob = default;
                using (var presetQuery = SystemAPI.QueryBuilder().WithAll<AggregatePresetBlobReference>().Build())
                {
                    if (presetQuery.IsEmptyIgnoreFilter)
                    {
                        continue;
                    }
                    // Get first preset blob (TODO: Match by preset ID)
                    var presetEntity = presetQuery.GetSingletonEntity();
                    var presetRef = SystemAPI.GetComponent<AggregatePresetBlobReference>(presetEntity);
                    presetBlob = presetRef.Blob;
                }

                if (!presetBlob.IsCreated)
                {
                    continue;
                }

                float3 basePosition = req.Position;
                if ((req.Flags & SpawnFlags.AtCursor) != 0 && hasCursorHit)
                {
                    basePosition = cursorPosition;
                }

                // Create group entity
                var groupEntity = ecb.CreateEntity();
                ecb.AddComponent(groupEntity, new AggregateGroup
                {
                    AggregatePresetId = req.AggregatePresetId,
                    OwnerPlayerId = req.OwnerPlayerId
                });
                ecb.AddComponent(groupEntity, new Formation
                {
                    Type = presetBlob.Value.FormationType,
                    Spacing = presetBlob.Value.FormationSpacing
                });
                var membersBuffer = ecb.AddBuffer<AggregateMembers>(groupEntity);

                // Expand members
                var random = new Unity.Mathematics.Random(req.Seed);
                var preset = presetBlob.Value;
                int totalSpawned = 0;
                int targetCount = req.TotalCount > 0 ? req.TotalCount : 0;

                for (int i = 0; i < preset.Members.Length; i++)
                {
                    var member = preset.Members[i];
                    int count = targetCount > 0 
                        ? math.min(member.MaxCount, targetCount - totalSpawned)
                        : random.NextInt(member.MinCount, member.MaxCount + 1);

                    // Generate spawn positions for this member type
                    for (int j = 0; j < count; j++)
                    {
                        // Get prefab
                        if (!SystemAPI.HasSingleton<PrototypeRegistryBlob>())
                        {
                            continue;
                        }
                        var registry = SystemAPI.GetSingleton<PrototypeRegistryBlob>();
                        if (!PrototypeLookup.TryGetPrefab(registry.Entries, member.PrototypeId, out var prefab))
                        {
                            continue;
                        }

                        // Calculate position based on formation
                        float3 position = CalculateFormationPosition(basePosition, preset.FormationType, preset.FormationSpacing, totalSpawned, ref random);

                        // Instantiate member
                        var memberEntity = ecb.Instantiate(prefab);
                        ecb.SetComponent(memberEntity, LocalTransform.FromPositionRotation(position, req.Rotation));

                        // Apply overrides
                        if (member.StatsOverrides.Health > 0 || member.StatsOverrides.Speed > 0)
                        {
                            ecb.AddComponent(memberEntity, member.StatsOverrides);
                        }
                        ecb.AddComponent(memberEntity, member.AlignmentOverride);
                        ecb.AddComponent(memberEntity, member.OutlookOverride);

                        // Add to group
                        membersBuffer.Add(new AggregateMembers { Member = memberEntity });
                        totalSpawned++;
                    }
                }

                // Cleanup request
                ecb.DestroyEntity(entity);
            }
        }

        private float3 CalculateFormationPosition(float3 basePosition, FormationType formationType, float spacing, int index, ref Unity.Mathematics.Random random)
        {
            // All formation offsets are computed in local XZ plane, preserving basePosition.y
            // This allows spawning at any Y level (ground, flying, space)
            switch (formationType)
            {
                case FormationType.Point:
                    return basePosition;
                case FormationType.Circle:
                    float angle = (index * 2f * math.PI) / math.max(1, index + 1);
                    float radius = spacing * math.sqrt(index);
                    // Preserve Y from basePosition for 3D-aware spawning
                    return basePosition + new float3(math.cos(angle) * radius, 0f, math.sin(angle) * radius);
                case FormationType.Grid:
                    int cols = (int)math.ceil(math.sqrt(index + 1));
                    int row = index / cols;
                    int col = index % cols;
                    // Preserve Y from basePosition for 3D-aware spawning
                    return basePosition + new float3((col - cols * 0.5f) * spacing, 0f, (row - cols * 0.5f) * spacing);
                case FormationType.Line:
                    // Preserve Y from basePosition for 3D-aware spawning
                    return basePosition + new float3((index - (index + 1) * 0.5f) * spacing, 0f, 0f);
                default:
                    return basePosition;
            }
        }
    }
}
#endif


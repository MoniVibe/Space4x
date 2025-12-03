using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Resolves target entities for vessels into explicit world positions.
    /// Similar to VillagerTargetingSystem but designed for vessels.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    [UpdateAfter(typeof(VesselAISystem))]
    public partial struct VesselTargetingSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<ResourceRegistryEntry> _resourceEntriesLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private EntityQuery _resourceRegistryQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _resourceEntriesLookup = state.GetBufferLookup<ResourceRegistryEntry>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);

            _resourceRegistryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRegistry, ResourceRegistryEntry>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _statsLookup.Update(ref state);

            // Initialize with empty array to ensure it's always valid (Burst requirement)
            NativeArray<ResourceRegistryEntry> resourceEntries = new NativeArray<ResourceRegistryEntry>(0, Allocator.TempJob);
            bool disposeResourceEntries = true;
            bool hasResourceEntries = false;

            _resourceEntriesLookup.Update(ref state);

            if (!_resourceRegistryQuery.IsEmptyIgnoreFilter)
            {
                var resourceEntity = _resourceRegistryQuery.GetSingletonEntity();
                if (_resourceEntriesLookup.TryGetBuffer(resourceEntity, out var resourceBuffer) && resourceBuffer.Length > 0)
                {
                    hasResourceEntries = true;
                    // Dispose the empty array and use the buffer's view (buffer owns the memory)
                    resourceEntries.Dispose();
                    resourceEntries = resourceBuffer.AsNativeArray();
                    disposeResourceEntries = false; // Don't dispose - buffer owns it
                }
                else
                {
                    hasResourceEntries = false;
                }
            }

            // Create and schedule job - ResourceEntries is guaranteed valid
            var job = new ResolveVesselTargetPositionsJob
            {
                TransformLookup = _transformLookup,
                StatsLookup = _statsLookup,
                ResourceEntries = resourceEntries,
                HasResourceEntries = hasResourceEntries
            };

            var jobHandle = job.ScheduleParallel(state.Dependency);

            // Dispose array after job completes (only if we created empty one)
            // Note: Arrays from AsNativeArray() are views owned by buffers, don't dispose those
            if (disposeResourceEntries)
            {
                state.Dependency = resourceEntries.Dispose(jobHandle);
            }
            else
            {
                state.Dependency = jobHandle;
            }
        }

        [BurstCompile]
        public partial struct ResolveVesselTargetPositionsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<IndividualStats> StatsLookup;
            [ReadOnly] public NativeArray<ResourceRegistryEntry> ResourceEntries;
            public bool HasResourceEntries;

            public void Execute(ref VesselAIState aiState, Entity entity)
            {
                if (aiState.TargetEntity == Entity.Null)
                {
                    aiState.TargetPosition = float3.zero;
                    return;
                }

                // Try to get transform directly first (for GameObjects and carriers)
                if (TransformLookup.TryGetComponent(aiState.TargetEntity, out var targetTransform))
                {
                    var targetPos = targetTransform.Position;
                    
                    // Tactics stat improves targeting accuracy (reduces position error)
                    float tacticsAccuracy = 1f;
                    if (StatsLookup.HasComponent(entity))
                    {
                        var stats = StatsLookup[entity];
                        var tacticsModifier = stats.Tactics / 100f; // 0-1 normalized
                        // Higher tactics = more accurate targeting (less position drift)
                        tacticsAccuracy = 1f - (tacticsModifier * 0.1f); // Up to 10% reduction in position error
                    }
                    
                    // Apply small random offset based on tactics (lower tactics = more error)
                    var error = (1f - tacticsAccuracy) * 0.5f; // Max 0.5 unit error for low tactics
                    var hash = (uint)entity.Index ^ (uint)aiState.TargetEntity.Index;
                    var randomX = (hash % 1000) / 1000f - 0.5f;
                    var randomZ = ((hash >> 10) % 1000) / 1000f - 0.5f;
                    var offset = new float3(randomX, 0f, randomZ) * error;
                    
                    aiState.TargetPosition = targetPos + offset;
                    return;
                }

                // Try to find in resource registry (for DOTS resource entities)
                if (HasResourceEntries && RegistryEntryLookup.TryFindEntryIndex(ResourceEntries, aiState.TargetEntity, out var resourceIndex))
                {
                    aiState.TargetPosition = ResourceEntries[resourceIndex].Position;
                    return;
                }

                // If target not found and we have a stored position, use it (fallback)
                if (!aiState.TargetPosition.Equals(float3.zero))
                {
                    return; // Keep existing position
                }

                // Target not found - this shouldn't happen, but clear it to prevent infinite wait
                // Debug: Log this case - it means target entity was assigned but can't be resolved
                aiState.TargetEntity = Entity.Null;
                aiState.TargetPosition = float3.zero;
            }
        }
    }
}





























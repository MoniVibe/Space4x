using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Steering;
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
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    [UpdateAfter(typeof(VesselAISystem))]
    public partial struct VesselTargetingSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Asteroid> _asteroidLookup;
        private ComponentLookup<Space4XAsteroidVolumeConfig> _asteroidVolumeLookup;
        private ComponentLookup<MiningVessel> _miningVesselLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<VesselPhysicalProperties> _physicalLookup;
        private ComponentLookup<MiningState> _miningStateLookup;
        private BufferLookup<ResourceRegistryEntry> _resourceEntriesLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private EntityQuery _resourceRegistryQuery;

        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(true);
            _asteroidVolumeLookup = state.GetComponentLookup<Space4XAsteroidVolumeConfig>(true);
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _physicalLookup = state.GetComponentLookup<VesselPhysicalProperties>(true);
            _miningStateLookup = state.GetComponentLookup<MiningState>(true);
            _resourceEntriesLookup = state.GetBufferLookup<ResourceRegistryEntry>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);

            _resourceRegistryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRegistry, ResourceRegistryEntry>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

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
            _asteroidLookup.Update(ref state);
            _asteroidVolumeLookup.Update(ref state);
            _miningVesselLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _physicalLookup.Update(ref state);
            _miningStateLookup.Update(ref state);
            _statsLookup.Update(ref state);

            var latchConfig = Space4XMiningLatchConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XMiningLatchConfig>(out var latchConfigSingleton))
            {
                latchConfig = latchConfigSingleton;
            }

            var latchRegionCount = latchConfig.RegionCount > 0 ? latchConfig.RegionCount : Space4XMiningLatchUtility.DefaultLatchRegionCount;

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
                AsteroidLookup = _asteroidLookup,
                AsteroidVolumeLookup = _asteroidVolumeLookup,
                MiningVesselLookup = _miningVesselLookup,
                CarrierLookup = _carrierLookup,
                PhysicalLookup = _physicalLookup,
                MiningStateLookup = _miningStateLookup,
                StatsLookup = _statsLookup,
                ResourceEntries = resourceEntries,
                HasResourceEntries = hasResourceEntries,
                LatchRegionCount = latchRegionCount
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

        [WithNone(typeof(SimulationDisabledTag))]
        public partial struct ResolveVesselTargetPositionsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<Asteroid> AsteroidLookup;
            [ReadOnly] public ComponentLookup<Space4XAsteroidVolumeConfig> AsteroidVolumeLookup;
            [ReadOnly] public ComponentLookup<MiningVessel> MiningVesselLookup;
            [ReadOnly] public ComponentLookup<Carrier> CarrierLookup;
            [ReadOnly] public ComponentLookup<VesselPhysicalProperties> PhysicalLookup;
            [ReadOnly] public ComponentLookup<MiningState> MiningStateLookup;
            [ReadOnly] public ComponentLookup<IndividualStats> StatsLookup;
            [ReadOnly] public NativeArray<ResourceRegistryEntry> ResourceEntries;
            public bool HasResourceEntries;
            public int LatchRegionCount;

            public void Execute(ref VesselAIState aiState, Entity entity)
            {
                if (MiningStateLookup.HasComponent(entity))
                {
                    return;
                }

                if (aiState.TargetEntity == Entity.Null)
                {
                    aiState.TargetPosition = float3.zero;
                    return;
                }

                var hasAsteroidSurfaceTarget = false;

                // Try to get transform directly first (for GameObjects and carriers)
                if (TransformLookup.TryGetComponent(aiState.TargetEntity, out var targetTransform))
                {
                    var targetPos = targetTransform.Position;
                    if (AsteroidLookup.HasComponent(aiState.TargetEntity) &&
                        AsteroidVolumeLookup.HasComponent(aiState.TargetEntity) &&
                        TransformLookup.TryGetComponent(entity, out var selfTransform))
                    {
                        var volume = AsteroidVolumeLookup[aiState.TargetEntity];
                        var radius = math.max(0.5f, volume.Radius);
                        var regionId = Space4XMiningLatchUtility.ComputeLatchRegion(entity, aiState.TargetEntity, volume.Seed, LatchRegionCount);
                        var surfacePoint = Space4XMiningLatchUtility.ComputeSurfaceLatchPoint(targetPos, radius, regionId, volume.Seed);
                        var direction = math.normalizesafe(surfacePoint - targetPos, new float3(0f, 0f, 1f));
                        var standoff = MiningVesselLookup.HasComponent(entity) ? 1.1f : 5.5f;
                        var vesselRadius = PhysicalLookup.HasComponent(entity)
                            ? math.max(0.1f, PhysicalLookup[entity].Radius)
                            : 0.6f;
                        if (CarrierLookup.HasComponent(entity))
                        {
                            standoff = 6.5f;
                        }
                        standoff = math.max(standoff, vesselRadius + 0.25f);
                        targetPos = surfacePoint + direction * standoff;
                        hasAsteroidSurfaceTarget = true;
                    }
                    
                    // Tactics stat improves targeting accuracy (reduces position error)
                    float tacticsAccuracy = 1f;
                    if (!hasAsteroidSurfaceTarget && StatsLookup.HasComponent(entity))
                    {
                        var stats = StatsLookup[entity];
                        var tacticsModifier = stats.Tactics / 100f; // 0-1 normalized
                        // Higher tactics = more accurate targeting (less position drift)
                        tacticsAccuracy = 1f - (tacticsModifier * 0.1f); // Up to 10% reduction in position error
                    }
                    
                    // Apply small random offset based on tactics (lower tactics = more error)
                    var error = (1f - tacticsAccuracy) * 0.5f; // Max 0.5 unit error for low tactics
                    var targetSeed = math.hash(new uint2((uint)aiState.TargetEntity.Index, (uint)aiState.TargetEntity.Version));
                    SteeringPrimitives.DeterministicOffset2D(in entity, targetSeed, out var offset2);
                    var offset = new float3(offset2.x, 0f, offset2.y) * error;
                    
                    aiState.TargetPosition = targetPos + offset;
                    return;
                }

                // Try to find in resource registry (for DOTS resource entities)
                if (HasResourceEntries && RegistryEntryLookup.TryFindEntryIndex(ResourceEntries, aiState.TargetEntity, out var resourceIndex))
                {
                    var targetPos = ResourceEntries[resourceIndex].Position;
                    if (AsteroidLookup.HasComponent(aiState.TargetEntity) &&
                        AsteroidVolumeLookup.HasComponent(aiState.TargetEntity) &&
                        TransformLookup.TryGetComponent(entity, out var selfTransform))
                    {
                        var volume = AsteroidVolumeLookup[aiState.TargetEntity];
                        var radius = math.max(0.5f, volume.Radius);
                        var toSelf = selfTransform.Position - targetPos;
                        var direction = math.normalizesafe(toSelf, new float3(0f, 0f, 1f));
                        var standoff = MiningVesselLookup.HasComponent(entity) ? 1.1f : 5.5f;
                        var vesselRadius = PhysicalLookup.HasComponent(entity)
                            ? math.max(0.1f, PhysicalLookup[entity].Radius)
                            : 0.6f;
                        if (CarrierLookup.HasComponent(entity))
                        {
                            standoff = 6.5f;
                        }
                        standoff = math.max(standoff, vesselRadius + 0.25f);
                        targetPos += direction * (radius + standoff);
                        hasAsteroidSurfaceTarget = true;
                    }

                    aiState.TargetPosition = targetPos;
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

















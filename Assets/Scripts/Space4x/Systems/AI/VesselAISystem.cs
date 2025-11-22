using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
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
    /// AI system for vessels - assigns vessels to asteroids for mining.
    /// Similar to VillagerAISystem but designed for vessels.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    [UpdateBefore(typeof(Space4X.Systems.AI.VesselTargetingSystem))]
    public partial struct VesselAISystem : ISystem
    {
        private EntityQuery _vesselQuery;
        private EntityQuery _resourceRegistryQuery;
        private EntityQuery _carrierQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _vesselQuery = SystemAPI.QueryBuilder()
                .WithAll<VesselAIState, MiningVessel, LocalTransform>()
                .WithNone<MiningOrder>()
                .Build();

            _resourceRegistryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRegistry, ResourceRegistryEntry>()
                .Build();

            _carrierQuery = SystemAPI.QueryBuilder()
                .WithAll<Carrier, LocalTransform>()
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

            if (_vesselQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            // Get resource registry to find asteroids
            if (!_resourceRegistryQuery.IsEmptyIgnoreFilter)
            {
                var resourceEntity = _resourceRegistryQuery.GetSingletonEntity();
                if (state.EntityManager.HasBuffer<ResourceRegistryEntry>(resourceEntity))
                {
                    var resourceEntries = state.EntityManager.GetBuffer<ResourceRegistryEntry>(resourceEntity);
                    var hasResources = resourceEntries.Length > 0;
                    
                    // Get carrier entities and transforms for finding nearest carrier
                    var carriers = new NativeList<Entity>(Allocator.TempJob);
                    var carrierTransforms = new NativeList<LocalTransform>(Allocator.TempJob);

                    if (!_carrierQuery.IsEmptyIgnoreFilter)
                    {
                        foreach (var (carrierTransform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                            .WithAll<Carrier>()
                            .WithEntityAccess())
                        {
                            carriers.Add(entity);
                            carrierTransforms.Add(carrierTransform.ValueRO);
                        }
                    }
                    
                    // Debug logging (only first frame)
                    if (timeState.Tick <= 5)
                    {
#if UNITY_EDITOR
                        UnityEngine.Debug.Log($"[VesselAISystem] Tick {timeState.Tick}: vessels={_vesselQuery.CalculateEntityCount()}, resources={resourceEntries.Length}, carriers={carriers.Length}, HasResources={hasResources}");
#endif
                    }

                    if (hasResources)
                    {
                        var job = new UpdateVesselAIJob
                        {
                            ResourceEntries = resourceEntries.AsNativeArray(),
                            HasResources = hasResources,
                            Carriers = carriers.AsArray(),
                            CarrierTransforms = carrierTransforms.AsArray(),
                            DeltaTime = timeState.FixedDeltaTime,
                            CurrentTick = timeState.Tick
                        };

                        var jobHandle = job.ScheduleParallel(state.Dependency);
                        var carriersDisposeHandle = carriers.Dispose(jobHandle);
                        var carrierTransformsDisposeHandle = carrierTransforms.Dispose(jobHandle);
                        state.Dependency = JobHandle.CombineDependencies(carriersDisposeHandle, carrierTransformsDisposeHandle);
                    }
                    else
                    {
                        carriers.Dispose();
                        carrierTransforms.Dispose();
                    }
                }
                else
                {
                    if (timeState.Tick <= 5)
                    {
#if UNITY_EDITOR
                        UnityEngine.Debug.LogWarning("[VesselAISystem] ResourceRegistry entity found but has no ResourceRegistryEntry buffer!");
#endif
                    }
                }
            }
            else
            {
                if (timeState.Tick <= 5)
                {
#if UNITY_EDITOR
                    UnityEngine.Debug.LogWarning("[VesselAISystem] ResourceRegistry singleton NOT FOUND! Resources won't be registered, vessels can't find targets.");
#endif
                }
            }
        }

        [BurstCompile]
        [WithNone(typeof(MiningOrder))]
        public partial struct UpdateVesselAIJob : IJobEntity
        {
            [ReadOnly] public NativeArray<ResourceRegistryEntry> ResourceEntries;
            public bool HasResources;
            [ReadOnly] public NativeArray<Entity> Carriers;
            [ReadOnly] public NativeArray<LocalTransform> CarrierTransforms;
            public float DeltaTime;
            public uint CurrentTick;

            public void Execute(ref VesselAIState aiState, in MiningVessel vessel, in LocalTransform transform)
            {
                aiState.StateTimer += DeltaTime;

                // If vessel is idle and has capacity, find a target asteroid
                if (aiState.CurrentState == VesselAIState.State.Idle && vessel.CurrentCargo < vessel.CargoCapacity * 0.95f)
                {
                    // Find nearest asteroid that matches vessel's resource type
                    Entity bestTarget = Entity.Null;
                    float bestDistance = float.MaxValue;

                    if (HasResources)
                    {
                        // Find any raw resource (asteroid) for mining
                        for (int i = 0; i < ResourceEntries.Length; i++)
                        {
                            var entry = ResourceEntries[i];

                            if (entry.Tier != ResourceTier.Raw)
                            {
                                continue;
                            }

                            if (entry.ResourceTypeIndex == ushort.MaxValue)
                            {
                                continue;
                            }

                            var distance = math.distance(transform.Position, entry.Position);
                            if (distance < bestDistance)
                            {
                                bestTarget = entry.SourceEntity;
                                bestDistance = distance;
                            }
                        }
                    }

                    if (bestTarget != Entity.Null)
                    {
                        aiState.CurrentGoal = VesselAIState.Goal.Mining;
                        aiState.CurrentState = VesselAIState.State.MovingToTarget;
                        aiState.TargetEntity = bestTarget;
                        aiState.StateTimer = 0f;
                        aiState.StateStartTick = CurrentTick;
                    }
                }
                // If vessel is full, return to carrier (or origin if no carrier)
                else if (vessel.CurrentCargo >= vessel.CargoCapacity * 0.95f && aiState.CurrentState != VesselAIState.State.Returning)
                {
                    aiState.CurrentGoal = VesselAIState.Goal.Returning;
                    aiState.CurrentState = VesselAIState.State.Returning;
                    
                    // Find nearest carrier
                    Entity nearestCarrier = Entity.Null;
                    float nearestDistance = float.MaxValue;
                    
                    if (Carriers.Length > 0 && CarrierTransforms.Length == Carriers.Length)
                    {
                        for (int i = 0; i < Carriers.Length; i++)
                        {
                            var distance = math.distance(transform.Position, CarrierTransforms[i].Position);
                            if (distance < nearestDistance)
                            {
                                nearestCarrier = Carriers[i];
                                nearestDistance = distance;
                            }
                        }
                    }
                    
                    if (nearestCarrier != Entity.Null)
                    {
                        aiState.TargetEntity = nearestCarrier;
                        // TargetPosition will be resolved by VesselTargetingSystem
                        // Debug logging removed - Burst doesn't support string formatting in jobs
                    }
                    else
                    {
                        // No carrier found, return to origin (0,0,0)
                        aiState.TargetEntity = Entity.Null;
                        aiState.TargetPosition = float3.zero;
                        // Debug logging removed - Burst doesn't support Debug.Log in jobs
                    }
                    
                    aiState.StateTimer = 0f;
                    aiState.StateStartTick = CurrentTick;
                }
                // If vessel is at target and not full, transition to mining state
                else if (aiState.CurrentState == VesselAIState.State.MovingToTarget && 
                         aiState.TargetEntity != Entity.Null &&
                         vessel.CurrentCargo < vessel.CargoCapacity * 0.95f)
                {
                    // Transition to mining state - VesselGatheringSystem will handle actual gathering
                    aiState.CurrentState = VesselAIState.State.Mining;
                    aiState.StateTimer = 0f;
                    aiState.StateStartTick = CurrentTick;
                }
            }
        }
    }
}


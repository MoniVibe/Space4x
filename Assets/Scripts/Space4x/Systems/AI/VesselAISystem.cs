using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;
using ResourceTypeId = Space4X.Registry.ResourceTypeId;
using ResourceRegistry = PureDOTS.Runtime.Components.ResourceRegistry;
using ResourceRegistryEntry = PureDOTS.Runtime.Components.ResourceRegistryEntry;
using UnityDebug = UnityEngine.Debug;

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
        private ComponentLookup<MiningOrder> _miningOrderLookup;
        private ComponentLookup<Space4X.Registry.ResourceTypeId> _resourceTypeLookup;
        private ComponentLookup<Asteroid> _asteroidLookup;
        private ComponentLookup<MinerTargetStrategy> _targetStrategyLookup;
        private ComponentLookup<PureDOTS.Runtime.Components.ResourceSourceState> _resourceStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _vesselQuery = SystemAPI.QueryBuilder()
                .WithAll<VesselAIState, MiningVessel, LocalTransform>()
                .Build();

            _resourceRegistryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResourceRegistry, ResourceRegistryEntry>()
                .Build();

            _carrierQuery = SystemAPI.QueryBuilder()
                .WithAll<Carrier, LocalTransform>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _miningOrderLookup = state.GetComponentLookup<MiningOrder>(false);
            _resourceTypeLookup = state.GetComponentLookup<Space4X.Registry.ResourceTypeId>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(true);
            _targetStrategyLookup = state.GetComponentLookup<MinerTargetStrategy>(true);
            _resourceStateLookup = state.GetComponentLookup<PureDOTS.Runtime.Components.ResourceSourceState>(true);
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
                    
                    // Debug logging (only first few ticks)
#if UNITY_EDITOR
                    if (timeState.Tick <= 5)
                    {
                        var vesselCount = _vesselQuery.CalculateEntityCount();
                        var resourceCount = resourceEntries.Length;
                        var carrierCount = carriers.Length;
                        LogEditorInfo(timeState.Tick, vesselCount, resourceCount, carrierCount, hasResources);
                    }
#endif

                    if (hasResources)
                    {
                        _miningOrderLookup.Update(ref state);
                        _resourceTypeLookup.Update(ref state);
                        _asteroidLookup.Update(ref state);
                        _targetStrategyLookup.Update(ref state);
                        _resourceStateLookup.Update(ref state);

                        var job = new UpdateVesselAIJob
                        {
                            ResourceEntries = resourceEntries.AsNativeArray(),
                            HasResources = hasResources,
                            Carriers = carriers.AsArray(),
                            CarrierTransforms = carrierTransforms.AsArray(),
                            MiningOrderLookup = _miningOrderLookup,
                            ResourceTypeLookup = _resourceTypeLookup,
                            AsteroidLookup = _asteroidLookup,
                            TargetStrategyLookup = _targetStrategyLookup,
                            ResourceStateLookup = _resourceStateLookup,
                            DeltaTime = timeState.FixedDeltaTime,
                            CurrentTick = timeState.Tick
                        };

                        // NOTE: This job writes MiningOrder via ComponentLookup on the current entity.
                        // Scheduling non-parallel avoids safety violations about parallel writes to lookups.
                        var jobHandle = job.Schedule(state.Dependency);
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
                        LogEditorWarning("[VesselAISystem] ResourceRegistry entity found but has no ResourceRegistryEntry buffer!");
#endif
                    }
                }
            }
            else
            {
                if (timeState.Tick <= 5)
                {
#if UNITY_EDITOR
                    LogEditorWarning("[VesselAISystem] ResourceRegistry singleton NOT FOUND! Resources won't be registered, vessels can't find targets.");
#endif
                }
            }
        }

        [BurstCompile]
        public partial struct UpdateVesselAIJob : IJobEntity
        {
            [ReadOnly] public NativeArray<ResourceRegistryEntry> ResourceEntries;
            public bool HasResources;
            [ReadOnly] public NativeArray<Entity> Carriers;
            [ReadOnly] public NativeArray<LocalTransform> CarrierTransforms;
            public ComponentLookup<MiningOrder> MiningOrderLookup;
            [ReadOnly] public ComponentLookup<Space4X.Registry.ResourceTypeId> ResourceTypeLookup;
            [ReadOnly] public ComponentLookup<Asteroid> AsteroidLookup;
            [ReadOnly] public ComponentLookup<MinerTargetStrategy> TargetStrategyLookup;
            [ReadOnly] public ComponentLookup<PureDOTS.Runtime.Components.ResourceSourceState> ResourceStateLookup;
            public float DeltaTime;
            public uint CurrentTick;

            public void Execute(ref VesselAIState aiState, in MiningVessel vessel, in LocalTransform transform, Entity entity)
            {
                aiState.StateTimer += DeltaTime;

                // Check if vessel has MiningOrder that needs target assignment
                bool hasMiningOrder = MiningOrderLookup.HasComponent(entity);
                if (hasMiningOrder)
                {
                    var miningOrder = MiningOrderLookup.GetRefRW(entity).ValueRO;
                    
                    // Assign target to MiningOrder if pending and no target
                    if (miningOrder.Status == MiningOrderStatus.Pending && 
                        miningOrder.TargetEntity == Entity.Null &&
                        vessel.CurrentCargo < vessel.CargoCapacity * 0.95f)
                    {
                        // Get target selection strategy (default to Nearest if not specified)
                        var strategy = MinerTargetStrategy.Strategy.Nearest;
                        if (TargetStrategyLookup.HasComponent(entity))
                        {
                            strategy = TargetStrategyLookup[entity].SelectionStrategy;
                        }

                        Entity bestTarget = Entity.Null;
                        float bestScore = strategy == MinerTargetStrategy.Strategy.Nearest ? float.MaxValue : float.MinValue;

                        if (HasResources)
                        {
                            if (miningOrder.PreferredTarget != Entity.Null &&
                                ResourceStateLookup.HasComponent(miningOrder.PreferredTarget) &&
                                ResourceStateLookup[miningOrder.PreferredTarget].UnitsRemaining > 0f &&
                                ResourceTypeLookup.HasComponent(miningOrder.PreferredTarget) &&
                                ResourceTypeLookup[miningOrder.PreferredTarget].Value == miningOrder.ResourceId)
                            {
                                bestTarget = miningOrder.PreferredTarget;
                                bestScore = strategy == MinerTargetStrategy.Strategy.Nearest ? 0f : float.MaxValue;
                            }

                            // Find best asteroid matching MiningOrder.ResourceId based on strategy
                            for (int i = 0; i < ResourceEntries.Length && bestTarget == Entity.Null; i++)
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

                                // Check if resource matches MiningOrder.ResourceId
                                if (!ResourceTypeLookup.HasComponent(entry.SourceEntity))
                                {
                                    continue;
                                }

                                var resourceTypeId = ResourceTypeLookup[entry.SourceEntity];
                                if (resourceTypeId.Value != miningOrder.ResourceId)
                                {
                                    continue;
                                }

                                // Skip if asteroid is depleted
                                if (ResourceStateLookup.HasComponent(entry.SourceEntity) &&
                                    ResourceStateLookup[entry.SourceEntity].UnitsRemaining <= 0f)
                                {
                                    continue;
                                }

                                var distance = math.distance(transform.Position, entry.Position);
                                float score = 0f;

                                switch (strategy)
                                {
                                    case MinerTargetStrategy.Strategy.Nearest:
                                        // Lower distance is better
                                        score = -distance; // Negate so higher is better
                                        break;

                                    case MinerTargetStrategy.Strategy.BestYield:
                                        // Higher yield (ResourceAmount / MiningRate) is better
                                        if (AsteroidLookup.HasComponent(entry.SourceEntity))
                                        {
                                            var asteroid = AsteroidLookup[entry.SourceEntity];
                                            var yield = asteroid.MiningRate > 0f 
                                                ? asteroid.ResourceAmount / asteroid.MiningRate 
                                                : asteroid.ResourceAmount;
                                            score = yield;
                                        }
                                        else
                                        {
                                            // Fallback: use ResourceAmount if no Asteroid component
                                            score = ResourceStateLookup.HasComponent(entry.SourceEntity)
                                                ? ResourceStateLookup[entry.SourceEntity].UnitsRemaining
                                                : 0f;
                                        }
                                        break;

                                    case MinerTargetStrategy.Strategy.Balanced:
                                        // Score = yield / (distance + 1)
                                        float yieldValue = 0f;
                                        if (AsteroidLookup.HasComponent(entry.SourceEntity))
                                        {
                                            var asteroid = AsteroidLookup[entry.SourceEntity];
                                            yieldValue = asteroid.MiningRate > 0f 
                                                ? asteroid.ResourceAmount / asteroid.MiningRate 
                                                : asteroid.ResourceAmount;
                                        }
                                        else
                                        {
                                            yieldValue = ResourceStateLookup.HasComponent(entry.SourceEntity)
                                                ? ResourceStateLookup[entry.SourceEntity].UnitsRemaining
                                                : 0f;
                                        }
                                        score = yieldValue / (distance + 1f);
                                        break;
                                }

                                if ((strategy == MinerTargetStrategy.Strategy.Nearest && score > bestScore) ||
                                    (strategy != MinerTargetStrategy.Strategy.Nearest && score > bestScore))
                                {
                                    bestTarget = entry.SourceEntity;
                                    bestScore = score;
                                }
                            }
                        }

                        if (bestTarget != Entity.Null)
                        {
                            // Assign target to MiningOrder
                            miningOrder.TargetEntity = bestTarget;
                            miningOrder.Status = MiningOrderStatus.Active;
                            MiningOrderLookup.GetRefRW(entity).ValueRW = miningOrder;
                        }
                    }

                    // Sync MiningOrder.TargetEntity to VesselAIState if MiningOrder has a target
                    if (miningOrder.TargetEntity != Entity.Null && 
                        miningOrder.Status == MiningOrderStatus.Active)
                    {
                        // If MiningOrder has a target, use it for movement (unless vessel is full and needs to return)
                        if (vessel.CurrentCargo < vessel.CargoCapacity * 0.95f)
                        {
                            if (aiState.TargetEntity != miningOrder.TargetEntity)
                            {
                                aiState.TargetEntity = miningOrder.TargetEntity;
                                aiState.CurrentGoal = VesselAIState.Goal.Mining;
                                if (aiState.CurrentState == VesselAIState.State.Idle)
                                {
                                    aiState.CurrentState = VesselAIState.State.MovingToTarget;
                                    aiState.StateTimer = 0f;
                                    aiState.StateStartTick = CurrentTick;
                                }
                            }
                            // Skip legacy AI target finding if MiningOrder is active and vessel not full
                            // But continue to check return logic below
                        }
                    }
                }

                // If vessel is idle and has capacity, find a target asteroid (legacy path for vessels without MiningOrder)
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
                // If vessel is full, return to carrier (or origin if no carrier).
                // Note: VesselGatheringSystem can set CurrentState=Returning first, so we must also handle
                // "already Returning but still targeting the resource" by (re)assigning a carrier target here.
                else if (vessel.CurrentCargo >= vessel.CargoCapacity * 0.95f)
                {
                    var desiredCarrier = vessel.CarrierEntity;
                    if (desiredCarrier == Entity.Null)
                    {
                        // Fall back to nearest carrier when the vessel isn't explicitly assigned.
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

                        desiredCarrier = nearestCarrier;
                    }

                    var needsReturnTarget = aiState.CurrentState != VesselAIState.State.Returning
                                            || aiState.CurrentGoal != VesselAIState.Goal.Returning
                                            || aiState.TargetEntity != desiredCarrier;

                    if (needsReturnTarget)
                    {
                        aiState.CurrentGoal = VesselAIState.Goal.Returning;
                        aiState.CurrentState = VesselAIState.State.Returning;
                        aiState.TargetEntity = desiredCarrier;
                        // Always clear TargetPosition so the targeting system can resolve it (including origin targets).
                        aiState.TargetPosition = float3.zero;
                        aiState.StateTimer = 0f;
                        aiState.StateStartTick = CurrentTick;
                    }
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

#if UNITY_EDITOR
        [BurstDiscard]
        private static void LogEditorInfo(
            uint tick,
            int vesselCount,
            int resourceCount,
            int carrierCount,
            bool hasResources)
        {
            UnityEngine.Debug.Log($"[VesselAISystem] Tick {tick}: vessels={vesselCount}, resources={resourceCount}, carriers={carrierCount}, HasResources={hasResources}");
        }

        [BurstDiscard]
        private static void LogEditorWarning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }
#endif
    }
}

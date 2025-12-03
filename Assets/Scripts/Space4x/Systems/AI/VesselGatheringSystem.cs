using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Handles vessels gathering resources from asteroids when they're close enough.
    /// Similar to VillagerJobExecutionSystem but for vessels.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(VesselMovementSystem))]
    public partial struct VesselGatheringSystem : ISystem
    {
        private ComponentLookup<ResourceSourceState> _resourceStateLookup;
        private ComponentLookup<ResourceSourceConfig> _resourceConfigLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Asteroid> _asteroidLookup;
        private ComponentLookup<ResourceDeposit> _resourceDepositLookup;
        private ComponentLookup<ResourceNodeTag> _resourceNodeTagLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _resourceStateLookup = state.GetComponentLookup<ResourceSourceState>(false);
            _resourceConfigLookup = state.GetComponentLookup<ResourceSourceConfig>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(true);
            _resourceDepositLookup = state.GetComponentLookup<ResourceDeposit>(false);
            _resourceNodeTagLookup = state.GetComponentLookup<ResourceNodeTag>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MiningVessel>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _resourceStateLookup.Update(ref state);
            _resourceConfigLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _asteroidLookup.Update(ref state);
            _resourceDepositLookup.Update(ref state);
            _resourceNodeTagLookup.Update(ref state);

            var gatherDistance = 3f; // Vessels gather when within 3 units of asteroid
            var gatherDistanceSq = gatherDistance * gatherDistance;
            var deltaTime = timeState.FixedDeltaTime;
            var hasCommandLog = SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out var commandLog);

            foreach (var (vessel, aiState, transform, entity) in SystemAPI.Query<RefRW<MiningVessel>, RefRW<VesselAIState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                // Only gather if we're in mining state and have a target
                if (aiState.ValueRO.CurrentState != VesselAIState.State.Mining ||
                    aiState.ValueRO.TargetEntity == Entity.Null ||
                    vessel.ValueRO.CurrentCargo >= vessel.ValueRO.CargoCapacity * 0.95f)
                {
                    continue;
                }

                var targetEntity = aiState.ValueRO.TargetEntity;
                
                // Check if target resource still exists
                bool hasResourceSource = _resourceStateLookup.HasComponent(targetEntity);
                bool hasResourceDeposit = _resourceDepositLookup.HasComponent(targetEntity) && 
                                         _resourceNodeTagLookup.HasComponent(targetEntity);
                
                if ((!hasResourceSource && !hasResourceDeposit) ||
                    !_transformLookup.HasComponent(targetEntity))
                {
                    // Target lost, return to idle
                    aiState.ValueRW.CurrentState = VesselAIState.State.Idle;
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Idle;
                    aiState.ValueRW.TargetEntity = Entity.Null;
                    continue;
                }

                var resourceTransform = _transformLookup[targetEntity];
                
                // Determine resource type and amount from either ResourceSourceState or ResourceDeposit
                ResourceType resourceType;
                float unitsRemaining = 0f;
                
                if (hasResourceDeposit)
                {
                    // Use ResourceDeposit (rocks)
                    var deposit = _resourceDepositLookup[targetEntity];
                    unitsRemaining = deposit.CurrentAmount;
                    
                    // Map ResourceTypeId to ResourceType enum (simplified - would need proper mapping)
                    // For now, default to Minerals if we can't determine from Asteroid component
                    resourceType = ResourceType.Minerals;
                    
                    // Try to get resource type from Asteroid if it exists (for compatibility)
                    if (_asteroidLookup.HasComponent(targetEntity))
                    {
                        resourceType = _asteroidLookup[targetEntity].ResourceType;
                    }
                }
                else if (hasResourceSource)
                {
                    // Use legacy ResourceSourceState (asteroids)
                    var resourceState = _resourceStateLookup[targetEntity];
                    unitsRemaining = resourceState.UnitsRemaining;
                    
                    if (!_asteroidLookup.HasComponent(targetEntity))
                    {
                        continue;
                    }
                    resourceType = _asteroidLookup[targetEntity].ResourceType;
                }
                else
                {
                    continue;
                }

                var asteroid = _asteroidLookup.HasComponent(targetEntity) 
                    ? _asteroidLookup[targetEntity] 
                    : default(Asteroid);

                // Prevent mixing cargo types – if we're carrying something else, head back to carrier
                var vesselValue = vessel.ValueRO;
                ResourceType targetResourceType = _asteroidLookup.HasComponent(targetEntity) 
                    ? asteroid.ResourceType 
                    : resourceType;
                    
                if (vesselValue.CurrentCargo > 0.01f && vesselValue.CargoResourceType != targetResourceType)
                {
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Returning;
                    aiState.ValueRW.CurrentState = VesselAIState.State.Returning;
                    continue;
                }

                if (vesselValue.CurrentCargo <= 0.01f)
                {
                    vesselValue.CargoResourceType = targetResourceType;
                }

                // Check distance to resource
                var distSq = math.distancesq(transform.ValueRO.Position, resourceTransform.Position);
                if (distSq > gatherDistanceSq)
                {
                    // Not close enough yet, transition to moving state
                    if (aiState.ValueRO.CurrentState == VesselAIState.State.Mining)
                    {
                        aiState.ValueRW.CurrentState = VesselAIState.State.MovingToTarget;
                    }
                    continue;
                }

                // Close enough to gather - transition to mining state if not already
                if (aiState.ValueRO.CurrentState == VesselAIState.State.MovingToTarget)
                {
                    aiState.ValueRW.CurrentState = VesselAIState.State.Mining;
                    aiState.ValueRW.StateTimer = 0f;
                    aiState.ValueRW.StateStartTick = timeState.Tick;
                }

                // Check if resource is depleted
                if (unitsRemaining <= 0f)
                {
                    // Resource depleted, return to idle to find new target
                    aiState.ValueRW.CurrentState = VesselAIState.State.Idle;
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Idle;
                    aiState.ValueRW.TargetEntity = Entity.Null;
                    continue;
                }

                // Get gather rate from config (or use default)
                float gatherRate = 8f; // Default gather rate
                if (_resourceConfigLookup.HasComponent(targetEntity))
                {
                    var config = _resourceConfigLookup[targetEntity];
                    gatherRate = math.max(0.1f, config.GatherRatePerWorker);
                }

                var gatherAmount = gatherRate * deltaTime;

                // Don't gather more than available or capacity remaining
                var capacityRemaining = vesselValue.CargoCapacity - vesselValue.CurrentCargo;
                gatherAmount = math.min(gatherAmount, unitsRemaining);
                gatherAmount = math.min(gatherAmount, capacityRemaining);

                if (gatherAmount <= 0f)
                {
                    continue;
                }

                // Update resource state (either ResourceSourceState or ResourceDeposit)
                if (hasResourceDeposit)
                {
                    var deposit = _resourceDepositLookup[targetEntity];
                    deposit.CurrentAmount -= gatherAmount;
                    
                    // Handle depletion
                    if (deposit.CurrentAmount <= 0f)
                    {
                        deposit.CurrentAmount = 0f;
                        // Optionally add DepletedTag
                        if (!state.EntityManager.HasComponent<DepletedTag>(targetEntity))
                        {
                            state.EntityManager.AddComponent<DepletedTag>(targetEntity);
                        }
                    }
                    
                    _resourceDepositLookup[targetEntity] = deposit;
                }
                else if (hasResourceSource)
                {
                    var resourceState = _resourceStateLookup[targetEntity];
                    resourceState.UnitsRemaining -= gatherAmount;
                    _resourceStateLookup[targetEntity] = resourceState;
                }

                // Update vessel cargo
                vesselValue.CurrentCargo += gatherAmount;
                vessel.ValueRW = vesselValue;

                if (hasCommandLog)
                {
                    commandLog.Add(new MiningCommandLogEntry
                    {
                        Tick = timeState.Tick,
                        CommandType = MiningCommandType.Gather,
                        SourceEntity = targetEntity,
                        TargetEntity = entity,
                        ResourceType = targetResourceType,
                        Amount = gatherAmount,
                        Position = resourceTransform.Position
                    });
                }

                // If vessel is full, transition to returning state
                if (vesselValue.CurrentCargo >= vesselValue.CargoCapacity * 0.95f)
                {
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Returning;
                    aiState.ValueRW.CurrentState = VesselAIState.State.Returning;
                    // TargetEntity will be set to carrier by VesselAISystem
                }
            }
        }
    }
}


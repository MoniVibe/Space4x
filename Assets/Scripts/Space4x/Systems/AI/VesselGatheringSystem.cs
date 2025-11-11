using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _resourceStateLookup = state.GetComponentLookup<ResourceSourceState>(false);
            _resourceConfigLookup = state.GetComponentLookup<ResourceSourceConfig>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(true);

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

            var gatherDistance = 3f; // Vessels gather when within 3 units of asteroid
            var gatherDistanceSq = gatherDistance * gatherDistance;
            var deltaTime = timeState.FixedDeltaTime;

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

                // Check if target resource still exists
                if (!_resourceStateLookup.HasComponent(aiState.ValueRO.TargetEntity) ||
                    !_transformLookup.HasComponent(aiState.ValueRO.TargetEntity))
                {
                    // Target lost, return to idle
                    aiState.ValueRW.CurrentState = VesselAIState.State.Idle;
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Idle;
                    aiState.ValueRW.TargetEntity = Entity.Null;
                    continue;
                }

                var resourceState = _resourceStateLookup[aiState.ValueRO.TargetEntity];
                var resourceTransform = _transformLookup[aiState.ValueRO.TargetEntity];

                // Ensure we know the asteroid metadata for resource typing
                if (!_asteroidLookup.HasComponent(aiState.ValueRO.TargetEntity))
                {
                    continue;
                }

                var asteroid = _asteroidLookup[aiState.ValueRO.TargetEntity];

                // Prevent mixing cargo types – if we're carrying something else, head back to carrier
                var vesselValue = vessel.ValueRO;
                if (vesselValue.CurrentCargo > 0.01f && vesselValue.CargoResourceType != asteroid.ResourceType)
                {
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Returning;
                    aiState.ValueRW.CurrentState = VesselAIState.State.Returning;
                    continue;
                }

                if (vesselValue.CurrentCargo <= 0.01f)
                {
                    vesselValue.CargoResourceType = asteroid.ResourceType;
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
                if (resourceState.UnitsRemaining <= 0f)
                {
                    // Resource depleted, return to idle to find new target
                    aiState.ValueRW.CurrentState = VesselAIState.State.Idle;
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Idle;
                    aiState.ValueRW.TargetEntity = Entity.Null;
                    continue;
                }

                // Get gather rate from config
                var config = _resourceConfigLookup.HasComponent(aiState.ValueRO.TargetEntity)
                    ? _resourceConfigLookup[aiState.ValueRO.TargetEntity]
                    : new ResourceSourceConfig { GatherRatePerWorker = 8f };

                var gatherRate = math.max(0.1f, config.GatherRatePerWorker);
                var gatherAmount = gatherRate * deltaTime;

                // Don't gather more than available or capacity remaining
                var capacityRemaining = vesselValue.CargoCapacity - vesselValue.CurrentCargo;
                gatherAmount = math.min(gatherAmount, resourceState.UnitsRemaining);
                gatherAmount = math.min(gatherAmount, capacityRemaining);

                if (gatherAmount <= 0f)
                {
                    continue;
                }

                // Update resource state
                resourceState.UnitsRemaining -= gatherAmount;
                _resourceStateLookup[aiState.ValueRO.TargetEntity] = resourceState;

                // Update vessel cargo
                vesselValue.CurrentCargo += gatherAmount;
                vessel.ValueRW = vesselValue;

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


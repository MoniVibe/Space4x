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

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _resourceStateLookup = state.GetComponentLookup<ResourceSourceState>(false);
            _resourceConfigLookup = state.GetComponentLookup<ResourceSourceConfig>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);

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
                var capacityRemaining = vessel.ValueRO.CargoCapacity - vessel.ValueRO.CurrentCargo;
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
                var vesselValue = vessel.ValueRO;
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


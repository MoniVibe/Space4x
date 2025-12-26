using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Space4X.Systems.AI;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Bridges MiningState.Phase to VesselAIState.CurrentState for entities with MiningOrder,
    /// ensuring movement systems can respond to mining state changes.
    /// Runs after Space4XMinerMiningSystem but before VesselMovementSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    // Removed invalid UpdateAfter: Space4XMinerMiningSystem runs in FixedStepSimulationSystemGroup.
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselAISystem))]
    [UpdateBefore(typeof(Space4X.Systems.AI.VesselTargetingSystem))]
    public partial struct Space4XMiningMovementBridgeSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
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

            var currentTick = timeState.Tick;
            _transformLookup.Update(ref state);

            foreach (var (miningState, miningOrder, aiState, entity) in SystemAPI.Query<RefRO<MiningState>, RefRO<MiningOrder>, RefRW<VesselAIState>>()
                         .WithAll<MiningOrder>()
                         .WithEntityAccess())
            {
                // Only sync if MiningOrder is active
                if (miningOrder.ValueRO.Status != MiningOrderStatus.Active)
                {
                    continue;
                }

                // Sync MiningState.Phase to VesselAIState.CurrentState
                var phase = miningState.ValueRO.Phase;
                var targetEntity = miningState.ValueRO.ActiveTarget;

                // Map MiningPhase to VesselAIState.State
                VesselAIState.State newState;
                switch (phase)
                {
                    case MiningPhase.Idle:
                        newState = VesselAIState.State.Idle;
                        break;
                    case MiningPhase.Undocking:
                        newState = VesselAIState.State.Idle;
                        break;
                    case MiningPhase.ApproachTarget:
                        newState = VesselAIState.State.MovingToTarget;
                        break;
                    case MiningPhase.Mining:
                        newState = VesselAIState.State.Mining;
                        break;
                    case MiningPhase.Latching:
                    case MiningPhase.Detaching:
                        newState = VesselAIState.State.Mining;
                        break;
                    case MiningPhase.ReturnApproach:
                    case MiningPhase.Docking:
                        newState = VesselAIState.State.Returning;
                        break;
                    default:
                        newState = VesselAIState.State.Idle;
                        break;
                }

                var desiredGoal = (phase == MiningPhase.ReturnApproach || phase == MiningPhase.Docking)
                    ? VesselAIState.Goal.Returning
                    : VesselAIState.Goal.Mining;

                // Update VesselAIState if changed
                if (aiState.ValueRO.CurrentState != newState)
                {
                    aiState.ValueRW.CurrentState = newState;
                    aiState.ValueRW.CurrentGoal = desiredGoal;

                    // Sync target entity from MiningState if available
                    if (targetEntity != Entity.Null && aiState.ValueRO.TargetEntity != targetEntity)
                    {
                        aiState.ValueRW.TargetEntity = targetEntity;
                    }

                    // Update state timing
                    if (newState != VesselAIState.State.Idle)
                    {
                        aiState.ValueRW.StateTimer = 0f;
                        aiState.ValueRW.StateStartTick = currentTick;
                    }
                }
                else if (targetEntity != Entity.Null && aiState.ValueRO.TargetEntity != targetEntity)
                {
                    // Update target even if state hasn't changed
                    aiState.ValueRW.TargetEntity = targetEntity;
                }

                if (aiState.ValueRO.CurrentGoal != desiredGoal)
                {
                    aiState.ValueRW.CurrentGoal = desiredGoal;
                }

                // Populate TargetPosition directly when we have a transform so movement systems don't skip MiningOrder vessels
                if (targetEntity != Entity.Null && _transformLookup.HasComponent(targetEntity))
                {
                    var targetTransform = _transformLookup[targetEntity];
                    aiState.ValueRW.TargetPosition = targetTransform.Position;
                }
            }
        }
    }
}

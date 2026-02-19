using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using Space4X.Runtime;
using Space4X.Registry;
using Space4X.Systems.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Systems
{
    /// <summary>
    /// Bridges shared AICommand queue to Space4X-specific VesselAIState.
    /// Consumes commands from AISystemGroup pipeline and translates them into vessel goals.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    [UpdateBefore(typeof(VesselTargetingSystem))]
    public partial struct Space4XVesselAICommandBridgeSystem : ISystem
    {
        private ComponentLookup<VesselAIUtilityBinding> _utilityBindingLookup;
        private ComponentLookup<VesselAIState> _aiStateLookup;
        private ComponentLookup<MiningVessel> _miningVesselLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _utilityBindingLookup = state.GetComponentLookup<VesselAIUtilityBinding>(true);
            _aiStateLookup = state.GetComponentLookup<VesselAIState>(false);
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<AICommandQueueTag>();
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

            var queueEntity = SystemAPI.GetSingletonEntity<AICommandQueueTag>();
            if (!state.EntityManager.HasBuffer<AICommand>(queueEntity))
            {
                return;
            }

            var commands = state.EntityManager.GetBuffer<AICommand>(queueEntity);
            if (commands.Length == 0)
            {
                return;
            }

            _utilityBindingLookup.Update(ref state);
            _aiStateLookup.Update(ref state);
            _miningVesselLookup.Update(ref state);

            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                if (!_aiStateLookup.HasComponent(command.Agent))
                {
                    continue;
                }

                // Only process commands for miner vessels
                if (!_miningVesselLookup.HasComponent(command.Agent))
                {
                    continue;
                }

                var vessel = _miningVesselLookup[command.Agent];
                var aiState = _aiStateLookup[command.Agent];
                var goal = MapActionToGoal(command.Agent, command.ActionIndex, vessel, ref state);

                // Only update if goal changed or if we need to update target
                if (goal != VesselAIState.Goal.None)
                {
                    bool shouldUpdate = false;

                    if (goal != aiState.CurrentGoal)
                    {
                        aiState.CurrentGoal = goal;
                        aiState.CurrentState = GoalToState(goal);
                        aiState.StateTimer = 0f;
                        aiState.StateStartTick = timeState.Tick;
                        shouldUpdate = true;
                    }

                    // Update target if provided
                    if (command.TargetEntity != Entity.Null)
                    {
                        aiState.TargetEntity = command.TargetEntity;
                        aiState.TargetPosition = command.TargetPosition;
                        shouldUpdate = true;
                    }

                    if (shouldUpdate)
                    {
                        _aiStateLookup[command.Agent] = aiState;
                    }
                }
            }
        }

        private VesselAIState.Goal MapActionToGoal(Entity agent, byte actionIndex, MiningVessel vessel, ref SystemState state)
        {
            // Check if vessel has utility binding that maps actions to goals
            if (_utilityBindingLookup.HasComponent(agent))
            {
                var binding = _utilityBindingLookup[agent];
                if (actionIndex < binding.Goals.Length)
                {
                    var goal = binding.Goals[actionIndex];
                    
                    // Override logic: if vessel is full, force Returning goal
                    if (vessel.CurrentCargo >= vessel.CargoCapacity * 0.95f && goal == VesselAIState.Goal.Mining)
                    {
                        return VesselAIState.Goal.Returning;
                    }
                    
                    // If vessel is not full and goal is Returning, allow Mining
                    if (vessel.CurrentCargo < vessel.CargoCapacity * 0.95f && goal == VesselAIState.Goal.Returning)
                    {
                        return VesselAIState.Goal.Mining;
                    }
                    
                    return goal;
                }
            }

            // Fallback: map action indices to goals
            // Action 0: Mining
            // Action 1: Returning
            switch (actionIndex)
            {
                case 0:
                    // Only mine if not full
                    if (vessel.CurrentCargo < vessel.CargoCapacity * 0.95f)
                    {
                        return VesselAIState.Goal.Mining;
                    }
                    return VesselAIState.Goal.Returning;
                case 1:
                    // Only return if full
                    if (vessel.CurrentCargo >= vessel.CargoCapacity * 0.95f)
                    {
                        return VesselAIState.Goal.Returning;
                    }
                    return VesselAIState.Goal.Mining;
                default:
                    return VesselAIState.Goal.None;
            }
        }

        private static VesselAIState.State GoalToState(VesselAIState.Goal goal)
        {
            return goal switch
            {
                VesselAIState.Goal.Mining => VesselAIState.State.MovingToTarget,
                VesselAIState.Goal.Returning => VesselAIState.State.Returning,
                VesselAIState.Goal.Idle => VesselAIState.State.Idle,
                _ => VesselAIState.State.Idle
            };
        }
    }
}

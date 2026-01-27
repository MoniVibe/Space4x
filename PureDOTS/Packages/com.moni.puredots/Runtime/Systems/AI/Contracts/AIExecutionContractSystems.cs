using PureDOTS.Runtime.AI.Contracts;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Contracts;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.AI.Contracts
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ContractIntentArbitrationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ContractHarnessEnabled>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            foreach (var intent in SystemAPI.Query<RefRW<AIContractIntent>>())
            {
                if (intent.ValueRO.IntentId != 0)
                {
                    intent.ValueRW.LastUpdatedTick = tick;
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ContractIntentArbitrationSystem))]
    public partial struct ContractActionSelectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ContractHarnessEnabled>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            foreach (var (intent, selection, execution) in SystemAPI.Query<
                RefRO<AIContractIntent>,
                RefRW<AIContractActionSelectionState>,
                RefRO<AIContractActionExecutionState>>())
            {
                if (execution.ValueRO.Phase != AIContractExecutionPhase.Idle)
                {
                    continue;
                }

                if (intent.ValueRO.IntentId == 0)
                {
                    selection.ValueRW.ActionId = 0;
                    selection.ValueRW.ChosenTick = tick;
                    continue;
                }

                if (selection.ValueRO.ActionId == 0)
                {
                    selection.ValueRW.ActionId = intent.ValueRO.IntentId;
                    selection.ValueRW.ChosenTick = tick;
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ContractActionSelectionSystem))]
    public partial struct ContractInterruptResolutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ContractHarnessEnabled>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            foreach (var (intent, interrupt, execution) in SystemAPI.Query<
                RefRO<AIContractIntent>,
                RefRW<AIContractInterruptRequest>,
                RefRW<AIContractActionExecutionState>>())
            {
                if (interrupt.ValueRO.Reason == 0)
                {
                    continue;
                }

                if (interrupt.ValueRO.Priority >= intent.ValueRO.Priority)
                {
                    execution.ValueRW.Phase = AIContractExecutionPhase.Interrupted;
                    execution.ValueRW.LastInterruptedTick = tick;
                    execution.ValueRW.LastTransitionTick = tick;
                    execution.ValueRW.FailureCode = interrupt.ValueRO.Reason;
                }

                interrupt.ValueRW.Reason = 0;
                interrupt.ValueRW.Priority = 0;
                interrupt.ValueRW.RequestedTick = 0;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ContractInterruptResolutionSystem))]
    public partial struct ContractActionExecutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ContractHarnessEnabled>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            foreach (var (selection, execution) in SystemAPI.Query<
                RefRO<AIContractActionSelectionState>,
                RefRW<AIContractActionExecutionState>>())
            {
                if (execution.ValueRO.Phase == AIContractExecutionPhase.Interrupted ||
                    execution.ValueRO.Phase == AIContractExecutionPhase.Failed)
                {
                    continue;
                }

                if (execution.ValueRO.Phase == AIContractExecutionPhase.Idle && selection.ValueRO.ActionId != 0)
                {
                    execution.ValueRW.ActionId = selection.ValueRO.ActionId;
                    execution.ValueRW.Phase = AIContractExecutionPhase.Running;
                    execution.ValueRW.PhaseStartTick = tick;
                    execution.ValueRW.LastTransitionTick = tick;
                    continue;
                }

                if (execution.ValueRO.Phase == AIContractExecutionPhase.Running)
                {
                    if (tick - execution.ValueRO.PhaseStartTick >= 2)
                    {
                        execution.ValueRW.Phase = AIContractExecutionPhase.Success;
                        execution.ValueRW.LastTransitionTick = tick;
                    }
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ContractActionExecutionSystem))]
    public partial struct ContractRecoverySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ContractHarnessEnabled>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            foreach (var (intent, selection, execution, recovery) in SystemAPI.Query<
                RefRO<AIContractIntent>,
                RefRW<AIContractActionSelectionState>,
                RefRW<AIContractActionExecutionState>,
                RefRW<AIContractRecoveryState>>())
            {
                if (execution.ValueRO.Phase != AIContractExecutionPhase.Interrupted &&
                    execution.ValueRO.Phase != AIContractExecutionPhase.Failed)
                {
                    continue;
                }

                if (tick <= execution.ValueRO.LastInterruptedTick)
                {
                    continue;
                }

                if (tick < recovery.ValueRO.CooldownUntilTick)
                {
                    continue;
                }

                if (recovery.ValueRO.RetryBudget > 0 && intent.ValueRO.IntentId != 0)
                {
                    recovery.ValueRW.RetryBudget -= 1;
                    selection.ValueRW.ActionId = 0;
                    execution.ValueRW.Phase = AIContractExecutionPhase.Idle;
                    recovery.ValueRW.LastRecoveryTick = tick;
                    recovery.ValueRW.CooldownUntilTick = tick + 1;
                    continue;
                }

                selection.ValueRW.ActionId = 0;
                execution.ValueRW.Phase = AIContractExecutionPhase.Idle;
                recovery.ValueRW.LastRecoveryTick = tick;
                recovery.ValueRW.CooldownUntilTick = tick + 1;
            }
        }
    }
}

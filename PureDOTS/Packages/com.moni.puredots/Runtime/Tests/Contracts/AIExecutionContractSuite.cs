#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using PureDOTS.Runtime.AI.Contracts;
using PureDOTS.Systems.AI.Contracts;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace PureDOTS.Tests.Contracts
{
    public sealed class AIExecutionContractSuite : IContractSuite
    {
        private Entity _agent;

        public string ContractId => "AI.EXECUTION.V1";

        public int StepCount => 5;

        public IReadOnlyList<Type> SystemTypes => new[]
        {
            typeof(ContractIntentArbitrationSystem),
            typeof(ContractActionSelectionSystem),
            typeof(ContractInterruptResolutionSystem),
            typeof(ContractActionExecutionSystem),
            typeof(ContractRecoverySystem)
        };

        public void Setup(World world)
        {
        }

        public void Seed(EntityManager entityManager)
        {
            _agent = entityManager.CreateEntity(
                typeof(AIContractIntent),
                typeof(AIContractActionSelectionState),
                typeof(AIContractActionExecutionState),
                typeof(AIContractRecoveryState),
                typeof(AIContractInterruptRequest));

            entityManager.SetComponentData(_agent, new AIContractIntent
            {
                IntentId = 1,
                Priority = 5,
                Issuer = Entity.Null,
                LastUpdatedTick = 0
            });

            entityManager.SetComponentData(_agent, new AIContractActionSelectionState
            {
                ActionId = 0,
                ChosenTick = 0
            });

            entityManager.SetComponentData(_agent, new AIContractActionExecutionState
            {
                ActionId = 0,
                Phase = AIContractExecutionPhase.Idle,
                PhaseStartTick = 0,
                LastTransitionTick = 0,
                FailureCode = 0,
                LastInterruptedTick = 0
            });

            entityManager.SetComponentData(_agent, new AIContractRecoveryState
            {
                CooldownUntilTick = 0,
                RetryBudget = 1,
                LastRecoveryTick = 0
            });

            entityManager.SetComponentData(_agent, new AIContractInterruptRequest
            {
                Reason = 0,
                Priority = 0,
                RequestedTick = 0
            });
        }

        public void Step(World world, uint tick)
        {
            if (tick == 1)
            {
                var entityManager = world.EntityManager;
                entityManager.SetComponentData(_agent, new AIContractInterruptRequest
                {
                    Reason = 1,
                    Priority = 10,
                    RequestedTick = tick
                });
            }
        }

        public void Assert(EntityManager entityManager)
        {
            var execution = entityManager.GetComponentData<AIContractActionExecutionState>(_agent);
            var recovery = entityManager.GetComponentData<AIContractRecoveryState>(_agent);
            var timeState = entityManager.CreateEntityQuery(typeof(PureDOTS.Runtime.Components.TimeState))
                .GetSingleton<PureDOTS.Runtime.Components.TimeState>();

            NUnitAssert.That(execution.LastInterruptedTick, Is.EqualTo(1u));
            NUnitAssert.That(recovery.LastRecoveryTick, Is.GreaterThan(0u));
            NUnitAssert.That(recovery.LastRecoveryTick - execution.LastInterruptedTick, Is.LessThanOrEqualTo(2u));
            NUnitAssert.That(execution.Phase, Is.Not.EqualTo(AIContractExecutionPhase.Running));

            if (execution.Phase == AIContractExecutionPhase.Running)
            {
                NUnitAssert.LessOrEqual(timeState.Tick - execution.PhaseStartTick, 2u);
            }
        }
    }
}
#endif

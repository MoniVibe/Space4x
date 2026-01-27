#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace PureDOTS.Tests.Contracts
{
    public sealed class ContractSuiteRunnerTests
    {
        public static IEnumerable<IContractSuite> Suites()
        {
            yield return new AIExecutionContractSuite();
            yield return new ResourceProductionContractSuite();
            yield return new QualityContractSuite();
            yield return new ConstructionContractSuite();
            yield return new NeedsContractSuite();
        }

        [TestCaseSource(nameof(Suites))]
        public void ContractSuite_Executes(IContractSuite suite)
        {
            var testWorld = new ContractTestWorld($"ContractSuite:{suite.ContractId}");
            try
            {
                RegisterSystems(testWorld, suite.SystemTypes);
                suite.Setup(testWorld.World);
                suite.Seed(testWorld.EntityManager);

                for (int stepIndex = 0; stepIndex < suite.StepCount; stepIndex++)
                {
                    var nextTick = testWorld.CurrentTick + 1;
                    suite.Step(testWorld.World, nextTick);
                    testWorld.StepTicks(1);
                }

                suite.Assert(testWorld.EntityManager);
                AssertFatalCounters(testWorld.EntityManager);
            }
            finally
            {
                testWorld.Dispose();
            }
        }

        private static void RegisterSystems(ContractTestWorld world, IReadOnlyList<Type> systemTypes)
        {
            for (int i = 0; i < systemTypes.Count; i++)
            {
                var systemType = systemTypes[i];
                if (!typeof(ISystem).IsAssignableFrom(systemType))
                {
                    throw new ArgumentException($"System type must be ISystem: {systemType}");
                }

                var addMethod = typeof(ContractTestWorld).GetMethod(nameof(ContractTestWorld.AddSystem))?.MakeGenericMethod(systemType);
                addMethod?.Invoke(world, Array.Empty<object>());
            }
        }

        private static void AssertFatalCounters(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(typeof(PureDOTS.Runtime.Logistics.Contracts.ContractInvariantCounters));
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var counters = query.GetSingleton<PureDOTS.Runtime.Logistics.Contracts.ContractInvariantCounters>();
            if (counters.NegativeInventoryCount > 0 ||
                counters.ReservedOverAvailableCount > 0 ||
                counters.IllegalStateTransitionCount > 0 ||
                counters.CommitWithoutHoldCount > 0)
            {
                var violations = DumpViolations(entityManager, 32);
                Assert.Fail($"Contract fatal counters tripped.\n{violations}");
            }
        }

        private static string DumpViolations(EntityManager entityManager, int maxCount)
        {
            var query = entityManager.CreateEntityQuery(
                typeof(PureDOTS.Runtime.Contracts.ContractViolationStream),
                typeof(PureDOTS.Runtime.Contracts.ContractViolationRingState),
                typeof(PureDOTS.Runtime.Contracts.ContractViolationEvent));
            if (query.IsEmptyIgnoreFilter)
            {
                return "No violation ring buffer.";
            }

            var entity = query.GetSingletonEntity();
            var ringState = entityManager.GetComponentData<PureDOTS.Runtime.Contracts.ContractViolationRingState>(entity);
            var buffer = entityManager.GetBuffer<PureDOTS.Runtime.Contracts.ContractViolationEvent>(entity);
            if (buffer.Length == 0 || ringState.Capacity <= 0)
            {
                return "Violation ring buffer empty.";
            }

            var count = System.Math.Min(maxCount, ringState.Capacity);
            var start = ringState.WriteIndex - count;
            if (start < 0)
            {
                start += ringState.Capacity;
            }

            var output = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                var index = (start + i) % ringState.Capacity;
                var entry = buffer[index];
                if (entry.Tick == 0 && entry.ReservationId == 0 && entry.Subject == Entity.Null)
                {
                    continue;
                }

                output.Append('[')
                    .Append(entry.Tick)
                    .Append("] ")
                    .Append(entry.ContractId.ToString())
                    .Append(" reason=")
                    .Append(entry.Reason)
                    .Append(" entity=")
                    .Append(entry.Subject.Index)
                    .Append(" reservation=")
                    .Append(entry.ReservationId)
                    .AppendLine();
            }

            return output.ToString();
        }
    }
}
#endif

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using PureDOTS.Runtime.Production.Contracts;
using PureDOTS.Systems.Production.Contracts;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace PureDOTS.Tests.Contracts
{
    public sealed class QualityContractSuite : IContractSuite
    {
        private Entity _entity;

        public string ContractId => "QUALITY.ITEM.V1";
        public int StepCount => 2;

        public IReadOnlyList<Type> SystemTypes => new[]
        {
            typeof(ContractQualityAggregationSystem)
        };

        public void Setup(World world)
        {
        }

        public void Seed(EntityManager entityManager)
        {
            _entity = entityManager.CreateEntity(typeof(ContractQualityRequest), typeof(ContractQualityResult));
            entityManager.SetComponentData(_entity, new ContractQualityRequest
            {
                InputA = 0.2f,
                InputB = 0.8f,
                WeightA = 1f,
                WeightB = 3f,
                MinValue = 0f,
                MaxValue = 1f,
                LastProcessedTick = 0
            });
            entityManager.SetComponentData(_entity, new ContractQualityResult());
        }

        public void Step(World world, uint tick)
        {
        }

        public void Assert(EntityManager entityManager)
        {
            var result = entityManager.GetComponentData<ContractQualityResult>(_entity);
            NUnitAssert.That(result.OutputValue, Is.InRange(0f, 1f));

            var request = entityManager.GetComponentData<ContractQualityRequest>(_entity);
            NUnitAssert.That(request.LastProcessedTick, Is.GreaterThan(0u));
            NUnitAssert.That(result.LastProcessedTick, Is.EqualTo(request.LastProcessedTick));
        }
    }
}
#endif

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using PureDOTS.Runtime.Needs.Contracts;
using PureDOTS.Systems.Needs.Contracts;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace PureDOTS.Tests.Contracts
{
    public sealed class NeedsContractSuite : IContractSuite
    {
        private Entity _entity;

        public string ContractId => "NEEDS.CORE.V1";
        public int StepCount => 3;

        public IReadOnlyList<Type> SystemTypes => new[]
        {
            typeof(ContractNeedOverrideSystem)
        };

        public void Setup(World world)
        {
        }

        public void Seed(EntityManager entityManager)
        {
            _entity = entityManager.CreateEntity(typeof(ContractNeedState), typeof(ContractNeedOverridePolicy), typeof(ContractNeedOverrideIntent));
            entityManager.SetComponentData(_entity, new ContractNeedState
            {
                Hunger = 0.9f,
                Health = 1.0f,
                Rest = 0.5f,
                Morale = 0.5f
            });
            entityManager.SetComponentData(_entity, new ContractNeedOverridePolicy
            {
                CriticalHunger = 0.8f,
                CriticalHealth = 0.2f,
                MinStableTicks = 2
            });
            entityManager.SetComponentData(_entity, new ContractNeedOverrideIntent
            {
                Active = 0,
                Reason = 0,
                LastChangedTick = 0
            });
        }

        public void Step(World world, uint tick)
        {
            if (tick == 2)
            {
                var needs = world.EntityManager.GetComponentData<ContractNeedState>(_entity);
                needs.Hunger = 0.1f;
                world.EntityManager.SetComponentData(_entity, needs);
            }
        }

        public void Assert(EntityManager entityManager)
        {
            var intent = entityManager.GetComponentData<ContractNeedOverrideIntent>(_entity);
            NUnitAssert.That(intent.LastChangedTick, Is.GreaterThan(0u));
        }
    }
}
#endif

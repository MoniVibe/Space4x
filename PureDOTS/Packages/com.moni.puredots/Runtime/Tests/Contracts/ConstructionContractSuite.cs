#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using PureDOTS.Runtime.Construction.Contracts;
using PureDOTS.Runtime.Logistics.Contracts;
using PureDOTS.Systems.Construction.Contracts;
using PureDOTS.Systems.Logistics.Contracts;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace PureDOTS.Tests.Contracts
{
    public sealed class ConstructionContractSuite : IContractSuite
    {
        private Entity _ledger;
        private Entity _site;
        private const int StoneId = 3;

        public string ContractId => "CONSTRUCTION.LOOP.V1";
        public int StepCount => 4;

        public IReadOnlyList<Type> SystemTypes => new[]
        {
            typeof(ContractReservationLedgerSystem),
            typeof(ContractConstructionStateSystem)
        };

        public void Setup(World world)
        {
        }

        public void Seed(EntityManager entityManager)
        {
            _ledger = entityManager.CreateEntity(typeof(ContractReservationLedgerState), typeof(ContractInvariantCounters));
            entityManager.SetComponentData(_ledger, new ContractReservationLedgerState { NextReservationId = 1 });
            entityManager.SetComponentData(_ledger, new ContractInvariantCounters());
            entityManager.AddBuffer<ContractReservationLedgerEntry>(_ledger);

            _site = entityManager.CreateEntity(typeof(ContractConstructionSite));
            var requirements = entityManager.AddBuffer<ContractConstructionRequirement>(_site);
            requirements.Add(new ContractConstructionRequirement { ResourceId = StoneId, Amount = 2 });
            entityManager.SetComponentData(_site, new ContractConstructionSite
            {
                State = ContractConstructionState.Planned,
                StateTick = 0
            });

            var ledger = entityManager.GetBuffer<ContractReservationLedgerEntry>(_ledger);
            ledger.Add(new ContractReservationLedgerEntry
            {
                ReservationId = 1,
                ResourceId = StoneId,
                Amount = 2,
                Owner = _site,
                State = ReservationState.Held,
                ExpireTick = 0,
                CommittedTick = 0,
                LastStateTick = 0
            });
        }

        public void Step(World world, uint tick)
        {
            if (tick == 3)
            {
                world.EntityManager.AddComponent<ContractConstructionCancel>(_site);
            }
        }

        public void Assert(EntityManager entityManager)
        {
            var site = entityManager.GetComponentData<ContractConstructionSite>(_site);
            NUnitAssert.That(site.State, Is.EqualTo(ContractConstructionState.Cancelled).Or.EqualTo(ContractConstructionState.Complete));
        }
    }
}
#endif

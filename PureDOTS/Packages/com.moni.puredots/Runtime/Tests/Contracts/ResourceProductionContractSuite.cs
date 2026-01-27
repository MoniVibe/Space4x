#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using PureDOTS.Runtime.Logistics.Contracts;
using PureDOTS.Runtime.Production.Contracts;
using PureDOTS.Systems.Logistics.Contracts;
using PureDOTS.Systems.Production.Contracts;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace PureDOTS.Tests.Contracts
{
    public sealed class ResourceProductionContractSuite : IContractSuite
    {
        private Entity _owner;
        private Entity _owner2;
        private Entity _ledger;
        private const int OreId = 1;
        private const int IngotId = 2;
        private const int FuelId = 99;

        public string ContractId => "RESOURCE.LEDGER.V1+PRODUCTION.ACCOUNTING.V1";
        public int StepCount => 4;

        public IReadOnlyList<Type> SystemTypes => new[]
        {
            typeof(ContractReservationRequestSystem),
            typeof(ContractReservationLedgerSystem),
            typeof(ContractProductionReducerSystem),
            typeof(ContractLedgerInvariantSystem)
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

            _owner = entityManager.CreateEntity(typeof(ContractProductionRequest), typeof(ContractProductionResult));
            var inventory = entityManager.AddBuffer<ContractInventory>(_owner);
            inventory.Add(new ContractInventory { ResourceId = OreId, Amount = 10 });

            entityManager.AddBuffer<ContractReservationRequest>(_owner);
            entityManager.SetComponentData(_owner, new ContractProductionRequest
            {
                InputResourceId = OreId,
                InputAmount = 5,
                OutputResourceId = IngotId,
                OutputAmount = 2,
                RecipeId = 1,
                LastProcessedTick = 0
            });
            entityManager.SetComponentData(_owner, new ContractProductionResult());

            _owner2 = entityManager.CreateEntity(typeof(ContractProductionRequest), typeof(ContractProductionResult));
            var inventory2 = entityManager.AddBuffer<ContractInventory>(_owner2);
            inventory2.Add(new ContractInventory { ResourceId = FuelId, Amount = 1 });
            entityManager.SetComponentData(_owner2, new ContractProductionRequest
            {
                InputResourceId = FuelId,
                InputAmount = 1,
                OutputResourceId = IngotId,
                OutputAmount = 1,
                RecipeId = 2,
                LastProcessedTick = 0
            });
            entityManager.SetComponentData(_owner2, new ContractProductionResult());
        }

        public void Step(World world, uint tick)
        {
            var entityManager = world.EntityManager;
            var requests = entityManager.GetBuffer<ContractReservationRequest>(_owner);
            if (tick == 1)
            {
                requests.Add(new ContractReservationRequest
                {
                    ResourceId = OreId,
                    Amount = 5,
                    Requester = _owner,
                    Purpose = 1,
                    ExpireTick = tick + 2
                });
            }

            if (tick == 2)
            {
                requests.Add(new ContractReservationRequest
                {
                    ResourceId = OreId,
                    Amount = 10,
                    Requester = _owner,
                    Purpose = 2,
                    ExpireTick = tick + 1
                });
            }

            if (tick == 3)
            {
                var production = entityManager.GetComponentData<ContractProductionRequest>(_owner);
                production.LastProcessedTick = 0;
                entityManager.SetComponentData(_owner, production);

                var ledger = entityManager.GetBuffer<ContractReservationLedgerEntry>(_ledger);
                ledger.Add(new ContractReservationLedgerEntry
                {
                    ReservationId = 2,
                    ResourceId = OreId,
                    Amount = 1,
                    Owner = _owner,
                    State = ReservationState.Held,
                    ExpireTick = 0,
                    CommittedTick = 0,
                    LastStateTick = 1
                });
                ledger.Add(new ContractReservationLedgerEntry
                {
                    ReservationId = 2,
                    ResourceId = OreId,
                    Amount = 1,
                    Owner = _owner,
                    State = ReservationState.Held,
                    ExpireTick = 0,
                    CommittedTick = 0,
                    LastStateTick = 1
                });
                ledger.Add(new ContractReservationLedgerEntry
                {
                    ReservationId = 3,
                    ResourceId = OreId,
                    Amount = 1,
                    Owner = _owner,
                    State = ReservationState.Held,
                    ExpireTick = 0,
                    CommittedTick = 2,
                    LastStateTick = 1
                });
                ledger.Add(new ContractReservationLedgerEntry
                {
                    ReservationId = 4,
                    ResourceId = OreId,
                    Amount = 1,
                    Owner = _owner,
                    State = ReservationState.Released,
                    ExpireTick = 0,
                    CommittedTick = 0,
                    LastStateTick = 1
                });
                ledger.Add(new ContractReservationLedgerEntry
                {
                    ReservationId = 5,
                    ResourceId = FuelId,
                    Amount = 1,
                    Owner = _owner2,
                    State = ReservationState.Released,
                    ExpireTick = 0,
                    CommittedTick = 0,
                    LastStateTick = 1
                });
            }
        }

        public void Assert(EntityManager entityManager)
        {
            var inventory = entityManager.GetBuffer<ContractInventory>(_owner);
            NUnitAssert.That(GetAmount(inventory, OreId), Is.GreaterThanOrEqualTo(0));
            NUnitAssert.That(GetAmount(inventory, IngotId), Is.GreaterThanOrEqualTo(0));

            var counters = entityManager.GetComponentData<ContractInvariantCounters>(_ledger);
            NUnitAssert.That(counters.NegativeInventoryCount, Is.EqualTo(0));
            NUnitAssert.That(counters.ReservedOverAvailableCount, Is.EqualTo(0));
            NUnitAssert.That(counters.DoubleCommitAttemptCount, Is.GreaterThanOrEqualTo(1));
            NUnitAssert.That(counters.DuplicateReservationIdCount, Is.GreaterThanOrEqualTo(1));
            NUnitAssert.That(counters.IllegalStateTransitionCount, Is.GreaterThanOrEqualTo(1));
            NUnitAssert.That(counters.CommitWithoutHoldCount, Is.GreaterThanOrEqualTo(1));

            var result = entityManager.GetComponentData<ContractProductionResult>(_owner);
            NUnitAssert.That(result.LastProcessedTick, Is.GreaterThan(0u));

            var ledger = entityManager.GetBuffer<ContractReservationLedgerEntry>(_ledger);
            var hasReleased = false;
            for (int i = 0; i < ledger.Length; i++)
            {
                if (ledger[i].State == ReservationState.Released)
                {
                    hasReleased = true;
                    break;
                }
            }

            NUnitAssert.That(hasReleased, Is.True);
        }

        private static int GetAmount(DynamicBuffer<ContractInventory> inventory, int resourceId)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i].ResourceId == resourceId)
                {
                    return inventory[i].Amount;
                }
            }

            return 0;
        }
    }
}
#endif

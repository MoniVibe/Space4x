using System;
using System.Collections.Generic;
using Unity.Entities;

namespace PureDOTS.Tests.Contracts
{
    public interface IContractSuite
    {
        string ContractId { get; }
        int StepCount { get; }
        IReadOnlyList<Type> SystemTypes { get; }

        void Setup(World world);
        void Seed(EntityManager entityManager);
        void Step(World world, uint tick);
        void Assert(EntityManager entityManager);
    }
}

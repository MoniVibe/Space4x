#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Entities;

namespace Space4X.Tests
{
    public class Space4XCraftCustomizationSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Space4XCraftCustomizationSystemTests");
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated)
            {
                _world.Dispose();
            }
        }

        [Test]
        public void OverMassStrikeCraft_LoadoutFlagsViolation_AndReducesSpeedMultiplier()
        {
            var craft = _entityManager.CreateEntity(typeof(StrikeCraftProfile), typeof(VesselMovement));
            _entityManager.SetComponentData(craft, StrikeCraftProfile.Create(StrikeCraftRole.Bomber, Entity.Null));
            _entityManager.SetComponentData(craft, new VesselMovement
            {
                BaseSpeed = 80f,
                CurrentSpeed = 80f,
                Acceleration = 20f,
                Deceleration = 20f,
                TurnSpeed = 2f,
                SlowdownDistance = 30f,
                ArrivalDistance = 5f
            });

            RunBootstrap();

            var profile = _entityManager.GetComponentData<NonCapitalCraftProfile>(craft);
            profile.MaxMassTons = 30f;
            profile.BaseMassTons = 20f;
            _entityManager.SetComponentData(craft, profile);

            var internals = _entityManager.GetComponentData<CraftInternalState>(craft);
            internals.CoreIntegrity = 1f;
            internals.EngineIntegrity = 1f;
            internals.AvionicsIntegrity = 1f;
            internals.LifeSupportIntegrity = 1f;
            internals.InternalMassTons = 12f;
            _entityManager.SetComponentData(craft, internals);

            var modules = _entityManager.GetBuffer<CraftModuleInstance>(craft);
            modules.Add(new CraftModuleInstance
            {
                SlotId = 1,
                Category = CraftModuleCategory.Weapon,
                ModuleId = "torp-rack-heavy",
                MassTons = 20f,
                HeatLoad = 0.45f,
                Reliability = 0.7f
            });
            modules.Add(new CraftModuleInstance
            {
                SlotId = 2,
                Category = CraftModuleCategory.Weapon,
                ModuleId = "torp-rack-heavy-2",
                MassTons = 18f,
                HeatLoad = 0.45f,
                Reliability = 0.7f
            });

            RunCustomization();

            var aggregate = _entityManager.GetComponentData<CraftLoadoutAggregate>(craft);
            Assert.IsTrue((aggregate.Violations & CraftBuildViolation.OverMass) != 0, "Overweight loadouts should be flagged.");
            Assert.Less(aggregate.EffectiveSpeedMultiplier, 1f, "Overweight loadouts should lose speed.");
        }

        [Test]
        public void MiningCraft_LoadoutProjectsToMiningAndMovementStats()
        {
            var miner = _entityManager.CreateEntity(typeof(MiningVessel), typeof(VesselMovement));
            _entityManager.SetComponentData(miner, new MiningVessel
            {
                VesselId = "miner-test",
                CarrierEntity = Entity.Null,
                MiningEfficiency = 1f,
                Speed = 10f,
                CargoCapacity = 50f,
                CurrentCargo = 0f
            });
            _entityManager.SetComponentData(miner, new VesselMovement
            {
                BaseSpeed = 12f,
                CurrentSpeed = 12f,
                Acceleration = 6f,
                Deceleration = 6f,
                TurnSpeed = 1.5f,
                SlowdownDistance = 15f,
                ArrivalDistance = 2f
            });

            RunBootstrap();

            var profile = _entityManager.GetComponentData<NonCapitalCraftProfile>(miner);
            profile.MaxMassTons = 120f;
            profile.BaseMassTons = 30f;
            _entityManager.SetComponentData(miner, profile);

            var internals = _entityManager.GetComponentData<CraftInternalState>(miner);
            internals.CoreIntegrity = 1f;
            internals.EngineIntegrity = 1f;
            internals.AvionicsIntegrity = 1f;
            internals.LifeSupportIntegrity = 1f;
            internals.InternalMassTons = 8f;
            internals.BaseHeatLoad = 0.15f;
            internals.HeatDissipation = 1.1f;
            _entityManager.SetComponentData(miner, internals);

            var modules = _entityManager.GetBuffer<CraftModuleInstance>(miner);
            modules.Add(new CraftModuleInstance
            {
                SlotId = 1,
                Category = CraftModuleCategory.MiningTool,
                ModuleId = "laser-stripper",
                MassTons = 8f,
                MiningYieldBonus = 0.6f,
                HeatLoad = 0.2f,
                Reliability = 0.8f
            });
            modules.Add(new CraftModuleInstance
            {
                SlotId = 3,
                Category = CraftModuleCategory.Cargo,
                ModuleId = "expanded-bays",
                MassTons = 10f,
                CargoBonus = 0.5f,
                TransferBonus = 0.25f,
                Reliability = 0.75f
            });
            modules.Add(new CraftModuleInstance
            {
                SlotId = 0,
                Category = CraftModuleCategory.Propulsion,
                ModuleId = "vectored-thrusters",
                MassTons = 6f,
                ThrustBonus = 0.4f,
                HeatLoad = 0.1f,
                HeatDissipation = 0.25f,
                Reliability = 0.9f
            });

            RunCustomization();

            var minerAfter = _entityManager.GetComponentData<MiningVessel>(miner);
            var movementAfter = _entityManager.GetComponentData<VesselMovement>(miner);
            var aggregate = _entityManager.GetComponentData<CraftLoadoutAggregate>(miner);

            Assert.Greater(aggregate.MiningYieldMultiplier, 1f);
            Assert.Greater(aggregate.CargoMultiplier, 1f);
            Assert.Greater(minerAfter.MiningEfficiency, 1f, "Mining efficiency should scale from loadout modules.");
            Assert.Greater(minerAfter.CargoCapacity, 50f, "Cargo capacity should scale from cargo modules.");
            Assert.Greater(movementAfter.BaseSpeed, 12f, "Movement base speed should scale from propulsion/thrust modules.");
        }

        private void RunBootstrap()
        {
            var system = _world.GetOrCreateSystem<Space4XCraftCustomizationBootstrapSystem>();
            system.Update(_world.Unmanaged);
        }

        private void RunCustomization()
        {
            var system = _world.GetOrCreateSystem<Space4XCraftCustomizationSystem>();
            system.Update(_world.Unmanaged);
        }
    }
}
#endif

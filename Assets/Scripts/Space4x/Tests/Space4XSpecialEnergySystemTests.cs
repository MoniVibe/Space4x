#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Systems.Power;
using Space4X.Tests.TestHarness;
using Unity.Entities;

namespace Space4X.Tests
{
    public sealed class Space4XSpecialEnergySystemTests
    {
        [Test]
        public void SpecialEnergySystem_RegenScalesWithReactor()
        {
            using var harness = new ISystemTestHarness();
            var em = harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(em);

            var timeEntity = em.CreateEntityQuery(ComponentType.ReadWrite<PureDOTS.Runtime.Components.TimeState>()).GetSingletonEntity();
            var time = em.GetComponentData<PureDOTS.Runtime.Components.TimeState>(timeEntity);
            time.FixedDeltaTime = 0.5f;
            em.SetComponentData(timeEntity, time);

            var ship = em.CreateEntity(
                typeof(ShipReactorSpec),
                typeof(ShipSpecialEnergyConfig),
                typeof(ShipSpecialEnergyState));
            em.SetComponentData(ship, new ShipReactorSpec
            {
                OutputMW = 1000f,
                Efficiency = 0.8f
            });
            em.SetComponentData(ship, new ShipSpecialEnergyConfig
            {
                BaseMax = 50f,
                BaseRegenPerSecond = 2f,
                ReactorOutputToMax = 0.01f,
                ReactorOutputToRegen = 0.002f,
                ReactorEfficiencyRegenMultiplier = 1f,
                RestartRegenPenaltyMultiplier = 0.2f,
                ActivationCostMultiplier = 1f
            });
            em.SetComponentData(ship, new ShipSpecialEnergyState
            {
                Current = 10f,
                EffectiveMax = 0f,
                EffectiveRegenPerSecond = 0f
            });
            em.AddBuffer<ShipSpecialEnergyPassiveModifier>(ship);
            em.AddBuffer<ShipSpecialEnergySpendRequest>(ship);

            var system = harness.World.GetOrCreateSystem<Space4XShipSpecialEnergySystem>();
            system.Update(harness.World.Unmanaged);

            var energy = em.GetComponentData<ShipSpecialEnergyState>(ship);
            Assert.AreEqual(60f, energy.EffectiveMax, 1e-4f);
            Assert.AreEqual(3.2f, energy.EffectiveRegenPerSecond, 1e-4f);
            Assert.AreEqual(11.6f, energy.Current, 1e-4f);
        }

        [Test]
        public void SpecialEnergySystem_PassiveCapReduction_ClampsCurrentAndTracksFailedSpends()
        {
            using var harness = new ISystemTestHarness();
            var em = harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(em);

            var ship = em.CreateEntity(
                typeof(ShipReactorSpec),
                typeof(ShipSpecialEnergyConfig),
                typeof(ShipSpecialEnergyState));
            em.SetComponentData(ship, new ShipReactorSpec
            {
                OutputMW = 0f,
                Efficiency = 1f
            });
            em.SetComponentData(ship, new ShipSpecialEnergyConfig
            {
                BaseMax = 80f,
                BaseRegenPerSecond = 0f,
                ReactorOutputToMax = 0f,
                ReactorOutputToRegen = 0f,
                ReactorEfficiencyRegenMultiplier = 1f,
                RestartRegenPenaltyMultiplier = 0.2f,
                ActivationCostMultiplier = 1f
            });
            em.SetComponentData(ship, new ShipSpecialEnergyState
            {
                Current = 90f,
                EffectiveMax = 0f,
                EffectiveRegenPerSecond = 0f
            });

            var modifiers = em.AddBuffer<ShipSpecialEnergyPassiveModifier>(ship);
            modifiers.Add(new ShipSpecialEnergyPassiveModifier
            {
                AdditiveMax = -30f,
                MultiplicativeMax = 1f,
                AdditiveRegenPerSecond = 0f,
                MultiplicativeRegen = 1f
            });

            var spends = em.AddBuffer<ShipSpecialEnergySpendRequest>(ship);
            spends.Add(new ShipSpecialEnergySpendRequest
            {
                Amount = 60f
            });

            var system = harness.World.GetOrCreateSystem<Space4XShipSpecialEnergySystem>();
            system.Update(harness.World.Unmanaged);

            var energy = em.GetComponentData<ShipSpecialEnergyState>(ship);
            Assert.AreEqual(50f, energy.EffectiveMax, 1e-4f);
            Assert.AreEqual(50f, energy.Current, 1e-4f);
            Assert.AreEqual(1, energy.FailedSpendAttempts);
        }

        [Test]
        public void FocusAbilitySystem_ConsumesSpecialEnergyWhenPresent()
        {
            using var harness = new ISystemTestHarness();
            var em = harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(em);

            var actor = em.CreateEntity(
                typeof(Space4XEntityFocus),
                typeof(OfficerFocusProfile),
                typeof(ShipSpecialEnergyState));
            em.SetComponentData(actor, Space4XEntityFocus.Default(100f));
            em.SetComponentData(actor, OfficerFocusProfile.Captain());
            em.SetComponentData(actor, new ShipSpecialEnergyState
            {
                Current = 1f,
                EffectiveMax = 20f,
                EffectiveRegenPerSecond = 0f
            });
            em.AddBuffer<Space4XActiveFocusAbility>(actor);

            em.AddComponentData(actor, new FocusAbilityRequest
            {
                RequestedAbility = (ushort)Space4XFocusAbilityType.OfficerSupport
            });

            var system = harness.World.GetOrCreateSystem<Space4XFocusAbilitySystem>();
            system.Update(harness.World.Unmanaged);

            var abilitiesAfterFail = em.GetBuffer<Space4XActiveFocusAbility>(actor);
            var energyAfterFail = em.GetComponentData<ShipSpecialEnergyState>(actor);
            Assert.AreEqual(0, abilitiesAfterFail.Length, "Ability should not activate when special energy is insufficient.");
            Assert.AreEqual(1, energyAfterFail.FailedSpendAttempts);
            Assert.IsFalse(em.HasComponent<FocusAbilityRequest>(actor), "Failed request should be consumed.");

            var requiredCost = Space4XFocusAbilityDefinitions.GetSpecialEnergyActivationCost(Space4XFocusAbilityType.OfficerSupport);
            energyAfterFail.Current = requiredCost + 1f;
            em.SetComponentData(actor, energyAfterFail);
            em.AddComponentData(actor, new FocusAbilityRequest
            {
                RequestedAbility = (ushort)Space4XFocusAbilityType.OfficerSupport
            });

            system.Update(harness.World.Unmanaged);

            var abilitiesAfterSuccess = em.GetBuffer<Space4XActiveFocusAbility>(actor);
            var energyAfterSuccess = em.GetComponentData<ShipSpecialEnergyState>(actor);
            Assert.AreEqual(1, abilitiesAfterSuccess.Length, "Ability should activate when special energy is available.");
            Assert.AreEqual(1f, energyAfterSuccess.Current, 1e-3f);
            Assert.IsFalse(em.HasComponent<FocusAbilityRequest>(actor), "Successful request should be consumed.");
        }
    }
}
#endif

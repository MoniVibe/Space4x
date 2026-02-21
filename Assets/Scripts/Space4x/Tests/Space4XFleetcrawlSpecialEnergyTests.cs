#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.Tests.TestHarness;
using Space4x.Scenario;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    public sealed class Space4XFleetcrawlSpecialEnergyTests
    {
        [Test]
        public void FleetcrawlSpecialAbility_ConsumesSpecialEnergyAndDealsDamage()
        {
            using var harness = new ISystemTestHarness();
            var em = harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(em);

            var flagship = em.CreateEntity(
                typeof(LocalTransform),
                typeof(Space4XFleetcrawlPlayerDirective),
                typeof(PlayerFlagshipTag),
                typeof(Space4XRunPlayerTag),
                typeof(ShipSpecialEnergyState));
            em.SetComponentData(flagship, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            em.SetComponentData(flagship, new Space4XFleetcrawlPlayerDirective
            {
                SpecialRequested = 1,
                SpecialCooldownUntilTick = 0
            });
            em.SetComponentData(flagship, new ShipSpecialEnergyState
            {
                Current = Space4XFleetcrawlSpecialEnergyRules.SpecialAbilityCost + 5f,
                EffectiveMax = 100f
            });

            var enemy = em.CreateEntity(
                typeof(LocalTransform),
                typeof(HullIntegrity),
                typeof(ScenarioSide),
                typeof(Space4XRunEnemyTag));
            em.SetComponentData(enemy, new LocalTransform
            {
                Position = new float3(0f, 0f, 10f),
                Rotation = quaternion.identity,
                Scale = 1f
            });
            em.SetComponentData(enemy, HullIntegrity.HeavyCarrier);
            em.SetComponentData(enemy, new ScenarioSide { Side = 1 });
            em.SetComponentData(enemy, new Space4XRunEnemyTag
            {
                RoomIndex = 0,
                WaveIndex = 0,
                EnemyClass = Space4XFleetcrawlEnemyClass.Normal
            });

            var system = harness.World.GetOrCreateSystem<Space4XFleetcrawlSpecialAbilitySystem>();
            system.Update(harness.World.Unmanaged);

            var energy = em.GetComponentData<ShipSpecialEnergyState>(flagship);
            var directive = em.GetComponentData<Space4XFleetcrawlPlayerDirective>(flagship);
            var damage = em.GetBuffer<DamageEvent>(enemy);

            Assert.AreEqual(5f, energy.Current, 1e-4f);
            Assert.AreEqual(0, directive.SpecialRequested);
            Assert.Greater(damage.Length, 0, "Special ability should emit damage events when energy is available.");
        }

        [Test]
        public void FleetcrawlSpecialAbility_DeniesWhenEnergyInsufficient()
        {
            using var harness = new ISystemTestHarness();
            var em = harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(em);

            var flagship = em.CreateEntity(
                typeof(LocalTransform),
                typeof(Space4XFleetcrawlPlayerDirective),
                typeof(PlayerFlagshipTag),
                typeof(Space4XRunPlayerTag),
                typeof(ShipSpecialEnergyState));
            em.SetComponentData(flagship, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            em.SetComponentData(flagship, new Space4XFleetcrawlPlayerDirective
            {
                SpecialRequested = 1,
                SpecialCooldownUntilTick = 0
            });
            em.SetComponentData(flagship, new ShipSpecialEnergyState
            {
                Current = 0f,
                EffectiveMax = 100f
            });

            var enemy = em.CreateEntity(
                typeof(LocalTransform),
                typeof(HullIntegrity),
                typeof(ScenarioSide),
                typeof(Space4XRunEnemyTag));
            em.SetComponentData(enemy, new LocalTransform
            {
                Position = new float3(0f, 0f, 10f),
                Rotation = quaternion.identity,
                Scale = 1f
            });
            em.SetComponentData(enemy, HullIntegrity.HeavyCarrier);
            em.SetComponentData(enemy, new ScenarioSide { Side = 1 });
            em.SetComponentData(enemy, new Space4XRunEnemyTag
            {
                RoomIndex = 0,
                WaveIndex = 0,
                EnemyClass = Space4XFleetcrawlEnemyClass.Normal
            });

            var system = harness.World.GetOrCreateSystem<Space4XFleetcrawlSpecialAbilitySystem>();
            system.Update(harness.World.Unmanaged);

            var energy = em.GetComponentData<ShipSpecialEnergyState>(flagship);
            var directive = em.GetComponentData<Space4XFleetcrawlPlayerDirective>(flagship);
            var hasDamageBuffer = em.HasBuffer<DamageEvent>(enemy);

            Assert.AreEqual(0, directive.SpecialRequested);
            Assert.AreEqual(1, energy.FailedSpendAttempts);
            Assert.IsFalse(hasDamageBuffer, "Special ability should not fire without sufficient energy.");
        }
    }
}
#endif

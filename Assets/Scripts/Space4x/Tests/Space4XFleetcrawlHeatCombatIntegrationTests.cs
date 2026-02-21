#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Tests.TestHarness;
using Space4x.Fleetcrawl;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Tests
{
    public sealed class Space4XFleetcrawlHeatCombatIntegrationTests
    {
        [Test]
        public void WeaponSystem_ConservativeOverheat_SuppressesFire()
        {
            using var harness = new ISystemTestHarness(fixedDelta: 0.1f);
            var em = harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(em);

            var weaponSystem = harness.World.GetOrCreateSystem<Space4XWeaponSystem>();
            harness.Sim.AddSystemToUpdateList(weaponSystem);

            var target = em.CreateEntity(typeof(LocalTransform));
            em.SetComponentData(target, new LocalTransform
            {
                Position = new float3(0f, 0f, 5f),
                Rotation = quaternion.identity,
                Scale = 1f
            });

            var ship = em.CreateEntity(
                typeof(LocalTransform),
                typeof(Space4XEngagement),
                typeof(SupplyStatus),
                typeof(FleetcrawlHeatRuntimeState),
                typeof(FleetcrawlHeatsinkState),
                typeof(FleetcrawlHeatControlState),
                typeof(FleetcrawlHeatOutputState));
            em.SetComponentData(ship, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            em.SetComponentData(ship, new Space4XEngagement
            {
                PrimaryTarget = target,
                Phase = EngagementPhase.Engaged
            });
            em.SetComponentData(ship, new SupplyStatus
            {
                Ammunition = 100f,
                AmmunitionCapacity = 100f
            });
            em.SetComponentData(ship, new FleetcrawlHeatRuntimeState
            {
                CurrentHeat = 100f,
                BaseHeatCapacity = 100f,
                BaseDissipationPerTick = 0f,
                BaseOverheatThreshold01 = 0.8f,
                BaseRecoveryThreshold01 = 0.4f
            });
            em.SetComponentData(ship, new FleetcrawlHeatsinkState
            {
                StoredHeat = 0f,
                BaseCapacity = 0f,
                BaseAbsorbPerTick = 0f,
                BaseVentPerTick = 0f
            });
            em.SetComponentData(ship, new FleetcrawlHeatControlState
            {
                SafetyMode = FleetcrawlHeatSafetyMode.ConservativeThrottle,
                HeatsinkEnabled = 0
            });
            em.SetComponentData(ship, new FleetcrawlHeatOutputState
            {
                DamageMultiplier = 1f,
                CooldownMultiplier = 1f,
                FireRateThrottleMultiplier = 1f,
                EngineSpeedMultiplier = 1f,
                ShieldRechargeMultiplier = 1f,
                ShieldIntensityMultiplier = 1f
            });
            em.AddBuffer<FleetcrawlHeatActionEvent>(ship);

            var weapons = em.AddBuffer<WeaponMount>(ship);
            weapons.Add(new WeaponMount
            {
                IsEnabled = 1,
                Weapon = new Space4XWeapon
                {
                    Type = WeaponType.Laser,
                    Size = WeaponSize.Small,
                    BaseDamage = 5f,
                    OptimalRange = 5f,
                    MaxRange = 10f,
                    BaseAccuracy = (half)1f,
                    CooldownTicks = 2,
                    CurrentCooldown = 0,
                    AmmoPerShot = 0,
                    ShieldModifier = (half)1f,
                    ArmorPenetration = (half)0f
                },
                HeatCapacity = 100f,
                HeatDissipation = 0f,
                HeatPerShot = 2f,
                Heat01 = 0f
            });

            harness.Step(1);

            var after = em.GetBuffer<WeaponMount>(ship);
            var heatOutput = em.GetComponentData<FleetcrawlHeatOutputState>(ship);
            Assert.AreEqual(0, after[0].Weapon.CurrentCooldown, "Conservative overheat should suppress firing.");
            Assert.AreEqual(1, heatOutput.SuppressFire, "Heat output should signal suppressed fire.");
            Assert.AreEqual(1, heatOutput.IsOverheated, "Heat output should stay overheated.");
        }

        [Test]
        public void WeaponSystem_UnsafeOverheat_AllowsEventualFire()
        {
            using var harness = new ISystemTestHarness(fixedDelta: 0.1f);
            var em = harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(em);

            var weaponSystem = harness.World.GetOrCreateSystem<Space4XWeaponSystem>();
            harness.Sim.AddSystemToUpdateList(weaponSystem);

            var target = em.CreateEntity(typeof(LocalTransform));
            em.SetComponentData(target, new LocalTransform
            {
                Position = new float3(0f, 0f, 5f),
                Rotation = quaternion.identity,
                Scale = 1f
            });

            var ship = em.CreateEntity(
                typeof(LocalTransform),
                typeof(Space4XEngagement),
                typeof(SupplyStatus),
                typeof(FleetcrawlHeatRuntimeState),
                typeof(FleetcrawlHeatsinkState),
                typeof(FleetcrawlHeatControlState),
                typeof(FleetcrawlHeatOutputState));
            em.SetComponentData(ship, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            em.SetComponentData(ship, new Space4XEngagement
            {
                PrimaryTarget = target,
                Phase = EngagementPhase.Engaged
            });
            em.SetComponentData(ship, new SupplyStatus
            {
                Ammunition = 100f,
                AmmunitionCapacity = 100f
            });
            em.SetComponentData(ship, new FleetcrawlHeatRuntimeState
            {
                CurrentHeat = 95f,
                BaseHeatCapacity = 100f,
                BaseDissipationPerTick = 0f,
                BaseOverheatThreshold01 = 0.8f,
                BaseRecoveryThreshold01 = 0.4f
            });
            em.SetComponentData(ship, new FleetcrawlHeatsinkState
            {
                StoredHeat = 0f,
                BaseCapacity = 0f,
                BaseAbsorbPerTick = 0f,
                BaseVentPerTick = 0f
            });
            em.SetComponentData(ship, new FleetcrawlHeatControlState
            {
                SafetyMode = FleetcrawlHeatSafetyMode.UnsafeNoReduction,
                HeatsinkEnabled = 0
            });
            em.SetComponentData(ship, new FleetcrawlHeatOutputState
            {
                DamageMultiplier = 1f,
                CooldownMultiplier = 1f,
                FireRateThrottleMultiplier = 1f,
                EngineSpeedMultiplier = 1f,
                ShieldRechargeMultiplier = 1f,
                ShieldIntensityMultiplier = 1f
            });
            em.AddBuffer<FleetcrawlHeatActionEvent>(ship);

            var weapons = em.AddBuffer<WeaponMount>(ship);
            weapons.Add(new WeaponMount
            {
                IsEnabled = 1,
                Weapon = new Space4XWeapon
                {
                    Type = WeaponType.Laser,
                    Size = WeaponSize.Small,
                    BaseDamage = 5f,
                    OptimalRange = 5f,
                    MaxRange = 10f,
                    BaseAccuracy = (half)1f,
                    CooldownTicks = 2,
                    CurrentCooldown = 0,
                    AmmoPerShot = 0,
                    ShieldModifier = (half)1f,
                    ArmorPenetration = (half)0f
                },
                HeatCapacity = 100f,
                HeatDissipation = 0f,
                HeatPerShot = 2f,
                Heat01 = 0f
            });

            var fired = false;
            for (var i = 0; i < 20; i++)
            {
                harness.Step(1);
                var after = em.GetBuffer<WeaponMount>(ship);
                if (after[0].Weapon.CurrentCooldown > 0)
                {
                    fired = true;
                    break;
                }
            }

            Assert.IsTrue(fired, "Unsafe mode should allow eventual firing despite overheat.");
        }

        [Test]
        public void WeaponSystem_ResolvedHeatStats_ApplyToPlayerOnly()
        {
            using var harness = new ISystemTestHarness(fixedDelta: 0.1f);
            var em = harness.World.EntityManager;
            CoreSingletonBootstrapSystem.EnsureSingletons(em);

            var weaponSystem = harness.World.GetOrCreateSystem<Space4XWeaponSystem>();
            harness.Sim.AddSystemToUpdateList(weaponSystem);

            var target = em.CreateEntity(typeof(LocalTransform));
            em.SetComponentData(target, new LocalTransform
            {
                Position = new float3(0f, 0f, 5f),
                Rotation = quaternion.identity,
                Scale = 1f
            });

            var runtime = em.CreateEntity(typeof(FleetcrawlOfferRuntimeTag));
            var rolledLimbs = em.AddBuffer<FleetcrawlRolledLimbBufferElement>(runtime);
            var ownedItems = em.AddBuffer<FleetcrawlOwnedItem>(runtime);
            var heatDefs = em.AddBuffer<FleetcrawlHeatModifierDefinition>(runtime);
            rolledLimbs.Clear();
            ownedItems.Add(new FleetcrawlOwnedItem
            {
                Value = new FleetcrawlRolledItem
                {
                    ItemId = new FixedString64Bytes("item_hot_core")
                }
            });
            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("test_heat_item_boost"),
                SourceKind = FleetcrawlHeatModifierSourceKind.ItemId,
                SourceId = new FixedString64Bytes("item_hot_core"),
                HeatGenerationMultiplier = 2f
            });

            var player = em.CreateEntity(
                typeof(LocalTransform),
                typeof(Space4XEngagement),
                typeof(SupplyStatus),
                typeof(Space4XRunPlayerTag),
                typeof(FleetcrawlHeatRuntimeState),
                typeof(FleetcrawlHeatsinkState),
                typeof(FleetcrawlHeatControlState),
                typeof(FleetcrawlHeatOutputState));
            em.SetComponentData(player, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            em.SetComponentData(player, new Space4XEngagement
            {
                PrimaryTarget = target,
                Phase = EngagementPhase.Engaged
            });
            em.SetComponentData(player, new SupplyStatus
            {
                Ammunition = 100f,
                AmmunitionCapacity = 100f
            });
            em.SetComponentData(player, new FleetcrawlHeatRuntimeState
            {
                CurrentHeat = 70f,
                BaseHeatCapacity = 100f,
                BaseDissipationPerTick = 0f,
                BaseOverheatThreshold01 = 0.85f,
                BaseRecoveryThreshold01 = 0.4f
            });
            em.SetComponentData(player, new FleetcrawlHeatsinkState
            {
                StoredHeat = 0f,
                BaseCapacity = 0f,
                BaseAbsorbPerTick = 0f,
                BaseVentPerTick = 0f
            });
            em.SetComponentData(player, new FleetcrawlHeatControlState
            {
                SafetyMode = FleetcrawlHeatSafetyMode.ConservativeThrottle,
                HeatsinkEnabled = 0
            });
            em.SetComponentData(player, new FleetcrawlHeatOutputState
            {
                DamageMultiplier = 1f,
                CooldownMultiplier = 1f,
                FireRateThrottleMultiplier = 1f,
                EngineSpeedMultiplier = 1f,
                ShieldRechargeMultiplier = 1f,
                ShieldIntensityMultiplier = 1f
            });
            em.AddBuffer<FleetcrawlHeatActionEvent>(player);
            var playerWeapons = em.AddBuffer<WeaponMount>(player);
            playerWeapons.Add(new WeaponMount
            {
                IsEnabled = 1,
                Weapon = new Space4XWeapon
                {
                    Type = WeaponType.Laser,
                    Size = WeaponSize.Small,
                    BaseDamage = 5f,
                    OptimalRange = 5f,
                    MaxRange = 10f,
                    BaseAccuracy = (half)1f,
                    CooldownTicks = 1,
                    CurrentCooldown = 0,
                    AmmoPerShot = 0,
                    ShieldModifier = (half)1f,
                    ArmorPenetration = (half)0f
                },
                HeatCapacity = 100f,
                HeatDissipation = 0f,
                HeatPerShot = 10f,
                Heat01 = 0f
            });

            var nonPlayer = em.CreateEntity(
                typeof(LocalTransform),
                typeof(Space4XEngagement),
                typeof(SupplyStatus),
                typeof(FleetcrawlHeatRuntimeState),
                typeof(FleetcrawlHeatsinkState),
                typeof(FleetcrawlHeatControlState),
                typeof(FleetcrawlHeatOutputState));
            em.SetComponentData(nonPlayer, new LocalTransform
            {
                Position = new float3(0f, 0f, -1f),
                Rotation = quaternion.identity,
                Scale = 1f
            });
            em.SetComponentData(nonPlayer, new Space4XEngagement
            {
                PrimaryTarget = target,
                Phase = EngagementPhase.Engaged
            });
            em.SetComponentData(nonPlayer, new SupplyStatus
            {
                Ammunition = 100f,
                AmmunitionCapacity = 100f
            });
            em.SetComponentData(nonPlayer, new FleetcrawlHeatRuntimeState
            {
                CurrentHeat = 70f,
                BaseHeatCapacity = 100f,
                BaseDissipationPerTick = 0f,
                BaseOverheatThreshold01 = 0.85f,
                BaseRecoveryThreshold01 = 0.4f
            });
            em.SetComponentData(nonPlayer, new FleetcrawlHeatsinkState
            {
                StoredHeat = 0f,
                BaseCapacity = 0f,
                BaseAbsorbPerTick = 0f,
                BaseVentPerTick = 0f
            });
            em.SetComponentData(nonPlayer, new FleetcrawlHeatControlState
            {
                SafetyMode = FleetcrawlHeatSafetyMode.ConservativeThrottle,
                HeatsinkEnabled = 0
            });
            em.SetComponentData(nonPlayer, new FleetcrawlHeatOutputState
            {
                DamageMultiplier = 1f,
                CooldownMultiplier = 1f,
                FireRateThrottleMultiplier = 1f,
                EngineSpeedMultiplier = 1f,
                ShieldRechargeMultiplier = 1f,
                ShieldIntensityMultiplier = 1f
            });
            em.AddBuffer<FleetcrawlHeatActionEvent>(nonPlayer);
            var nonPlayerWeapons = em.AddBuffer<WeaponMount>(nonPlayer);
            nonPlayerWeapons.Add(new WeaponMount
            {
                IsEnabled = 1,
                Weapon = new Space4XWeapon
                {
                    Type = WeaponType.Laser,
                    Size = WeaponSize.Small,
                    BaseDamage = 5f,
                    OptimalRange = 5f,
                    MaxRange = 10f,
                    BaseAccuracy = (half)1f,
                    CooldownTicks = 1,
                    CurrentCooldown = 0,
                    AmmoPerShot = 0,
                    ShieldModifier = (half)1f,
                    ArmorPenetration = (half)0f
                },
                HeatCapacity = 100f,
                HeatDissipation = 0f,
                HeatPerShot = 10f,
                Heat01 = 0f
            });

            harness.Step(1);
            harness.Step(1);

            var playerHeat = em.GetComponentData<FleetcrawlHeatOutputState>(player);
            var nonPlayerHeat = em.GetComponentData<FleetcrawlHeatOutputState>(nonPlayer);
            Assert.AreEqual(1, playerHeat.IsOverheated, "Player heat should use resolved runtime multipliers.");
            Assert.AreEqual(1, playerHeat.SuppressFire, "Player heat should suppress fire after overheat.");
            Assert.AreEqual(0, nonPlayerHeat.IsOverheated, "Non-player heat should remain on identity stats.");
            Assert.AreEqual(0, nonPlayerHeat.SuppressFire, "Non-player heat should not be suppressed.");
            Assert.Greater(playerHeat.Heat01, nonPlayerHeat.Heat01, "Player heat should increase faster due to resolved modifiers.");
        }
    }
}
#endif

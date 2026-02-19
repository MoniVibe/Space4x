using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4x.Fleetcrawl
{
    [InternalBufferCapacity(16)]
    public struct FleetcrawlModuleUpgradeDefinition : IBufferElementData
    {
        public FleetcrawlModuleType ModuleType;
        public FleetcrawlComboTag RequiredTags;
        public float TurnRateMultiplier;
        public float AccelerationMultiplier;
        public float DecelerationMultiplier;
        public float MaxSpeedMultiplier;
        public float CooldownMultiplier;
        public float DamageMultiplier;
    }

    public struct FleetcrawlResolvedUpgradeStats
    {
        public float TurnRateMultiplier;
        public float AccelerationMultiplier;
        public float DecelerationMultiplier;
        public float MaxSpeedMultiplier;
        public float CooldownMultiplier;
        public float DamageMultiplier;

        public static FleetcrawlResolvedUpgradeStats Identity => new FleetcrawlResolvedUpgradeStats
        {
            TurnRateMultiplier = 1f,
            AccelerationMultiplier = 1f,
            DecelerationMultiplier = 1f,
            MaxSpeedMultiplier = 1f,
            CooldownMultiplier = 1f,
            DamageMultiplier = 1f
        };

        public void ApplyMultipliers(
            float turnRate,
            float acceleration,
            float deceleration,
            float maxSpeed,
            float cooldown,
            float damage)
        {
            TurnRateMultiplier *= math.max(0.01f, turnRate);
            AccelerationMultiplier *= math.max(0.01f, acceleration);
            DecelerationMultiplier *= math.max(0.01f, deceleration);
            MaxSpeedMultiplier *= math.max(0.01f, maxSpeed);
            CooldownMultiplier *= math.max(0.01f, cooldown);
            DamageMultiplier *= math.max(0.01f, damage);
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlLootShopBootstrapSystem))]
    public partial struct Space4XFleetcrawlModuleUpgradeBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FleetcrawlOfferRuntimeTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var runtimeEntity = SystemAPI.GetSingletonEntity<FleetcrawlOfferRuntimeTag>();
            var em = state.EntityManager;
            if (em.HasBuffer<FleetcrawlModuleUpgradeDefinition>(runtimeEntity))
            {
                return;
            }

            var defs = em.AddBuffer<FleetcrawlModuleUpgradeDefinition>(runtimeEntity);
            defs.Add(new FleetcrawlModuleUpgradeDefinition
            {
                ModuleType = FleetcrawlModuleType.Weapon,
                RequiredTags = FleetcrawlComboTag.Siege,
                TurnRateMultiplier = 0.98f,
                AccelerationMultiplier = 1f,
                DecelerationMultiplier = 1f,
                MaxSpeedMultiplier = 1f,
                CooldownMultiplier = 0.95f,
                DamageMultiplier = 1.08f
            });
            defs.Add(new FleetcrawlModuleUpgradeDefinition
            {
                ModuleType = FleetcrawlModuleType.Weapon,
                RequiredTags = FleetcrawlComboTag.Agile,
                TurnRateMultiplier = 1.1f,
                AccelerationMultiplier = 1.06f,
                DecelerationMultiplier = 1.02f,
                MaxSpeedMultiplier = 1.04f,
                CooldownMultiplier = 1f,
                DamageMultiplier = 1f
            });
            defs.Add(new FleetcrawlModuleUpgradeDefinition
            {
                ModuleType = FleetcrawlModuleType.Reactor,
                RequiredTags = FleetcrawlComboTag.Flux,
                TurnRateMultiplier = 1f,
                AccelerationMultiplier = 1.07f,
                DecelerationMultiplier = 1.06f,
                MaxSpeedMultiplier = 1.08f,
                CooldownMultiplier = 0.92f,
                DamageMultiplier = 1.05f
            });
            defs.Add(new FleetcrawlModuleUpgradeDefinition
            {
                ModuleType = FleetcrawlModuleType.Hangar,
                RequiredTags = FleetcrawlComboTag.Drone,
                TurnRateMultiplier = 1f,
                AccelerationMultiplier = 1.03f,
                DecelerationMultiplier = 1.03f,
                MaxSpeedMultiplier = 1.02f,
                CooldownMultiplier = 0.9f,
                DamageMultiplier = 1.06f
            });

            Debug.Log($"[FleetcrawlMeta] Module upgrade bootstrap definitions={defs.Length}.");
        }
    }

    public static class FleetcrawlModuleUpgradeResolver
    {
        public static FleetcrawlResolvedUpgradeStats ResolveForLimb(
            in FleetcrawlRolledLimb rolled,
            DynamicBuffer<FleetcrawlModuleLimbDefinition> limbDefinitions,
            DynamicBuffer<FleetcrawlLimbAffixDefinition> affixDefinitions,
            DynamicBuffer<FleetcrawlModuleUpgradeDefinition> upgradeDefinitions)
        {
            var stats = FleetcrawlResolvedUpgradeStats.Identity;

            for (var i = 0; i < limbDefinitions.Length; i++)
            {
                var limb = limbDefinitions[i];
                if (!limb.LimbId.Equals(rolled.LimbId))
                {
                    continue;
                }

                stats.ApplyMultipliers(
                    limb.TurnRateMultiplier,
                    limb.AccelerationMultiplier,
                    limb.DecelerationMultiplier,
                    limb.MaxSpeedMultiplier,
                    limb.CooldownMultiplier,
                    limb.DamageMultiplier);
                break;
            }

            for (var i = 0; i < affixDefinitions.Length; i++)
            {
                var affix = affixDefinitions[i];
                if (!affix.AffixId.Equals(rolled.AffixId))
                {
                    continue;
                }

                stats.ApplyMultipliers(
                    affix.TurnRateMultiplier,
                    affix.AccelerationMultiplier,
                    affix.DecelerationMultiplier,
                    affix.MaxSpeedMultiplier,
                    affix.CooldownMultiplier,
                    affix.DamageMultiplier);
                break;
            }

            for (var i = 0; i < upgradeDefinitions.Length; i++)
            {
                var upgrade = upgradeDefinitions[i];
                if (upgrade.ModuleType != rolled.ModuleType)
                {
                    continue;
                }

                if (upgrade.RequiredTags != FleetcrawlComboTag.None && (rolled.ComboTags & upgrade.RequiredTags) == 0)
                {
                    continue;
                }

                stats.ApplyMultipliers(
                    upgrade.TurnRateMultiplier,
                    upgrade.AccelerationMultiplier,
                    upgrade.DecelerationMultiplier,
                    upgrade.MaxSpeedMultiplier,
                    upgrade.CooldownMultiplier,
                    upgrade.DamageMultiplier);
            }

            var qualityScalar = 1f + ((int)rolled.Quality * 0.04f);
            stats.ApplyMultipliers(
                qualityScalar,
                qualityScalar,
                qualityScalar,
                qualityScalar,
                math.max(0.01f, 1f - ((int)rolled.Quality * 0.02f)),
                qualityScalar);

            return stats;
        }

        public static void ApplyToMovement(ref VesselMovement movement, in FleetcrawlResolvedUpgradeStats stats)
        {
            movement.TurnSpeed = math.max(0.01f, movement.TurnSpeed * stats.TurnRateMultiplier);
            movement.Acceleration = math.max(0.01f, movement.Acceleration * stats.AccelerationMultiplier);
            movement.Deceleration = math.max(0.01f, movement.Deceleration * stats.DecelerationMultiplier);
            movement.BaseSpeed = math.max(0.01f, movement.BaseSpeed * stats.MaxSpeedMultiplier);
        }

        public static void ApplyToWeapon(ref Space4XWeapon weapon, in FleetcrawlResolvedUpgradeStats stats)
        {
            weapon.BaseDamage = math.max(0.01f, weapon.BaseDamage * stats.DamageMultiplier);
            var cooldown = (int)math.round(weapon.CooldownTicks * stats.CooldownMultiplier);
            weapon.CooldownTicks = (ushort)math.clamp(cooldown, 1, ushort.MaxValue);
        }

        public static FleetcrawlResolvedUpgradeStats ResolveAggregate(
            DynamicBuffer<FleetcrawlRolledLimbBufferElement> rolledLimbs,
            DynamicBuffer<FleetcrawlModuleLimbDefinition> limbDefinitions,
            DynamicBuffer<FleetcrawlLimbAffixDefinition> affixDefinitions,
            DynamicBuffer<FleetcrawlModuleUpgradeDefinition> upgradeDefinitions)
        {
            var aggregate = FleetcrawlResolvedUpgradeStats.Identity;
            for (var i = 0; i < rolledLimbs.Length; i++)
            {
                var single = ResolveForLimb(rolledLimbs[i].Value, limbDefinitions, affixDefinitions, upgradeDefinitions);
                aggregate.ApplyMultipliers(
                    single.TurnRateMultiplier,
                    single.AccelerationMultiplier,
                    single.DecelerationMultiplier,
                    single.MaxSpeedMultiplier,
                    single.CooldownMultiplier,
                    single.DamageMultiplier);
            }

            return aggregate;
        }

        public static FleetcrawlResolvedUpgradeStats ResolveAggregateWithInventory(
            DynamicBuffer<FleetcrawlRolledLimbBufferElement> rolledLimbs,
            DynamicBuffer<FleetcrawlOwnedItem> ownedItems,
            DynamicBuffer<FleetcrawlModuleLimbDefinition> limbDefinitions,
            DynamicBuffer<FleetcrawlLimbAffixDefinition> affixDefinitions,
            DynamicBuffer<FleetcrawlModuleUpgradeDefinition> upgradeDefinitions,
            DynamicBuffer<FleetcrawlSetBonusDefinition> setBonusDefinitions)
        {
            var aggregate = ResolveAggregate(rolledLimbs, limbDefinitions, affixDefinitions, upgradeDefinitions);
            ApplyOwnedItemModifiers(ref aggregate, ownedItems);
            ApplySetBonuses(ref aggregate, ownedItems, setBonusDefinitions);
            return aggregate;
        }

        private static void ApplyOwnedItemModifiers(ref FleetcrawlResolvedUpgradeStats stats, DynamicBuffer<FleetcrawlOwnedItem> ownedItems)
        {
            for (var i = 0; i < ownedItems.Length; i++)
            {
                var item = ownedItems[i].Value;
                var qualityScalar = 1f + (int)item.Quality * 0.02f;
                switch (item.Archetype)
                {
                    case FleetcrawlLootArchetype.HullSegment:
                        stats.ApplyMultipliers(
                            1f * qualityScalar,
                            1.02f * qualityScalar,
                            1.03f * qualityScalar,
                            1.01f * qualityScalar,
                            1f,
                            1f);
                        break;
                    case FleetcrawlLootArchetype.Trinket:
                        stats.ApplyMultipliers(
                            1f,
                            1f,
                            1f,
                            1f,
                            ResolveBehaviorCooldownMultiplier(item.WeaponBehaviors),
                            ResolveBehaviorDamageMultiplier(item.WeaponBehaviors) * qualityScalar);
                        break;
                    case FleetcrawlLootArchetype.GeneralItem:
                        var stack = math.max(1, item.StackCount);
                        stats.ApplyMultipliers(
                            1f,
                            1f,
                            1f,
                            1f,
                            math.max(0.7f, 1f - (stack - 1) * 0.015f),
                            1f + (stack - 1) * 0.02f);
                        break;
                }
            }
        }

        private static void ApplySetBonuses(
            ref FleetcrawlResolvedUpgradeStats stats,
            DynamicBuffer<FleetcrawlOwnedItem> ownedItems,
            DynamicBuffer<FleetcrawlSetBonusDefinition> setBonusDefinitions)
        {
            for (var i = 0; i < setBonusDefinitions.Length; i++)
            {
                var set = setBonusDefinitions[i];
                var requiredCount = math.max(1, set.RequiredCount);
                var count = 0;

                for (var j = 0; j < ownedItems.Length; j++)
                {
                    var item = ownedItems[j].Value;
                    if (set.SetId.Length > 0 && !item.SetId.Equals(set.SetId))
                    {
                        continue;
                    }
                    if (set.ManufacturerId.Length > 0 && !item.ManufacturerId.Equals(set.ManufacturerId))
                    {
                        continue;
                    }
                    if (set.RequiredItemTags != FleetcrawlComboTag.None &&
                        (item.ComboTags & set.RequiredItemTags) != set.RequiredItemTags)
                    {
                        continue;
                    }
                    if (set.RequiredWeaponBehaviors != FleetcrawlWeaponBehaviorTag.None &&
                        (item.WeaponBehaviors & set.RequiredWeaponBehaviors) != set.RequiredWeaponBehaviors)
                    {
                        continue;
                    }
                    if (set.RequiredSkillFamily != FleetcrawlSkillFamily.None && item.SkillFamily != set.RequiredSkillFamily)
                    {
                        continue;
                    }

                    count++;
                }

                if (count < requiredCount)
                {
                    continue;
                }

                stats.ApplyMultipliers(
                    set.TurnRateMultiplier,
                    set.AccelerationMultiplier,
                    set.DecelerationMultiplier,
                    set.MaxSpeedMultiplier,
                    set.CooldownMultiplier,
                    set.DamageMultiplier);
            }
        }

        private static float ResolveBehaviorDamageMultiplier(FleetcrawlWeaponBehaviorTag behavior)
        {
            var multiplier = 1f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.BeamFork) != 0) multiplier *= 1.08f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.Pierce) != 0) multiplier *= 1.05f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.Ricochet) != 0) multiplier *= 1.04f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.Ionize) != 0) multiplier *= 1.03f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.BurnPayload) != 0) multiplier *= 1.05f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.DroneFocus) != 0) multiplier *= 1.02f;
            return multiplier;
        }

        private static float ResolveBehaviorCooldownMultiplier(FleetcrawlWeaponBehaviorTag behavior)
        {
            var multiplier = 1f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.BeamFork) != 0) multiplier *= 1.02f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.Pierce) != 0) multiplier *= 0.99f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.Ricochet) != 0) multiplier *= 1.01f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.Ionize) != 0) multiplier *= 0.97f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.BurnPayload) != 0) multiplier *= 1.03f;
            if ((behavior & FleetcrawlWeaponBehaviorTag.DroneFocus) != 0) multiplier *= 0.98f;
            return math.max(0.7f, multiplier);
        }
    }
}

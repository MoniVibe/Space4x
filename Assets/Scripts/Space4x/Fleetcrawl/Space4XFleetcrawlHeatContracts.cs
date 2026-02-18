using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4x.Fleetcrawl
{
    public enum FleetcrawlHeatModifierSourceKind : byte
    {
        LimbId = 0,
        AffixId = 1,
        ItemId = 2,
        SetId = 3,
        ModuleType = 4,
        LimbSlot = 5,
        ComboTag = 6,
        WeaponBehavior = 7
    }

    [InternalBufferCapacity(32)]
    public struct FleetcrawlHeatModifierDefinition : IBufferElementData
    {
        public FixedString64Bytes ModifierId;
        public FleetcrawlHeatModifierSourceKind SourceKind;
        public FixedString64Bytes SourceId;
        public FleetcrawlModuleType ModuleType;
        public FleetcrawlLimbSlot Slot;
        public FleetcrawlComboTag ComboTags;
        public FleetcrawlWeaponBehaviorTag WeaponBehaviors;
        public float HeatGenerationMultiplier;
        public float HeatDissipationMultiplier;
        public float HeatCapacityMultiplier;
        public float OverheatThresholdOffset01;
        public float HeatDamageBonusPerHeat01;
        public float HeatCooldownBonusPerHeat01;
        public float OverheatDamagePenaltyMultiplier;
        public float OverheatCooldownPenaltyMultiplier;
    }

    public struct FleetcrawlResolvedHeatStats
    {
        public float HeatGenerationMultiplier;
        public float HeatDissipationMultiplier;
        public float HeatCapacityMultiplier;
        public float OverheatThresholdOffset01;
        public float HeatDamageBonusPerHeat01;
        public float HeatCooldownBonusPerHeat01;
        public float OverheatDamagePenaltyMultiplier;
        public float OverheatCooldownPenaltyMultiplier;

        public static FleetcrawlResolvedHeatStats Identity => new FleetcrawlResolvedHeatStats
        {
            HeatGenerationMultiplier = 1f,
            HeatDissipationMultiplier = 1f,
            HeatCapacityMultiplier = 1f,
            OverheatThresholdOffset01 = 0f,
            HeatDamageBonusPerHeat01 = 0f,
            HeatCooldownBonusPerHeat01 = 0f,
            OverheatDamagePenaltyMultiplier = 0.75f,
            OverheatCooldownPenaltyMultiplier = 1.25f
        };

        public void Apply(in FleetcrawlHeatModifierDefinition modifier)
        {
            var generation = modifier.HeatGenerationMultiplier <= 0f ? 1f : modifier.HeatGenerationMultiplier;
            var dissipation = modifier.HeatDissipationMultiplier <= 0f ? 1f : modifier.HeatDissipationMultiplier;
            var capacity = modifier.HeatCapacityMultiplier <= 0f ? 1f : modifier.HeatCapacityMultiplier;
            var overheatDamagePenalty = modifier.OverheatDamagePenaltyMultiplier <= 0f ? 1f : modifier.OverheatDamagePenaltyMultiplier;
            var overheatCooldownPenalty = modifier.OverheatCooldownPenaltyMultiplier <= 0f ? 1f : modifier.OverheatCooldownPenaltyMultiplier;

            HeatGenerationMultiplier *= math.max(0.05f, generation);
            HeatDissipationMultiplier *= math.max(0.05f, dissipation);
            HeatCapacityMultiplier *= math.max(0.05f, capacity);
            OverheatThresholdOffset01 += modifier.OverheatThresholdOffset01;
            HeatDamageBonusPerHeat01 += math.max(0f, modifier.HeatDamageBonusPerHeat01);
            HeatCooldownBonusPerHeat01 += math.max(0f, modifier.HeatCooldownBonusPerHeat01);
            OverheatDamagePenaltyMultiplier *= math.max(0.05f, overheatDamagePenalty);
            OverheatCooldownPenaltyMultiplier *= math.max(0.05f, overheatCooldownPenalty);
        }
    }

    public struct FleetcrawlHeatRuntimeState : IComponentData
    {
        public float CurrentHeat;
        public float BaseHeatCapacity;
        public float BaseDissipationPerTick;
        public float BaseOverheatThreshold01;
        public float BaseRecoveryThreshold01;
        public byte IsOverheated;
        public uint LastTick;
    }

    public struct FleetcrawlHeatOutputState : IComponentData
    {
        public float Heat01;
        public float HeatCapacity;
        public float DissipationPerTick;
        public float OverheatThreshold01;
        public float RecoveryThreshold01;
        public float DamageMultiplier;
        public float CooldownMultiplier;
        public byte IsOverheated;
    }

    [InternalBufferCapacity(16)]
    public struct FleetcrawlHeatActionEvent : IBufferElementData
    {
        public FleetcrawlModuleType ModuleType;
        public FleetcrawlLimbSlot Slot;
        public FleetcrawlComboTag ComboTags;
        public FleetcrawlWeaponBehaviorTag WeaponBehaviors;
        public float BaseHeat;
        public float Scale;
    }

    public static class FleetcrawlHeatResolver
    {
        public static FleetcrawlResolvedHeatStats ResolveAggregate(
            DynamicBuffer<FleetcrawlRolledLimbBufferElement> rolledLimbs,
            DynamicBuffer<FleetcrawlOwnedItem> ownedItems,
            DynamicBuffer<FleetcrawlHeatModifierDefinition> heatDefinitions)
        {
            var stats = FleetcrawlResolvedHeatStats.Identity;
            for (var i = 0; i < heatDefinitions.Length; i++)
            {
                var definition = heatDefinitions[i];
                if (!MatchesAny(definition, rolledLimbs, ownedItems))
                {
                    continue;
                }

                stats.Apply(definition);
            }

            return stats;
        }

        public static void Tick(
            uint tick,
            DynamicBuffer<FleetcrawlHeatActionEvent> actions,
            in FleetcrawlResolvedHeatStats heatStats,
            ref FleetcrawlHeatRuntimeState runtime,
            out FleetcrawlHeatOutputState output)
        {
            var capacity = math.max(1f, runtime.BaseHeatCapacity * math.max(0.05f, heatStats.HeatCapacityMultiplier));
            var dissipation = math.max(0f, runtime.BaseDissipationPerTick * math.max(0.05f, heatStats.HeatDissipationMultiplier));
            var generationMultiplier = math.max(0.05f, heatStats.HeatGenerationMultiplier);

            var generated = 0f;
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                var scale = action.Scale <= 0f ? 1f : action.Scale;
                generated += math.max(0f, action.BaseHeat * scale * generationMultiplier);
            }

            runtime.CurrentHeat = math.max(0f, runtime.CurrentHeat + generated - dissipation);
            var heat01 = math.saturate(runtime.CurrentHeat / capacity);

            var overheatThreshold = math.clamp(runtime.BaseOverheatThreshold01 + heatStats.OverheatThresholdOffset01, 0.25f, 0.99f);
            var recoveryThreshold = math.min(runtime.BaseRecoveryThreshold01, overheatThreshold - 0.01f);
            recoveryThreshold = math.clamp(recoveryThreshold, 0.05f, 0.98f);

            if (runtime.IsOverheated == 0 && heat01 >= overheatThreshold)
            {
                runtime.IsOverheated = 1;
            }
            else if (runtime.IsOverheated != 0 && heat01 <= recoveryThreshold)
            {
                runtime.IsOverheated = 0;
            }

            var damageMultiplier = 1f;
            var cooldownMultiplier = 1f;
            if (runtime.IsOverheated != 0)
            {
                damageMultiplier *= math.max(0.05f, heatStats.OverheatDamagePenaltyMultiplier);
                cooldownMultiplier *= math.max(0.05f, heatStats.OverheatCooldownPenaltyMultiplier);
            }
            else
            {
                var normalizedHeat = math.saturate(heat01 / math.max(0.01f, overheatThreshold));
                damageMultiplier *= 1f + math.max(0f, heatStats.HeatDamageBonusPerHeat01) * normalizedHeat;
                cooldownMultiplier *= math.max(0.1f, 1f - math.max(0f, heatStats.HeatCooldownBonusPerHeat01) * normalizedHeat);
            }

            runtime.LastTick = tick;
            output = new FleetcrawlHeatOutputState
            {
                Heat01 = heat01,
                HeatCapacity = capacity,
                DissipationPerTick = dissipation,
                OverheatThreshold01 = overheatThreshold,
                RecoveryThreshold01 = recoveryThreshold,
                DamageMultiplier = damageMultiplier,
                CooldownMultiplier = cooldownMultiplier,
                IsOverheated = runtime.IsOverheated
            };

            actions.Clear();
        }

        public static FleetcrawlResolvedUpgradeStats ApplyHeatToUpgradeStats(
            in FleetcrawlResolvedUpgradeStats baseStats,
            in FleetcrawlHeatOutputState heatOutput)
        {
            var merged = baseStats;
            merged.DamageMultiplier *= math.max(0.05f, heatOutput.DamageMultiplier);
            merged.CooldownMultiplier *= math.max(0.05f, heatOutput.CooldownMultiplier);
            return merged;
        }

        private static bool MatchesAny(
            in FleetcrawlHeatModifierDefinition definition,
            DynamicBuffer<FleetcrawlRolledLimbBufferElement> rolledLimbs,
            DynamicBuffer<FleetcrawlOwnedItem> ownedItems)
        {
            for (var i = 0; i < rolledLimbs.Length; i++)
            {
                if (MatchesLimb(definition, rolledLimbs[i].Value))
                {
                    return true;
                }
            }

            for (var i = 0; i < ownedItems.Length; i++)
            {
                if (MatchesItem(definition, ownedItems[i].Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesLimb(in FleetcrawlHeatModifierDefinition definition, in FleetcrawlRolledLimb limb)
        {
            switch (definition.SourceKind)
            {
                case FleetcrawlHeatModifierSourceKind.LimbId:
                    return definition.SourceId.Length > 0 && limb.LimbId.Equals(definition.SourceId);
                case FleetcrawlHeatModifierSourceKind.AffixId:
                    return definition.SourceId.Length > 0 && limb.AffixId.Equals(definition.SourceId);
                case FleetcrawlHeatModifierSourceKind.ModuleType:
                    return limb.ModuleType == definition.ModuleType;
                case FleetcrawlHeatModifierSourceKind.LimbSlot:
                    return limb.Slot == definition.Slot;
                case FleetcrawlHeatModifierSourceKind.ComboTag:
                    return definition.ComboTags != FleetcrawlComboTag.None &&
                           (limb.ComboTags & definition.ComboTags) == definition.ComboTags;
                default:
                    return false;
            }
        }

        private static bool MatchesItem(in FleetcrawlHeatModifierDefinition definition, in FleetcrawlRolledItem item)
        {
            switch (definition.SourceKind)
            {
                case FleetcrawlHeatModifierSourceKind.ItemId:
                    return definition.SourceId.Length > 0 && item.ItemId.Equals(definition.SourceId);
                case FleetcrawlHeatModifierSourceKind.SetId:
                    return definition.SourceId.Length > 0 && item.SetId.Equals(definition.SourceId);
                case FleetcrawlHeatModifierSourceKind.ComboTag:
                    return definition.ComboTags != FleetcrawlComboTag.None &&
                           (item.ComboTags & definition.ComboTags) == definition.ComboTags;
                case FleetcrawlHeatModifierSourceKind.WeaponBehavior:
                    return definition.WeaponBehaviors != FleetcrawlWeaponBehaviorTag.None &&
                           (item.WeaponBehaviors & definition.WeaponBehaviors) == definition.WeaponBehaviors;
                default:
                    return false;
            }
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlLootShopBootstrapSystem))]
    public partial struct Space4XFleetcrawlHeatBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FleetcrawlOfferRuntimeTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var runtime = SystemAPI.GetSingletonEntity<FleetcrawlOfferRuntimeTag>();
            var em = state.EntityManager;
            if (em.HasBuffer<FleetcrawlHeatModifierDefinition>(runtime))
            {
                return;
            }

            var heatDefs = em.AddBuffer<FleetcrawlHeatModifierDefinition>(runtime);
            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("heat_limb_reactor_flux_core"),
                SourceKind = FleetcrawlHeatModifierSourceKind.LimbId,
                SourceId = new FixedString64Bytes("limb_reactor_flux_core"),
                HeatGenerationMultiplier = 1.18f,
                HeatDissipationMultiplier = 1f,
                HeatCapacityMultiplier = 1.12f,
                OverheatThresholdOffset01 = 0f,
                HeatDamageBonusPerHeat01 = 0.14f,
                HeatCooldownBonusPerHeat01 = 0.08f,
                OverheatDamagePenaltyMultiplier = 0.78f,
                OverheatCooldownPenaltyMultiplier = 1.22f
            });
            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("heat_affix_overclocked"),
                SourceKind = FleetcrawlHeatModifierSourceKind.AffixId,
                SourceId = new FixedString64Bytes("affix_overclocked"),
                HeatGenerationMultiplier = 1.16f,
                HeatDissipationMultiplier = 0.95f,
                HeatCapacityMultiplier = 1f,
                OverheatThresholdOffset01 = -0.02f,
                HeatDamageBonusPerHeat01 = 0.2f,
                HeatCooldownBonusPerHeat01 = 0.12f,
                OverheatDamagePenaltyMultiplier = 0.72f,
                OverheatCooldownPenaltyMultiplier = 1.28f
            });
            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("heat_slot_cooling"),
                SourceKind = FleetcrawlHeatModifierSourceKind.LimbSlot,
                Slot = FleetcrawlLimbSlot.Cooling,
                HeatGenerationMultiplier = 0.92f,
                HeatDissipationMultiplier = 1.45f,
                HeatCapacityMultiplier = 1.08f,
                OverheatThresholdOffset01 = 0.05f,
                HeatDamageBonusPerHeat01 = 0f,
                HeatCooldownBonusPerHeat01 = 0f,
                OverheatDamagePenaltyMultiplier = 1f,
                OverheatCooldownPenaltyMultiplier = 1f
            });
            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("heat_item_flux_capsule"),
                SourceKind = FleetcrawlHeatModifierSourceKind.ItemId,
                SourceId = new FixedString64Bytes("item_flux_capsule"),
                HeatGenerationMultiplier = 1f,
                HeatDissipationMultiplier = 1.05f,
                HeatCapacityMultiplier = 1.1f,
                OverheatThresholdOffset01 = 0.01f,
                HeatDamageBonusPerHeat01 = 0.1f,
                HeatCooldownBonusPerHeat01 = 0.06f,
                OverheatDamagePenaltyMultiplier = 0.88f,
                OverheatCooldownPenaltyMultiplier = 1.15f
            });

            Debug.Log($"[FleetcrawlHeat] Heat modifier bootstrap definitions={heatDefs.Length}.");
        }
    }
}

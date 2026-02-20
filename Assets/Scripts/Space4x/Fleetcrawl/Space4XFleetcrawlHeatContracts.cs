using Space4X.Registry;
using Space4x.Scenario;
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

    public enum FleetcrawlHeatSafetyMode : byte
    {
        ConservativeThrottle = 0,
        BalancedAutoVent = 1,
        UnsafeNoReduction = 2
    }

    [InternalBufferCapacity(1)]
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
        public float HeatEngineSpeedBonusPerHeat01;
        public float HeatShieldRechargeBonusPerHeat01;
        public float HeatShieldIntensityBonusPerHeat01;
        public float OverheatDamagePenaltyMultiplier;
        public float OverheatCooldownPenaltyMultiplier;
        public float OverheatJamChancePerTick;
        public float OverheatThermalSelfDamagePerTick;
        public float HeatsinkCapacityMultiplier;
        public float HeatsinkAbsorbMultiplier;
        public float HeatsinkVentMultiplier;
        public float UnsafeThermalLeakMultiplier;
        public float PreOverheatThrottleStart01;
        public float PreOverheatThrottleScale;
    }

    public struct FleetcrawlResolvedHeatStats
    {
        public float HeatGenerationMultiplier;
        public float HeatDissipationMultiplier;
        public float HeatCapacityMultiplier;
        public float OverheatThresholdOffset01;
        public float HeatDamageBonusPerHeat01;
        public float HeatCooldownBonusPerHeat01;
        public float HeatEngineSpeedBonusPerHeat01;
        public float HeatShieldRechargeBonusPerHeat01;
        public float HeatShieldIntensityBonusPerHeat01;
        public float OverheatDamagePenaltyMultiplier;
        public float OverheatCooldownPenaltyMultiplier;
        public float OverheatJamChancePerTick;
        public float OverheatThermalSelfDamagePerTick;
        public float HeatsinkCapacityMultiplier;
        public float HeatsinkAbsorbMultiplier;
        public float HeatsinkVentMultiplier;
        public float UnsafeThermalLeakMultiplier;
        public float PreOverheatThrottleStart01;
        public float PreOverheatThrottleScale;

        public static FleetcrawlResolvedHeatStats Identity => new FleetcrawlResolvedHeatStats
        {
            HeatGenerationMultiplier = 1f,
            HeatDissipationMultiplier = 1f,
            HeatCapacityMultiplier = 1f,
            OverheatThresholdOffset01 = 0f,
            HeatDamageBonusPerHeat01 = 0f,
            HeatCooldownBonusPerHeat01 = 0f,
            HeatEngineSpeedBonusPerHeat01 = 0f,
            HeatShieldRechargeBonusPerHeat01 = 0f,
            HeatShieldIntensityBonusPerHeat01 = 0f,
            OverheatDamagePenaltyMultiplier = 0.75f,
            OverheatCooldownPenaltyMultiplier = 1.25f,
            OverheatJamChancePerTick = 0.12f,
            OverheatThermalSelfDamagePerTick = 0.4f,
            HeatsinkCapacityMultiplier = 1f,
            HeatsinkAbsorbMultiplier = 1f,
            HeatsinkVentMultiplier = 1f,
            UnsafeThermalLeakMultiplier = 1.35f,
            PreOverheatThrottleStart01 = 0.72f,
            PreOverheatThrottleScale = 0.4f
        };

        public void Apply(in FleetcrawlHeatModifierDefinition modifier)
        {
            var generation = modifier.HeatGenerationMultiplier <= 0f ? 1f : modifier.HeatGenerationMultiplier;
            var dissipation = modifier.HeatDissipationMultiplier <= 0f ? 1f : modifier.HeatDissipationMultiplier;
            var capacity = modifier.HeatCapacityMultiplier <= 0f ? 1f : modifier.HeatCapacityMultiplier;
            var overheatDamagePenalty = modifier.OverheatDamagePenaltyMultiplier <= 0f ? 1f : modifier.OverheatDamagePenaltyMultiplier;
            var overheatCooldownPenalty = modifier.OverheatCooldownPenaltyMultiplier <= 0f ? 1f : modifier.OverheatCooldownPenaltyMultiplier;
            var heatsinkCapacity = modifier.HeatsinkCapacityMultiplier <= 0f ? 1f : modifier.HeatsinkCapacityMultiplier;
            var heatsinkAbsorb = modifier.HeatsinkAbsorbMultiplier <= 0f ? 1f : modifier.HeatsinkAbsorbMultiplier;
            var heatsinkVent = modifier.HeatsinkVentMultiplier <= 0f ? 1f : modifier.HeatsinkVentMultiplier;
            var unsafeLeak = modifier.UnsafeThermalLeakMultiplier <= 0f ? 1f : modifier.UnsafeThermalLeakMultiplier;

            HeatGenerationMultiplier *= math.max(0.05f, generation);
            HeatDissipationMultiplier *= math.max(0.05f, dissipation);
            HeatCapacityMultiplier *= math.max(0.05f, capacity);
            OverheatThresholdOffset01 += modifier.OverheatThresholdOffset01;
            HeatDamageBonusPerHeat01 += math.max(0f, modifier.HeatDamageBonusPerHeat01);
            HeatCooldownBonusPerHeat01 += math.max(0f, modifier.HeatCooldownBonusPerHeat01);
            HeatEngineSpeedBonusPerHeat01 += math.max(0f, modifier.HeatEngineSpeedBonusPerHeat01);
            HeatShieldRechargeBonusPerHeat01 += math.max(0f, modifier.HeatShieldRechargeBonusPerHeat01);
            HeatShieldIntensityBonusPerHeat01 += math.max(0f, modifier.HeatShieldIntensityBonusPerHeat01);
            OverheatDamagePenaltyMultiplier *= math.max(0.05f, overheatDamagePenalty);
            OverheatCooldownPenaltyMultiplier *= math.max(0.05f, overheatCooldownPenalty);
            OverheatJamChancePerTick = math.clamp(OverheatJamChancePerTick + modifier.OverheatJamChancePerTick, 0f, 1f);
            OverheatThermalSelfDamagePerTick = math.max(0f, OverheatThermalSelfDamagePerTick + modifier.OverheatThermalSelfDamagePerTick);
            HeatsinkCapacityMultiplier *= math.max(0.05f, heatsinkCapacity);
            HeatsinkAbsorbMultiplier *= math.max(0.05f, heatsinkAbsorb);
            HeatsinkVentMultiplier *= math.max(0.05f, heatsinkVent);
            UnsafeThermalLeakMultiplier *= math.max(0.05f, unsafeLeak);
            if (modifier.PreOverheatThrottleStart01 > 0f)
            {
                PreOverheatThrottleStart01 = math.clamp(math.min(PreOverheatThrottleStart01, modifier.PreOverheatThrottleStart01), 0.05f, 0.99f);
            }
            PreOverheatThrottleScale += math.max(0f, modifier.PreOverheatThrottleScale);
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

    public struct FleetcrawlHeatsinkState : IComponentData
    {
        public float StoredHeat;
        public float BaseCapacity;
        public float BaseAbsorbPerTick;
        public float BaseVentPerTick;
    }

    public struct FleetcrawlHeatControlState : IComponentData
    {
        public FleetcrawlHeatSafetyMode SafetyMode;
        public byte HeatsinkEnabled;
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
        public float FireRateThrottleMultiplier;
        public float EngineSpeedMultiplier;
        public float ShieldRechargeMultiplier;
        public float ShieldIntensityMultiplier;
        public float JamChance;
        public float ThermalSelfDamagePerTick;
        public float HeatsinkStoredHeat;
        public float HeatsinkCapacity;
        public byte SuppressFire;
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
            var heatsink = default(FleetcrawlHeatsinkState);
            TickAdvanced(
                tick,
                actions,
                heatStats,
                ref runtime,
                ref heatsink,
                FleetcrawlHeatSafetyMode.BalancedAutoVent,
                out output);
        }

        public static void TickAdvanced(
            uint tick,
            DynamicBuffer<FleetcrawlHeatActionEvent> actions,
            in FleetcrawlResolvedHeatStats heatStats,
            ref FleetcrawlHeatRuntimeState runtime,
            ref FleetcrawlHeatsinkState heatsink,
            FleetcrawlHeatSafetyMode safetyMode,
            out FleetcrawlHeatOutputState output)
        {
            var capacity = math.max(1f, runtime.BaseHeatCapacity * math.max(0.05f, heatStats.HeatCapacityMultiplier));
            var dissipation = math.max(0f, runtime.BaseDissipationPerTick * math.max(0.05f, heatStats.HeatDissipationMultiplier));
            var generationMultiplier = math.max(0.05f, heatStats.HeatGenerationMultiplier);
            var heatsinkCapacity = math.max(0f, heatsink.BaseCapacity * math.max(0.05f, heatStats.HeatsinkCapacityMultiplier));
            var heatsinkAbsorbPerTick = math.max(0f, heatsink.BaseAbsorbPerTick * math.max(0.05f, heatStats.HeatsinkAbsorbMultiplier));
            var heatsinkVentPerTick = math.max(0f, heatsink.BaseVentPerTick * math.max(0.05f, heatStats.HeatsinkVentMultiplier));

            var generated = 0f;
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                var scale = action.Scale <= 0f ? 1f : action.Scale;
                generated += math.max(0f, action.BaseHeat * scale * generationMultiplier);
            }

            if (generated > 0f && heatsinkCapacity > 0f && heatsinkAbsorbPerTick > 0f)
            {
                heatsink.StoredHeat = math.clamp(heatsink.StoredHeat, 0f, heatsinkCapacity);
                var absorb = math.min(generated, math.min(heatsinkAbsorbPerTick, heatsinkCapacity - heatsink.StoredHeat));
                if (absorb > 0f)
                {
                    heatsink.StoredHeat += absorb;
                    generated -= absorb;
                }
            }

            runtime.CurrentHeat = math.max(0f, runtime.CurrentHeat + generated - dissipation);

            if (heatsink.StoredHeat > 0f && heatsinkVentPerTick > 0f)
            {
                var vent = math.min(heatsink.StoredHeat, heatsinkVentPerTick);
                heatsink.StoredHeat -= vent;
            }

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
            var fireRateThrottle = 1f;
            var engineSpeedMultiplier = 1f;
            var shieldRechargeMultiplier = 1f;
            var shieldIntensityMultiplier = 1f;
            var jamChance = 0f;
            var thermalSelfDamage = 0f;
            var suppressFire = (byte)0;
            var normalizedHeat = math.saturate(heat01 / math.max(0.01f, overheatThreshold));

            if (runtime.IsOverheated != 0)
            {
                if (safetyMode == FleetcrawlHeatSafetyMode.UnsafeNoReduction)
                {
                    var stress = math.saturate((heat01 - overheatThreshold) / math.max(0.01f, 1f - overheatThreshold));
                    jamChance = math.clamp(heatStats.OverheatJamChancePerTick * (1f + stress), 0f, 1f);
                    thermalSelfDamage = math.max(0f, heatStats.OverheatThermalSelfDamagePerTick * heatStats.UnsafeThermalLeakMultiplier * (1f + stress));
                }
                else
                {
                    damageMultiplier *= math.max(0.05f, heatStats.OverheatDamagePenaltyMultiplier);
                    cooldownMultiplier *= math.max(0.05f, heatStats.OverheatCooldownPenaltyMultiplier);
                    fireRateThrottle *= safetyMode == FleetcrawlHeatSafetyMode.ConservativeThrottle ? 1.25f : 1.15f;
                    suppressFire = 1;
                }

                engineSpeedMultiplier *= safetyMode == FleetcrawlHeatSafetyMode.UnsafeNoReduction ? 0.95f : 0.88f;
                shieldRechargeMultiplier *= safetyMode == FleetcrawlHeatSafetyMode.UnsafeNoReduction ? 0.92f : 0.82f;
                shieldIntensityMultiplier *= safetyMode == FleetcrawlHeatSafetyMode.UnsafeNoReduction ? 0.9f : 0.78f;
            }
            else
            {
                damageMultiplier *= 1f + math.max(0f, heatStats.HeatDamageBonusPerHeat01) * normalizedHeat;
                cooldownMultiplier *= math.max(0.1f, 1f - math.max(0f, heatStats.HeatCooldownBonusPerHeat01) * normalizedHeat);
                engineSpeedMultiplier *= 1f + math.max(0f, heatStats.HeatEngineSpeedBonusPerHeat01) * normalizedHeat;
                shieldRechargeMultiplier *= 1f + math.max(0f, heatStats.HeatShieldRechargeBonusPerHeat01) * normalizedHeat;
                shieldIntensityMultiplier *= 1f + math.max(0f, heatStats.HeatShieldIntensityBonusPerHeat01) * normalizedHeat;

                if (safetyMode != FleetcrawlHeatSafetyMode.UnsafeNoReduction)
                {
                    var throttleStart = math.clamp(heatStats.PreOverheatThrottleStart01, 0.05f, 0.99f);
                    if (heat01 > throttleStart)
                    {
                        var throttleHeat = math.saturate((heat01 - throttleStart) / math.max(0.01f, 1f - throttleStart));
                        fireRateThrottle *= 1f + throttleHeat * math.max(0f, heatStats.PreOverheatThrottleScale);
                    }
                }
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
                FireRateThrottleMultiplier = fireRateThrottle,
                EngineSpeedMultiplier = engineSpeedMultiplier,
                ShieldRechargeMultiplier = shieldRechargeMultiplier,
                ShieldIntensityMultiplier = shieldIntensityMultiplier,
                JamChance = jamChance,
                ThermalSelfDamagePerTick = thermalSelfDamage,
                HeatsinkStoredHeat = heatsink.StoredHeat,
                HeatsinkCapacity = heatsinkCapacity,
                SuppressFire = suppressFire,
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
            var fireThrottle = heatOutput.FireRateThrottleMultiplier <= 0f ? 1f : heatOutput.FireRateThrottleMultiplier;
            merged.CooldownMultiplier *= math.max(0.05f, heatOutput.CooldownMultiplier * math.max(0.1f, fireThrottle));
            var engineScale = heatOutput.EngineSpeedMultiplier <= 0f ? 1f : math.max(0.05f, heatOutput.EngineSpeedMultiplier);
            merged.TurnRateMultiplier *= engineScale;
            merged.AccelerationMultiplier *= engineScale;
            merged.DecelerationMultiplier *= engineScale;
            merged.MaxSpeedMultiplier *= engineScale;
            return merged;
        }

        public static float ResolveHeatSignature01(in FleetcrawlHeatOutputState heatOutput)
        {
            var heat01 = math.saturate(heatOutput.Heat01);
            var heatsinkFill01 = heatOutput.HeatsinkCapacity > 1e-5f
                ? math.saturate(heatOutput.HeatsinkStoredHeat / heatOutput.HeatsinkCapacity)
                : 0f;
            return math.saturate(heat01 + heatsinkFill01 * 0.35f + (heatOutput.IsOverheated != 0 ? 0.2f : 0f));
        }

        public static bool ShouldSuppressFire(in FleetcrawlHeatOutputState output)
        {
            return output.SuppressFire != 0;
        }

        public static bool ResolveJam(in FleetcrawlHeatOutputState output, Entity source, int mountIndex, uint tick)
        {
            if (output.SuppressFire != 0 || output.JamChance <= 1e-5f)
            {
                return false;
            }

            var hash = math.hash(new uint4(
                (uint)source.Index,
                (uint)math.max(0, source.Version),
                (uint)math.max(0, mountIndex + 1),
                tick ^ 0x9E3779B9u));
            var roll = (hash & 0xFFFFu) / 65535f;
            return roll < math.saturate(output.JamChance);
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
                HeatEngineSpeedBonusPerHeat01 = 0.08f,
                HeatShieldRechargeBonusPerHeat01 = 0.03f,
                HeatShieldIntensityBonusPerHeat01 = 0.02f,
                OverheatDamagePenaltyMultiplier = 0.78f,
                OverheatCooldownPenaltyMultiplier = 1.22f,
                OverheatJamChancePerTick = 0.03f,
                OverheatThermalSelfDamagePerTick = 0.1f,
                HeatsinkCapacityMultiplier = 1.1f,
                HeatsinkAbsorbMultiplier = 1.05f,
                HeatsinkVentMultiplier = 1f,
                UnsafeThermalLeakMultiplier = 1.15f,
                PreOverheatThrottleStart01 = 0.72f,
                PreOverheatThrottleScale = 0.35f
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
                HeatEngineSpeedBonusPerHeat01 = 0.06f,
                OverheatDamagePenaltyMultiplier = 0.72f,
                OverheatCooldownPenaltyMultiplier = 1.28f,
                OverheatJamChancePerTick = 0.08f,
                OverheatThermalSelfDamagePerTick = 0.28f,
                HeatsinkCapacityMultiplier = 0.95f,
                HeatsinkAbsorbMultiplier = 0.92f,
                HeatsinkVentMultiplier = 0.88f,
                UnsafeThermalLeakMultiplier = 1.35f,
                PreOverheatThrottleStart01 = 0.68f,
                PreOverheatThrottleScale = 0.46f
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
                HeatShieldRechargeBonusPerHeat01 = 0.05f,
                HeatShieldIntensityBonusPerHeat01 = 0.03f,
                OverheatDamagePenaltyMultiplier = 1f,
                OverheatCooldownPenaltyMultiplier = 1f,
                OverheatJamChancePerTick = -0.04f,
                OverheatThermalSelfDamagePerTick = -0.1f,
                HeatsinkCapacityMultiplier = 1.5f,
                HeatsinkAbsorbMultiplier = 1.65f,
                HeatsinkVentMultiplier = 1.4f,
                UnsafeThermalLeakMultiplier = 0.85f,
                PreOverheatThrottleStart01 = 0.8f,
                PreOverheatThrottleScale = 0.18f
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
                HeatShieldIntensityBonusPerHeat01 = 0.08f,
                OverheatDamagePenaltyMultiplier = 0.88f,
                OverheatCooldownPenaltyMultiplier = 1.15f,
                OverheatJamChancePerTick = 0.02f,
                OverheatThermalSelfDamagePerTick = 0.05f,
                HeatsinkCapacityMultiplier = 1.2f,
                HeatsinkAbsorbMultiplier = 1.1f,
                HeatsinkVentMultiplier = 1.12f,
                UnsafeThermalLeakMultiplier = 1.08f,
                PreOverheatThrottleStart01 = 0.74f,
                PreOverheatThrottleScale = 0.3f
            });
            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("heat_behavior_ionize"),
                SourceKind = FleetcrawlHeatModifierSourceKind.WeaponBehavior,
                WeaponBehaviors = FleetcrawlWeaponBehaviorTag.Ionize,
                HeatGenerationMultiplier = 1.02f,
                HeatDissipationMultiplier = 1f,
                HeatCapacityMultiplier = 1f,
                OverheatThresholdOffset01 = 0f,
                HeatDamageBonusPerHeat01 = 0.06f,
                HeatCooldownBonusPerHeat01 = 0.03f,
                HeatShieldRechargeBonusPerHeat01 = 0.12f,
                HeatShieldIntensityBonusPerHeat01 = 0.1f,
                OverheatDamagePenaltyMultiplier = 0.92f,
                OverheatCooldownPenaltyMultiplier = 1.08f,
                HeatsinkCapacityMultiplier = 1.06f,
                HeatsinkAbsorbMultiplier = 1.02f,
                HeatsinkVentMultiplier = 1.1f,
                PreOverheatThrottleStart01 = 0.76f,
                PreOverheatThrottleScale = 0.24f
            });
            heatDefs.Add(new FleetcrawlHeatModifierDefinition
            {
                ModifierId = new FixedString64Bytes("heat_set_prism"),
                SourceKind = FleetcrawlHeatModifierSourceKind.SetId,
                SourceId = new FixedString64Bytes("set_prism"),
                HeatGenerationMultiplier = 1f,
                HeatDissipationMultiplier = 1.08f,
                HeatCapacityMultiplier = 1.12f,
                OverheatThresholdOffset01 = 0.03f,
                HeatDamageBonusPerHeat01 = 0.08f,
                HeatCooldownBonusPerHeat01 = 0.05f,
                HeatEngineSpeedBonusPerHeat01 = 0.04f,
                HeatShieldRechargeBonusPerHeat01 = 0.08f,
                OverheatDamagePenaltyMultiplier = 0.9f,
                OverheatCooldownPenaltyMultiplier = 1.12f,
                OverheatJamChancePerTick = -0.03f,
                HeatsinkCapacityMultiplier = 1.2f,
                HeatsinkAbsorbMultiplier = 1.16f,
                HeatsinkVentMultiplier = 1.2f,
                UnsafeThermalLeakMultiplier = 0.92f,
                PreOverheatThrottleStart01 = 0.78f,
                PreOverheatThrottleScale = 0.22f
            });

            Debug.Log($"[FleetcrawlHeat] Heat modifier bootstrap definitions={heatDefs.Length}.");
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XFleetcrawlHeatRuntimeBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponMount>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (weapons, entity) in SystemAPI.Query<DynamicBuffer<WeaponMount>>()
                         .WithAny<Space4XRunPlayerTag, Space4XRunEnemyTag>()
                         .WithEntityAccess())
            {
                if (!em.HasComponent<FleetcrawlHeatRuntimeState>(entity))
                {
                    var capSum = 0f;
                    var dissSum = 0f;
                    for (var i = 0; i < weapons.Length; i++)
                    {
                        var mount = weapons[i];
                        capSum += mount.HeatCapacity > 0f ? mount.HeatCapacity : 100f;
                        dissSum += mount.HeatDissipation > 0f ? mount.HeatDissipation : 4f;
                    }

                    var baseCapacity = math.max(40f, capSum * 0.5f);
                    var baseDissipation = math.max(0.5f, dissSum * 0.25f);
                    ecb.AddComponent(entity, new FleetcrawlHeatRuntimeState
                    {
                        CurrentHeat = 0f,
                        BaseHeatCapacity = baseCapacity,
                        BaseDissipationPerTick = baseDissipation,
                        BaseOverheatThreshold01 = 0.85f,
                        BaseRecoveryThreshold01 = 0.45f,
                        IsOverheated = 0,
                        LastTick = 0u
                    });
                }

                if (!em.HasComponent<FleetcrawlHeatsinkState>(entity))
                {
                    ecb.AddComponent(entity, new FleetcrawlHeatsinkState
                    {
                        StoredHeat = 0f,
                        BaseCapacity = 60f,
                        BaseAbsorbPerTick = 10f,
                        BaseVentPerTick = 3f
                    });
                }

                if (!em.HasComponent<FleetcrawlHeatControlState>(entity))
                {
                    ecb.AddComponent(entity, new FleetcrawlHeatControlState
                    {
                        SafetyMode = FleetcrawlHeatSafetyMode.BalancedAutoVent,
                        HeatsinkEnabled = 1
                    });
                }

                if (!em.HasComponent<FleetcrawlHeatOutputState>(entity))
                {
                    ecb.AddComponent(entity, new FleetcrawlHeatOutputState
                    {
                        Heat01 = 0f,
                        HeatCapacity = 100f,
                        DissipationPerTick = 0f,
                        OverheatThreshold01 = 0.85f,
                        RecoveryThreshold01 = 0.45f,
                        DamageMultiplier = 1f,
                        CooldownMultiplier = 1f,
                        FireRateThrottleMultiplier = 1f,
                        EngineSpeedMultiplier = 1f,
                        ShieldRechargeMultiplier = 1f,
                        ShieldIntensityMultiplier = 1f,
                        JamChance = 0f,
                        ThermalSelfDamagePerTick = 0f,
                        HeatsinkStoredHeat = 0f,
                        HeatsinkCapacity = 0f,
                        SuppressFire = 0,
                        IsOverheated = 0
                    });
                }

                if (!em.HasBuffer<FleetcrawlHeatActionEvent>(entity))
                {
                    ecb.AddBuffer<FleetcrawlHeatActionEvent>(entity);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}

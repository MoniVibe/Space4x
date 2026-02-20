using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4x.Fleetcrawl
{
    public struct FleetcrawlOfferRuntimeTag : IComponentData
    {
    }

    public struct FleetcrawlRunLevelState : IComponentData
    {
        public int Level;
    }

    public struct FleetcrawlRunExperience : IComponentData
    {
        public int Value;
    }

    public struct FleetcrawlRunShardWallet : IComponentData
    {
        public int Shards;
    }

    public struct FleetcrawlRunChallengeState : IComponentData
    {
        public int Challenge;
    }

    public struct FleetcrawlOfferGenerationConfig : IComponentData
    {
        public int CurrencySlotCount;
        public int LootSlotCount;
    }

    public struct FleetcrawlOfferGenerationCache : IComponentData
    {
        public uint LastSignature;
        public int RefreshCount;
        public int LastRoomIndex;
        public int LastLevel;
        public int LastXp;
        public int LastShards;
        public int LastChallenge;
    }

    public struct FleetcrawlOfferRefreshRequest : IComponentData
    {
        public uint Nonce;
        public FixedString32Bytes SourceTag;
    }

    public enum FleetcrawlOfferChannel : byte
    {
        Loot = 0,
        CurrencyShop = 1
    }

    public struct FleetcrawlPurchaseRequest : IComponentData
    {
        public FleetcrawlOfferChannel Channel;
        public int SlotIndex;
        public uint Nonce;
        public FixedString32Bytes SourceTag;
    }

    public struct FleetcrawlPurchaseRuntimeState : IComponentData
    {
        public uint LastProcessedSignature;
        public uint LastProcessedRequestNonce;
        public int LastAutoPurchaseRoomIndex;
        public int PurchasesResolved;
    }

    // Keep singleton archetype lean; catalogs can spill to heap-backed buffer storage.
    [InternalBufferCapacity(1)]
    public struct FleetcrawlCurrencyShopCatalogEntry : IBufferElementData
    {
        public FixedString64Bytes OfferId;
        public FixedString64Bytes SkuId;
        public int Weight;
        public int MinLevel;
        public int BasePriceShards;
        public FleetcrawlComboTag ComboTags;
    }

    [InternalBufferCapacity(1)]
    public struct FleetcrawlLootOfferCatalogEntry : IBufferElementData
    {
        public FixedString64Bytes OfferId;
        public FleetcrawlLootArchetype Archetype;
        public FleetcrawlModuleType ModuleType;
        public FleetcrawlLimbSlot Slot;
        public FixedString64Bytes ItemId;
        public FixedString64Bytes ManufacturerId;
        public FixedString64Bytes SetId;
        public FleetcrawlWeaponBehaviorTag WeaponBehaviors;
        public FleetcrawlSkillFamily SkillFamily;
        public int Weight;
        public int MinLevel;
        public int BaseShardCost;
        public FleetcrawlComboTag ComboTags;
    }

    [InternalBufferCapacity(1)]
    public struct FleetcrawlCurrencyShopOfferEntry : IBufferElementData
    {
        public int SlotIndex;
        public FixedString64Bytes OfferId;
        public FixedString64Bytes SkuId;
        public int PriceShards;
        public FleetcrawlComboTag ComboTags;
        public uint RollHash;
    }

    [InternalBufferCapacity(1)]
    public struct FleetcrawlLootOfferEntry : IBufferElementData
    {
        public int SlotIndex;
        public FixedString64Bytes OfferId;
        public FleetcrawlLootArchetype Archetype;
        public FleetcrawlModuleType ModuleType;
        public FleetcrawlLimbSlot Slot;
        public FixedString64Bytes ItemId;
        public FixedString64Bytes ManufacturerId;
        public FixedString64Bytes SetId;
        public int PriceShards;
        public FleetcrawlLimbQualityTier Quality;
        public int ModuleSocketCount;
        public int StackCount;
        public FleetcrawlWeaponBehaviorTag WeaponBehaviors;
        public FleetcrawlSkillFamily SkillFamily;
        public FleetcrawlRolledLimb RolledLimb;
        public FleetcrawlRolledItem RolledItem;
        public FleetcrawlComboTag ComboTags;
        public uint RollHash;
    }

    public static class FleetcrawlDeterministicOfferGeneration
    {
        public static uint ComputeSignature(uint seed, int roomIndex, int level, int xp, int shards, int challenge, uint nonce)
        {
            var input = new uint4(
                seed ^ 0xB5297A4Du,
                (uint)(roomIndex + 1) * 2246822519u,
                ((uint)math.max(1, level) * 3266489917u) ^ ((uint)math.max(0, challenge) * 374761393u),
                ((uint)math.max(0, xp) * 668265263u) ^ ((uint)math.max(0, shards) * 362437u) ^ nonce);
            return math.hash(input);
        }

        public static uint ComputeOfferHash(
            uint seed,
            int roomIndex,
            int level,
            int xp,
            int shards,
            int challenge,
            uint nonce,
            int channel,
            int slotIndex,
            int refreshCount)
        {
            var baseHash = ComputeSignature(seed, roomIndex, level, xp, shards, challenge, nonce);
            return math.hash(new uint4(
                baseHash,
                (uint)(channel + 1) * 2246822519u,
                (uint)(slotIndex + 1) * 3266489917u,
                (uint)(refreshCount + 1) * 668265263u));
        }

    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlBootstrapSystem))]
    public partial struct Space4XFleetcrawlLootShopBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleetcrawlSeeded>();
            state.RequireForUpdate<Space4XFleetcrawlDirectorState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<FleetcrawlOfferRuntimeTag>(out _))
            {
                return;
            }

            var em = state.EntityManager;
            var runtime = em.CreateEntity(
                typeof(FleetcrawlOfferRuntimeTag),
                typeof(FleetcrawlOfferGenerationConfig),
                typeof(FleetcrawlOfferGenerationCache),
                typeof(FleetcrawlPurchaseRuntimeState));

            em.SetComponentData(runtime, new FleetcrawlOfferGenerationConfig
            {
                CurrencySlotCount = 4,
                LootSlotCount = 3
            });
            em.SetComponentData(runtime, new FleetcrawlOfferGenerationCache
            {
                LastSignature = 0u,
                RefreshCount = 0,
                LastRoomIndex = -1,
                LastLevel = 1,
                LastXp = 0,
                LastShards = 0,
                LastChallenge = 0
            });
            em.SetComponentData(runtime, new FleetcrawlPurchaseRuntimeState
            {
                LastProcessedSignature = 0u,
                LastProcessedRequestNonce = 0u,
                LastAutoPurchaseRoomIndex = -1,
                PurchasesResolved = 0
            });

            var shopCatalog = em.AddBuffer<FleetcrawlCurrencyShopCatalogEntry>(runtime);
            shopCatalog.Add(new FleetcrawlCurrencyShopCatalogEntry
            {
                OfferId = new FixedString64Bytes("shop_soft_cache_s"),
                SkuId = new FixedString64Bytes("soft_pack_small"),
                Weight = 10,
                MinLevel = 1,
                BasePriceShards = 40,
                ComboTags = FleetcrawlComboTag.Support
            });
            shopCatalog.Add(new FleetcrawlCurrencyShopCatalogEntry
            {
                OfferId = new FixedString64Bytes("shop_soft_cache_m"),
                SkuId = new FixedString64Bytes("soft_pack_medium"),
                Weight = 8,
                MinLevel = 2,
                BasePriceShards = 80,
                ComboTags = FleetcrawlComboTag.Flux
            });
            shopCatalog.Add(new FleetcrawlCurrencyShopCatalogEntry
            {
                OfferId = new FixedString64Bytes("shop_reroll_token"),
                SkuId = new FixedString64Bytes("ent_radar_uplink"),
                Weight = 6,
                MinLevel = 3,
                BasePriceShards = 120,
                ComboTags = FleetcrawlComboTag.Agile
            });

            var lootCatalog = em.AddBuffer<FleetcrawlLootOfferCatalogEntry>(runtime);
            lootCatalog.Add(new FleetcrawlLootOfferCatalogEntry
            {
                OfferId = new FixedString64Bytes("loot_weapon_barrel"),
                Archetype = FleetcrawlLootArchetype.ModuleLimb,
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Barrel,
                ItemId = new FixedString64Bytes("limb_barrel_serrated"),
                ManufacturerId = new FixedString64Bytes("baseline"),
                SetId = new FixedString64Bytes("set_raider"),
                WeaponBehaviors = FleetcrawlWeaponBehaviorTag.Pierce,
                SkillFamily = FleetcrawlSkillFamily.Ordnance,
                Weight = 12,
                MinLevel = 1,
                BaseShardCost = 25,
                ComboTags = FleetcrawlComboTag.Siege
            });
            lootCatalog.Add(new FleetcrawlLootOfferCatalogEntry
            {
                OfferId = new FixedString64Bytes("loot_weapon_stabilizer"),
                Archetype = FleetcrawlLootArchetype.ModuleLimb,
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Stabilizer,
                ItemId = new FixedString64Bytes("limb_stabilizer_vectored"),
                ManufacturerId = new FixedString64Bytes("baseline"),
                SetId = new FixedString64Bytes("set_raider"),
                WeaponBehaviors = FleetcrawlWeaponBehaviorTag.Ricochet,
                SkillFamily = FleetcrawlSkillFamily.Mobility,
                Weight = 10,
                MinLevel = 2,
                BaseShardCost = 30,
                ComboTags = FleetcrawlComboTag.Agile
            });
            lootCatalog.Add(new FleetcrawlLootOfferCatalogEntry
            {
                OfferId = new FixedString64Bytes("loot_reactor_core"),
                Archetype = FleetcrawlLootArchetype.ModuleLimb,
                ModuleType = FleetcrawlModuleType.Reactor,
                Slot = FleetcrawlLimbSlot.Core,
                ItemId = new FixedString64Bytes("limb_reactor_flux_core"),
                ManufacturerId = new FixedString64Bytes("prismworks"),
                SetId = new FixedString64Bytes("set_prism"),
                WeaponBehaviors = FleetcrawlWeaponBehaviorTag.Ionize,
                SkillFamily = FleetcrawlSkillFamily.Support,
                Weight = 8,
                MinLevel = 2,
                BaseShardCost = 35,
                ComboTags = FleetcrawlComboTag.Flux
            });
            lootCatalog.Add(new FleetcrawlLootOfferCatalogEntry
            {
                OfferId = new FixedString64Bytes("loot_hangar_utility"),
                Archetype = FleetcrawlLootArchetype.ModuleLimb,
                ModuleType = FleetcrawlModuleType.Hangar,
                Slot = FleetcrawlLimbSlot.Utility,
                ItemId = new FixedString64Bytes("limb_hangar_relay"),
                ManufacturerId = new FixedString64Bytes("prismworks"),
                SetId = new FixedString64Bytes("set_prism"),
                WeaponBehaviors = FleetcrawlWeaponBehaviorTag.DroneFocus,
                SkillFamily = FleetcrawlSkillFamily.Support,
                Weight = 7,
                MinLevel = 3,
                BaseShardCost = 40,
                ComboTags = FleetcrawlComboTag.Drone
            });
            lootCatalog.Add(new FleetcrawlLootOfferCatalogEntry
            {
                OfferId = new FixedString64Bytes("loot_hull_segment_bastion"),
                Archetype = FleetcrawlLootArchetype.HullSegment,
                ModuleType = FleetcrawlModuleType.Utility,
                Slot = FleetcrawlLimbSlot.Core,
                ItemId = new FixedString64Bytes("hull_bastion_ring"),
                ManufacturerId = new FixedString64Bytes("bastion"),
                SetId = new FixedString64Bytes("set_bastion"),
                WeaponBehaviors = FleetcrawlWeaponBehaviorTag.None,
                SkillFamily = FleetcrawlSkillFamily.Defense,
                Weight = 6,
                MinLevel = 3,
                BaseShardCost = 52,
                ComboTags = FleetcrawlComboTag.Vanguard | FleetcrawlComboTag.Support
            });
            lootCatalog.Add(new FleetcrawlLootOfferCatalogEntry
            {
                OfferId = new FixedString64Bytes("loot_trinket_prism_refractor"),
                Archetype = FleetcrawlLootArchetype.Trinket,
                ModuleType = FleetcrawlModuleType.Utility,
                Slot = FleetcrawlLimbSlot.Utility,
                ItemId = new FixedString64Bytes("trinket_prism_refractor"),
                ManufacturerId = new FixedString64Bytes("prismworks"),
                SetId = new FixedString64Bytes("set_prism"),
                WeaponBehaviors = FleetcrawlWeaponBehaviorTag.BeamFork | FleetcrawlWeaponBehaviorTag.Ionize,
                SkillFamily = FleetcrawlSkillFamily.Support,
                Weight = 5,
                MinLevel = 4,
                BaseShardCost = 64,
                ComboTags = FleetcrawlComboTag.Arc | FleetcrawlComboTag.Flux
            });
            lootCatalog.Add(new FleetcrawlLootOfferCatalogEntry
            {
                OfferId = new FixedString64Bytes("loot_item_nanite_cache"),
                Archetype = FleetcrawlLootArchetype.GeneralItem,
                ModuleType = FleetcrawlModuleType.Utility,
                Slot = FleetcrawlLimbSlot.Utility,
                ItemId = new FixedString64Bytes("item_nanite_cache"),
                ManufacturerId = new FixedString64Bytes("civfoundry"),
                SetId = new FixedString64Bytes("set_support"),
                WeaponBehaviors = FleetcrawlWeaponBehaviorTag.None,
                SkillFamily = FleetcrawlSkillFamily.Defense,
                Weight = 9,
                MinLevel = 2,
                BaseShardCost = 28,
                ComboTags = FleetcrawlComboTag.Support
            });

            var limbDefs = em.AddBuffer<FleetcrawlModuleLimbDefinition>(runtime);
            limbDefs.Add(new FleetcrawlModuleLimbDefinition
            {
                LimbId = new FixedString64Bytes("limb_barrel_serrated"),
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Barrel,
                SharingMode = FleetcrawlLimbSharingMode.Unique,
                ComboTags = FleetcrawlComboTag.Siege | FleetcrawlComboTag.Kinetic,
                MinQuality = FleetcrawlLimbQualityTier.Common,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 10,
                MinLevel = 1,
                TurnRateMultiplier = 0.97f,
                AccelerationMultiplier = 1f,
                DecelerationMultiplier = 1f,
                MaxSpeedMultiplier = 1f,
                CooldownMultiplier = 0.94f,
                DamageMultiplier = 1.12f
            });
            limbDefs.Add(new FleetcrawlModuleLimbDefinition
            {
                LimbId = new FixedString64Bytes("limb_stabilizer_vectored"),
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Stabilizer,
                SharingMode = FleetcrawlLimbSharingMode.Shared,
                ComboTags = FleetcrawlComboTag.Agile,
                MinQuality = FleetcrawlLimbQualityTier.Common,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 12,
                MinLevel = 1,
                TurnRateMultiplier = 1.12f,
                AccelerationMultiplier = 1.08f,
                DecelerationMultiplier = 1.03f,
                MaxSpeedMultiplier = 1.04f,
                CooldownMultiplier = 1f,
                DamageMultiplier = 1f
            });
            limbDefs.Add(new FleetcrawlModuleLimbDefinition
            {
                LimbId = new FixedString64Bytes("limb_reactor_flux_core"),
                ModuleType = FleetcrawlModuleType.Reactor,
                Slot = FleetcrawlLimbSlot.Core,
                SharingMode = FleetcrawlLimbSharingMode.Unique,
                ComboTags = FleetcrawlComboTag.Flux,
                MinQuality = FleetcrawlLimbQualityTier.Uncommon,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 9,
                MinLevel = 2,
                TurnRateMultiplier = 1f,
                AccelerationMultiplier = 1.07f,
                DecelerationMultiplier = 1.06f,
                MaxSpeedMultiplier = 1.08f,
                CooldownMultiplier = 0.92f,
                DamageMultiplier = 1.06f
            });
            limbDefs.Add(new FleetcrawlModuleLimbDefinition
            {
                LimbId = new FixedString64Bytes("limb_hangar_relay"),
                ModuleType = FleetcrawlModuleType.Hangar,
                Slot = FleetcrawlLimbSlot.Utility,
                SharingMode = FleetcrawlLimbSharingMode.Shared,
                ComboTags = FleetcrawlComboTag.Drone | FleetcrawlComboTag.Support,
                MinQuality = FleetcrawlLimbQualityTier.Uncommon,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 8,
                MinLevel = 3,
                TurnRateMultiplier = 1f,
                AccelerationMultiplier = 1.05f,
                DecelerationMultiplier = 1.04f,
                MaxSpeedMultiplier = 1.03f,
                CooldownMultiplier = 0.9f,
                DamageMultiplier = 1.08f
            });

            var affixDefs = em.AddBuffer<FleetcrawlLimbAffixDefinition>(runtime);
            affixDefs.Add(new FleetcrawlLimbAffixDefinition
            {
                AffixId = new FixedString64Bytes("affix_precise"),
                Slot = FleetcrawlLimbSlot.Stabilizer,
                ComboTags = FleetcrawlComboTag.Agile,
                MinQuality = FleetcrawlLimbQualityTier.Common,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 10,
                TurnRateMultiplier = 1.08f,
                AccelerationMultiplier = 1f,
                DecelerationMultiplier = 1f,
                MaxSpeedMultiplier = 1f,
                CooldownMultiplier = 1f,
                DamageMultiplier = 1f
            });
            affixDefs.Add(new FleetcrawlLimbAffixDefinition
            {
                AffixId = new FixedString64Bytes("affix_overclocked"),
                Slot = FleetcrawlLimbSlot.Core,
                ComboTags = FleetcrawlComboTag.Flux,
                MinQuality = FleetcrawlLimbQualityTier.Uncommon,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 8,
                TurnRateMultiplier = 1f,
                AccelerationMultiplier = 1.04f,
                DecelerationMultiplier = 1f,
                MaxSpeedMultiplier = 1.02f,
                CooldownMultiplier = 0.93f,
                DamageMultiplier = 1.05f
            });
            affixDefs.Add(new FleetcrawlLimbAffixDefinition
            {
                AffixId = new FixedString64Bytes("affix_heavy_payload"),
                Slot = FleetcrawlLimbSlot.Barrel,
                ComboTags = FleetcrawlComboTag.Siege,
                MinQuality = FleetcrawlLimbQualityTier.Common,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 9,
                TurnRateMultiplier = 0.96f,
                AccelerationMultiplier = 1f,
                DecelerationMultiplier = 1f,
                MaxSpeedMultiplier = 1f,
                CooldownMultiplier = 0.97f,
                DamageMultiplier = 1.1f
            });

            var hullDefs = em.AddBuffer<FleetcrawlHullSegmentDefinition>(runtime);
            hullDefs.Add(new FleetcrawlHullSegmentDefinition
            {
                SegmentId = new FixedString64Bytes("hull_bastion_ring"),
                ManufacturerId = new FixedString64Bytes("bastion"),
                SetId = new FixedString64Bytes("set_bastion"),
                ComboTags = FleetcrawlComboTag.Vanguard | FleetcrawlComboTag.Support,
                MinQuality = FleetcrawlLimbQualityTier.Uncommon,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 9,
                MinLevel = 3,
                ModuleSocketCount = 2,
                TurnRateMultiplier = 0.98f,
                AccelerationMultiplier = 1.02f,
                DecelerationMultiplier = 1.05f,
                MaxSpeedMultiplier = 1.01f,
                CooldownMultiplier = 0.98f,
                DamageMultiplier = 1.04f
            });
            hullDefs.Add(new FleetcrawlHullSegmentDefinition
            {
                SegmentId = new FixedString64Bytes("hull_raider_spine"),
                ManufacturerId = new FixedString64Bytes("raiderworks"),
                SetId = new FixedString64Bytes("set_raider"),
                ComboTags = FleetcrawlComboTag.Agile | FleetcrawlComboTag.Kinetic,
                MinQuality = FleetcrawlLimbQualityTier.Common,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 11,
                MinLevel = 2,
                ModuleSocketCount = 1,
                TurnRateMultiplier = 1.08f,
                AccelerationMultiplier = 1.07f,
                DecelerationMultiplier = 1.04f,
                MaxSpeedMultiplier = 1.05f,
                CooldownMultiplier = 1f,
                DamageMultiplier = 1.02f
            });

            var trinketDefs = em.AddBuffer<FleetcrawlTrinketDefinition>(runtime);
            trinketDefs.Add(new FleetcrawlTrinketDefinition
            {
                TrinketId = new FixedString64Bytes("trinket_prism_refractor"),
                ManufacturerId = new FixedString64Bytes("prismworks"),
                SetId = new FixedString64Bytes("set_prism"),
                ComboTags = FleetcrawlComboTag.Arc | FleetcrawlComboTag.Flux,
                WeaponBehaviors = FleetcrawlWeaponBehaviorTag.BeamFork | FleetcrawlWeaponBehaviorTag.Ionize,
                SkillFamily = FleetcrawlSkillFamily.Support,
                MinQuality = FleetcrawlLimbQualityTier.Uncommon,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 8,
                MinLevel = 4,
                CooldownMultiplier = 0.95f,
                DamageMultiplier = 1.08f
            });
            trinketDefs.Add(new FleetcrawlTrinketDefinition
            {
                TrinketId = new FixedString64Bytes("trinket_kinetic_gyrostab"),
                ManufacturerId = new FixedString64Bytes("baseline"),
                SetId = new FixedString64Bytes("set_raider"),
                ComboTags = FleetcrawlComboTag.Agile | FleetcrawlComboTag.Kinetic,
                WeaponBehaviors = FleetcrawlWeaponBehaviorTag.Ricochet | FleetcrawlWeaponBehaviorTag.Pierce,
                SkillFamily = FleetcrawlSkillFamily.Ordnance,
                MinQuality = FleetcrawlLimbQualityTier.Common,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 10,
                MinLevel = 3,
                CooldownMultiplier = 0.97f,
                DamageMultiplier = 1.06f
            });

            var itemDefs = em.AddBuffer<FleetcrawlGeneralItemDefinition>(runtime);
            itemDefs.Add(new FleetcrawlGeneralItemDefinition
            {
                ItemId = new FixedString64Bytes("item_nanite_cache"),
                ManufacturerId = new FixedString64Bytes("civfoundry"),
                SetId = new FixedString64Bytes("set_support"),
                ComboTags = FleetcrawlComboTag.Support,
                SkillFamily = FleetcrawlSkillFamily.Defense,
                MinQuality = FleetcrawlLimbQualityTier.Common,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 12,
                MinLevel = 2,
                MaxStackCount = 3,
                CooldownMultiplier = 0.99f,
                DamageMultiplier = 1.03f
            });
            itemDefs.Add(new FleetcrawlGeneralItemDefinition
            {
                ItemId = new FixedString64Bytes("item_flux_capsule"),
                ManufacturerId = new FixedString64Bytes("prismworks"),
                SetId = new FixedString64Bytes("set_prism"),
                ComboTags = FleetcrawlComboTag.Flux,
                SkillFamily = FleetcrawlSkillFamily.Support,
                MinQuality = FleetcrawlLimbQualityTier.Uncommon,
                MaxQuality = FleetcrawlLimbQualityTier.Legendary,
                Weight = 8,
                MinLevel = 3,
                MaxStackCount = 2,
                CooldownMultiplier = 0.95f,
                DamageMultiplier = 1.05f
            });

            var setDefs = em.AddBuffer<FleetcrawlSetBonusDefinition>(runtime);
            setDefs.Add(new FleetcrawlSetBonusDefinition
            {
                SetId = new FixedString64Bytes("set_raider"),
                ManufacturerId = new FixedString64Bytes("baseline"),
                RequiredItemTags = FleetcrawlComboTag.Agile | FleetcrawlComboTag.Kinetic,
                RequiredWeaponBehaviors = FleetcrawlWeaponBehaviorTag.Ricochet,
                RequiredSkillFamily = FleetcrawlSkillFamily.Ordnance,
                RequiredCount = 2,
                TurnRateMultiplier = 1.05f,
                AccelerationMultiplier = 1.04f,
                DecelerationMultiplier = 1.02f,
                MaxSpeedMultiplier = 1.04f,
                CooldownMultiplier = 0.96f,
                DamageMultiplier = 1.08f
            });
            setDefs.Add(new FleetcrawlSetBonusDefinition
            {
                SetId = new FixedString64Bytes("set_prism"),
                ManufacturerId = new FixedString64Bytes("prismworks"),
                RequiredItemTags = FleetcrawlComboTag.Arc | FleetcrawlComboTag.Flux,
                RequiredWeaponBehaviors = FleetcrawlWeaponBehaviorTag.BeamFork,
                RequiredSkillFamily = FleetcrawlSkillFamily.Support,
                RequiredCount = 2,
                TurnRateMultiplier = 1.03f,
                AccelerationMultiplier = 1.02f,
                DecelerationMultiplier = 1.01f,
                MaxSpeedMultiplier = 1.02f,
                CooldownMultiplier = 0.9f,
                DamageMultiplier = 1.1f
            });

            em.AddBuffer<FleetcrawlCurrencyShopOfferEntry>(runtime);
            em.AddBuffer<FleetcrawlLootOfferEntry>(runtime);
            em.AddBuffer<FleetcrawlRolledLimbBufferElement>(runtime);
            em.AddBuffer<FleetcrawlOwnedItem>(runtime);

            Debug.Log($"[FleetcrawlMeta] Loot/shop bootstrap ready. shop_catalog={shopCatalog.Length} loot_catalog={lootCatalog.Length} limbs={limbDefs.Length} affixes={affixDefs.Length} hulls={hullDefs.Length} trinkets={trinketDefs.Length} items={itemDefs.Length} sets={setDefs.Length}.");
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlRoomDirectorSystem))]
    [UpdateBefore(typeof(Space4XFleetcrawlOfferGenerationSystem))]
    public partial struct Space4XFleetcrawlRunStateBridgeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleetcrawlDirectorState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var directorEntity = SystemAPI.GetSingletonEntity<Space4XFleetcrawlDirectorState>();

            if (!em.HasComponent<FleetcrawlRunLevelState>(directorEntity))
            {
                em.AddComponentData(directorEntity, new FleetcrawlRunLevelState { Level = 1 });
            }
            if (!em.HasComponent<FleetcrawlRunExperience>(directorEntity))
            {
                em.AddComponentData(directorEntity, new FleetcrawlRunExperience { Value = 0 });
            }
            if (!em.HasComponent<FleetcrawlRunShardWallet>(directorEntity))
            {
                em.AddComponentData(directorEntity, new FleetcrawlRunShardWallet { Shards = 0 });
            }
            if (!em.HasComponent<FleetcrawlRunChallengeState>(directorEntity))
            {
                em.AddComponentData(directorEntity, new FleetcrawlRunChallengeState { Challenge = 0 });
            }

            var level = 1;
            var xp = 0;
            if (em.HasComponent<Space4XRunProgressionState>(directorEntity))
            {
                var progression = em.GetComponentData<Space4XRunProgressionState>(directorEntity);
                level = math.max(1, progression.Level);
                xp = math.max(0, progression.TotalExperienceEarned);
            }

            var shards = 0;
            if (em.HasComponent<Space4XRunMetaResourceState>(directorEntity))
            {
                var meta = em.GetComponentData<Space4XRunMetaResourceState>(directorEntity);
                shards = math.max(0, meta.Shards);
            }

            var challenge = 0;
            if (em.HasComponent<Space4XRunChallengeState>(directorEntity))
            {
                var runChallenge = em.GetComponentData<Space4XRunChallengeState>(directorEntity);
                challenge = runChallenge.Active != 0 ? math.max(0, runChallenge.RiskTier) : 0;
            }

            em.SetComponentData(directorEntity, new FleetcrawlRunLevelState { Level = level });
            em.SetComponentData(directorEntity, new FleetcrawlRunExperience { Value = xp });
            em.SetComponentData(directorEntity, new FleetcrawlRunShardWallet { Shards = shards });
            em.SetComponentData(directorEntity, new FleetcrawlRunChallengeState { Challenge = challenge });
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlLootShopBootstrapSystem))]
    [UpdateAfter(typeof(Space4XFleetcrawlRunStateBridgeSystem))]
    public partial struct Space4XFleetcrawlOfferGenerationSystem : ISystem
    {
        private EntityQuery _refreshRequestQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleetcrawlSeeded>();
            state.RequireForUpdate<Space4XFleetcrawlDirectorState>();
            state.RequireForUpdate<FleetcrawlOfferRuntimeTag>();
            _refreshRequestQuery = state.GetEntityQuery(ComponentType.ReadOnly<FleetcrawlOfferRefreshRequest>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var runtimeEntity = SystemAPI.GetSingletonEntity<FleetcrawlOfferRuntimeTag>();
            var config = em.GetComponentData<FleetcrawlOfferGenerationConfig>(runtimeEntity);
            var cache = em.GetComponentData<FleetcrawlOfferGenerationCache>(runtimeEntity);

            var directorEntity = SystemAPI.GetSingletonEntity<Space4XFleetcrawlDirectorState>();
            var director = em.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
            var roomIndex = math.max(0, director.CurrentRoomIndex);
            var level = 1;
            if (em.HasComponent<FleetcrawlRunLevelState>(directorEntity))
            {
                level = math.max(1, em.GetComponentData<FleetcrawlRunLevelState>(directorEntity).Level);
            }

            var xp = em.HasComponent<FleetcrawlRunExperience>(directorEntity)
                ? math.max(0, em.GetComponentData<FleetcrawlRunExperience>(directorEntity).Value)
                : 0;
            var shards = em.HasComponent<FleetcrawlRunShardWallet>(directorEntity)
                ? math.max(0, em.GetComponentData<FleetcrawlRunShardWallet>(directorEntity).Shards)
                : 0;
            var challenge = em.HasComponent<FleetcrawlRunChallengeState>(directorEntity)
                ? math.max(0, em.GetComponentData<FleetcrawlRunChallengeState>(directorEntity).Challenge)
                : 0;

            var hasRefreshRequest = !_refreshRequestQuery.IsEmptyIgnoreFilter;
            var requestNonce = 0u;
            var source = new FixedString32Bytes("state");
            if (hasRefreshRequest)
            {
                foreach (var request in SystemAPI.Query<RefRO<FleetcrawlOfferRefreshRequest>>())
                {
                    requestNonce = math.max(requestNonce, request.ValueRO.Nonce);
                    if (!request.ValueRO.SourceTag.IsEmpty)
                    {
                        source = request.ValueRO.SourceTag;
                    }
                }
            }

            var signature = FleetcrawlDeterministicOfferGeneration.ComputeSignature(
                director.Seed,
                roomIndex,
                level,
                xp,
                shards,
                challenge,
                requestNonce);

            var currencyOffers = em.GetBuffer<FleetcrawlCurrencyShopOfferEntry>(runtimeEntity);
            var lootOffers = em.GetBuffer<FleetcrawlLootOfferEntry>(runtimeEntity);
            if (!hasRefreshRequest &&
                signature == cache.LastSignature &&
                currencyOffers.Length > 0 &&
                lootOffers.Length > 0)
            {
                return;
            }

            var shopCatalog = em.GetBuffer<FleetcrawlCurrencyShopCatalogEntry>(runtimeEntity);
            var lootCatalog = em.GetBuffer<FleetcrawlLootOfferCatalogEntry>(runtimeEntity);
            var limbDefinitions = em.GetBuffer<FleetcrawlModuleLimbDefinition>(runtimeEntity);
            var affixDefinitions = em.GetBuffer<FleetcrawlLimbAffixDefinition>(runtimeEntity);
            var hullDefinitions = em.GetBuffer<FleetcrawlHullSegmentDefinition>(runtimeEntity);
            var trinketDefinitions = em.GetBuffer<FleetcrawlTrinketDefinition>(runtimeEntity);
            var itemDefinitions = em.GetBuffer<FleetcrawlGeneralItemDefinition>(runtimeEntity);

            currencyOffers.Clear();
            lootOffers.Clear();

            for (var slot = 0; slot < math.max(1, config.CurrencySlotCount); slot++)
            {
                var offerHash = FleetcrawlDeterministicOfferGeneration.ComputeOfferHash(
                    director.Seed,
                    roomIndex,
                    level,
                    xp,
                    shards,
                    challenge,
                    requestNonce,
                    channel: 0,
                    slotIndex: slot,
                    refreshCount: cache.RefreshCount);

                var index = PickWeightedShopIndex(shopCatalog, level, offerHash);
                if (index < 0)
                {
                    continue;
                }

                var catalogRow = shopCatalog[index];
                var price = math.max(1, catalogRow.BasePriceShards + (challenge * 8) + (level * 2));
                currencyOffers.Add(new FleetcrawlCurrencyShopOfferEntry
                {
                    SlotIndex = slot,
                    OfferId = catalogRow.OfferId,
                    SkuId = catalogRow.SkuId,
                    PriceShards = price,
                    ComboTags = catalogRow.ComboTags,
                    RollHash = offerHash
                });
            }

            for (var slot = 0; slot < math.max(1, config.LootSlotCount); slot++)
            {
                var offerHash = FleetcrawlDeterministicOfferGeneration.ComputeOfferHash(
                    director.Seed,
                    roomIndex,
                    level,
                    xp,
                    shards,
                    challenge,
                    requestNonce,
                    channel: 1,
                    slotIndex: slot,
                    refreshCount: cache.RefreshCount);

                var index = PickWeightedLootIndex(lootCatalog, level, offerHash);
                if (index < 0)
                {
                    continue;
                }

                var catalogRow = lootCatalog[index];
                var rollStream = cache.RefreshCount * 97 + slot + (int)requestNonce;
                var rolledLimb = default(FleetcrawlRolledLimb);
                FleetcrawlRolledItem rolledItem;
                switch (catalogRow.Archetype)
                {
                    case FleetcrawlLootArchetype.HullSegment:
                        rolledItem = FleetcrawlDeterministicLimbRollService.RollHullSegment(
                            director.Seed,
                            roomIndex,
                            level,
                            catalogRow.ItemId,
                            rollStream,
                            hullDefinitions);
                        break;
                    case FleetcrawlLootArchetype.Trinket:
                        rolledItem = FleetcrawlDeterministicLimbRollService.RollTrinket(
                            director.Seed,
                            roomIndex,
                            level,
                            catalogRow.ItemId,
                            rollStream,
                            trinketDefinitions);
                        break;
                    case FleetcrawlLootArchetype.GeneralItem:
                        rolledItem = FleetcrawlDeterministicLimbRollService.RollGeneralItem(
                            director.Seed,
                            roomIndex,
                            level,
                            catalogRow.ItemId,
                            rollStream,
                            itemDefinitions);
                        break;
                    default:
                        rolledLimb = FleetcrawlDeterministicLimbRollService.RollLimb(
                            director.Seed,
                            roomIndex,
                            level,
                            catalogRow.ModuleType,
                            catalogRow.Slot,
                            rollStream,
                            limbDefinitions,
                            affixDefinitions);
                        rolledItem = FleetcrawlDeterministicLimbRollService.FromLimb(
                            rolledLimb,
                            catalogRow.ManufacturerId,
                            catalogRow.SetId,
                            catalogRow.WeaponBehaviors,
                            catalogRow.SkillFamily);
                        break;
                }

                var behaviorCost = CountBehaviorFlags(rolledItem.WeaponBehaviors) * 5;
                var socketCost = math.max(0, rolledItem.ModuleSocketCount) * 4;
                var stackCost = math.max(0, rolledItem.StackCount - 1) * 2;
                var qualityCost = (int)rolledItem.Quality * 6;
                var price = math.max(1, catalogRow.BaseShardCost + (challenge * 5) + qualityCost + behaviorCost + socketCost + stackCost);
                lootOffers.Add(new FleetcrawlLootOfferEntry
                {
                    SlotIndex = slot,
                    OfferId = catalogRow.OfferId,
                    Archetype = catalogRow.Archetype,
                    ModuleType = catalogRow.ModuleType,
                    Slot = catalogRow.Slot,
                    ItemId = rolledItem.ItemId,
                    ManufacturerId = rolledItem.ManufacturerId,
                    SetId = rolledItem.SetId,
                    PriceShards = price,
                    Quality = rolledItem.Quality,
                    ModuleSocketCount = rolledItem.ModuleSocketCount,
                    StackCount = rolledItem.StackCount,
                    WeaponBehaviors = rolledItem.WeaponBehaviors,
                    SkillFamily = rolledItem.SkillFamily,
                    RolledLimb = rolledLimb,
                    RolledItem = rolledItem,
                    ComboTags = catalogRow.ComboTags | rolledItem.ComboTags,
                    RollHash = offerHash
                });
            }

            cache.LastSignature = signature;
            cache.RefreshCount += 1;
            cache.LastRoomIndex = roomIndex;
            cache.LastLevel = level;
            cache.LastXp = xp;
            cache.LastShards = shards;
            cache.LastChallenge = challenge;
            em.SetComponentData(runtimeEntity, cache);

            if (hasRefreshRequest)
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                foreach (var (_, entity) in SystemAPI.Query<RefRO<FleetcrawlOfferRefreshRequest>>().WithEntityAccess())
                {
                    ecb.DestroyEntity(entity);
                }

                ecb.Playback(em);
                ecb.Dispose();
            }

            Debug.Log($"[FleetcrawlMeta] OfferRefresh source={source} room={roomIndex} level={level} xp={xp} shards={shards} challenge={challenge} signature={signature} currency_offers={currencyOffers.Length} loot_offers={lootOffers.Length}.");
        }

        private static int PickWeightedShopIndex(DynamicBuffer<FleetcrawlCurrencyShopCatalogEntry> catalog, int level, uint hash)
        {
            var total = 0;
            for (var i = 0; i < catalog.Length; i++)
            {
                var row = catalog[i];
                if (level < row.MinLevel)
                {
                    continue;
                }

                total += math.max(0, row.Weight);
            }

            if (total <= 0)
            {
                return -1;
            }

            var pick = (int)(hash % (uint)total);
            var cursor = 0;
            for (var i = 0; i < catalog.Length; i++)
            {
                var row = catalog[i];
                if (level < row.MinLevel)
                {
                    continue;
                }

                var weight = math.max(0, row.Weight);
                if (weight <= 0)
                {
                    continue;
                }

                cursor += weight;
                if (pick < cursor)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int PickWeightedLootIndex(DynamicBuffer<FleetcrawlLootOfferCatalogEntry> catalog, int level, uint hash)
        {
            var total = 0;
            for (var i = 0; i < catalog.Length; i++)
            {
                var row = catalog[i];
                if (level < row.MinLevel)
                {
                    continue;
                }

                total += math.max(0, row.Weight);
            }

            if (total <= 0)
            {
                return -1;
            }

            var pick = (int)(hash % (uint)total);
            var cursor = 0;
            for (var i = 0; i < catalog.Length; i++)
            {
                var row = catalog[i];
                if (level < row.MinLevel)
                {
                    continue;
                }

                var weight = math.max(0, row.Weight);
                if (weight <= 0)
                {
                    continue;
                }

                cursor += weight;
                if (pick < cursor)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int CountBehaviorFlags(FleetcrawlWeaponBehaviorTag flags)
        {
            var value = (uint)flags;
            var count = 0;
            while (value != 0u)
            {
                count += (int)(value & 1u);
                value >>= 1;
            }
            return count;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetcrawlOfferGenerationSystem))]
    public partial struct Space4XFleetcrawlPurchaseApplySystem : ISystem
    {
        private EntityQuery _purchaseRequestQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleetcrawlDirectorState>();
            state.RequireForUpdate<FleetcrawlOfferRuntimeTag>();
            _purchaseRequestQuery = state.GetEntityQuery(ComponentType.ReadOnly<FleetcrawlPurchaseRequest>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var runtimeEntity = SystemAPI.GetSingletonEntity<FleetcrawlOfferRuntimeTag>();
            var directorEntity = SystemAPI.GetSingletonEntity<Space4XFleetcrawlDirectorState>();
            var director = em.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
            var cache = em.GetComponentData<FleetcrawlOfferGenerationCache>(runtimeEntity);
            var purchaseState = em.GetComponentData<FleetcrawlPurchaseRuntimeState>(runtimeEntity);
            var roomIndex = math.max(0, director.CurrentRoomIndex);

            if (!em.HasComponent<FleetcrawlRunShardWallet>(directorEntity))
            {
                em.AddComponentData(directorEntity, new FleetcrawlRunShardWallet { Shards = 0 });
            }
            if (!em.HasComponent<Space4XRunMetaResourceState>(directorEntity))
            {
                em.AddComponentData(directorEntity, new Space4XRunMetaResourceState());
            }

            var wallet = em.GetComponentData<FleetcrawlRunShardWallet>(directorEntity);
            var meta = em.GetComponentData<Space4XRunMetaResourceState>(directorEntity);
            var lootOffers = em.GetBuffer<FleetcrawlLootOfferEntry>(runtimeEntity);
            var shopOffers = em.GetBuffer<FleetcrawlCurrencyShopOfferEntry>(runtimeEntity);
            var ownedItems = em.GetBuffer<FleetcrawlOwnedItem>(runtimeEntity);
            var rolledLimbs = em.GetBuffer<FleetcrawlRolledLimbBufferElement>(runtimeEntity);
            var limbDefs = em.GetBuffer<FleetcrawlModuleLimbDefinition>(runtimeEntity);
            var affixDefs = em.GetBuffer<FleetcrawlLimbAffixDefinition>(runtimeEntity);
            var upgradeDefs = em.GetBuffer<FleetcrawlModuleUpgradeDefinition>(runtimeEntity);
            var setDefs = em.GetBuffer<FleetcrawlSetBonusDefinition>(runtimeEntity);

            var hasManualRequest = !_purchaseRequestQuery.IsEmptyIgnoreFilter;
            var selected = default(FleetcrawlPurchaseRequest);
            var hasSelected = false;
            if (hasManualRequest)
            {
                foreach (var request in SystemAPI.Query<RefRO<FleetcrawlPurchaseRequest>>())
                {
                    if (!hasSelected ||
                        request.ValueRO.Nonce > selected.Nonce ||
                        (request.ValueRO.Nonce == selected.Nonce && request.ValueRO.SlotIndex < selected.SlotIndex))
                    {
                        selected = request.ValueRO;
                        hasSelected = true;
                    }
                }
            }
            else if (cache.LastSignature != 0u && purchaseState.LastAutoPurchaseRoomIndex != roomIndex)
            {
                var autoPick = ResolveAutoPurchase(lootOffers, shopOffers, wallet.Shards);
                if (autoPick.HasValue)
                {
                    selected = autoPick.Value;
                    selected.SourceTag = new FixedString32Bytes("auto");
                    selected.Nonce = purchaseState.LastProcessedRequestNonce + 1u;
                    hasSelected = true;
                }
            }

            if (!hasSelected)
            {
                if (hasManualRequest)
                {
                    ClearPurchaseRequests(ref state);
                }
                return;
            }

            var purchased = false;
            var purchaseLabel = new FixedString64Bytes();
            if (selected.Channel == FleetcrawlOfferChannel.Loot)
            {
                var index = FindLootOfferIndexBySlot(lootOffers, selected.SlotIndex);
                if (index >= 0)
                {
                    var offer = lootOffers[index];
                    if (wallet.Shards >= offer.PriceShards)
                    {
                        var before = FleetcrawlModuleUpgradeResolver.ResolveAggregateWithInventory(
                            rolledLimbs, ownedItems, limbDefs, affixDefs, upgradeDefs, setDefs);

                        wallet.Shards = math.max(0, wallet.Shards - offer.PriceShards);
                        meta.Shards = wallet.Shards;
                        em.SetComponentData(directorEntity, wallet);
                        em.SetComponentData(directorEntity, meta);

                        ownedItems.Add(new FleetcrawlOwnedItem { Value = offer.RolledItem });
                        if (offer.Archetype == FleetcrawlLootArchetype.ModuleLimb)
                        {
                            rolledLimbs.Add(new FleetcrawlRolledLimbBufferElement { Value = offer.RolledLimb });
                        }

                        var after = FleetcrawlModuleUpgradeResolver.ResolveAggregateWithInventory(
                            rolledLimbs, ownedItems, limbDefs, affixDefs, upgradeDefs, setDefs);
                        var delta = ResolveDelta(before, after);
                        ApplyDeltaToPlayers(ref state, delta);

                        lootOffers.RemoveAt(index);
                        purchased = true;
                        purchaseLabel = offer.ItemId;
                    }
                }
            }
            else
            {
                var index = FindShopOfferIndexBySlot(shopOffers, selected.SlotIndex);
                if (index >= 0)
                {
                    var offer = shopOffers[index];
                    if (wallet.Shards >= offer.PriceShards)
                    {
                        wallet.Shards = math.max(0, wallet.Shards - offer.PriceShards);
                        meta.Shards = wallet.Shards;
                        em.SetComponentData(directorEntity, wallet);
                        em.SetComponentData(directorEntity, meta);
                        ApplyShopReward(em, directorEntity, offer.SkuId);
                        shopOffers.RemoveAt(index);
                        purchased = true;
                        purchaseLabel = offer.SkuId;
                    }
                }
            }

            if (purchased)
            {
                purchaseState.LastProcessedSignature = cache.LastSignature;
                purchaseState.LastProcessedRequestNonce = math.max(purchaseState.LastProcessedRequestNonce, selected.Nonce);
                purchaseState.PurchasesResolved++;
                if (!hasManualRequest)
                {
                    purchaseState.LastAutoPurchaseRoomIndex = roomIndex;
                }
                em.SetComponentData(runtimeEntity, purchaseState);

                var refresh = em.CreateEntity(typeof(FleetcrawlOfferRefreshRequest));
                em.SetComponentData(refresh, new FleetcrawlOfferRefreshRequest
                {
                    Nonce = (uint)purchaseState.PurchasesResolved,
                    SourceTag = selected.SourceTag.IsEmpty ? new FixedString32Bytes("purchase") : selected.SourceTag
                });

                Debug.Log($"[FleetcrawlMeta] PURCHASE channel={selected.Channel} slot={selected.SlotIndex} room={roomIndex} shards={wallet.Shards} item={purchaseLabel}.");
            }

            if (hasManualRequest)
            {
                ClearPurchaseRequests(ref state);
            }
        }

        private static FleetcrawlPurchaseRequest? ResolveAutoPurchase(
            DynamicBuffer<FleetcrawlLootOfferEntry> lootOffers,
            DynamicBuffer<FleetcrawlCurrencyShopOfferEntry> shopOffers,
            int shards)
        {
            var bestLootScore = int.MinValue;
            var bestLootHash = uint.MaxValue;
            var bestLootSlot = -1;
            for (var i = 0; i < lootOffers.Length; i++)
            {
                var offer = lootOffers[i];
                if (offer.PriceShards > shards)
                {
                    continue;
                }

                var score = ComputeLootScore(offer);
                if (score > bestLootScore || (score == bestLootScore && offer.RollHash < bestLootHash))
                {
                    bestLootScore = score;
                    bestLootHash = offer.RollHash;
                    bestLootSlot = offer.SlotIndex;
                }
            }

            if (bestLootSlot >= 0)
            {
                return new FleetcrawlPurchaseRequest
                {
                    Channel = FleetcrawlOfferChannel.Loot,
                    SlotIndex = bestLootSlot
                };
            }

            var bestShopScore = int.MinValue;
            var bestShopHash = uint.MaxValue;
            var bestShopSlot = -1;
            for (var i = 0; i < shopOffers.Length; i++)
            {
                var offer = shopOffers[i];
                if (offer.PriceShards > shards)
                {
                    continue;
                }

                var score = ComputeShopScore(offer);
                if (score > bestShopScore || (score == bestShopScore && offer.RollHash < bestShopHash))
                {
                    bestShopScore = score;
                    bestShopHash = offer.RollHash;
                    bestShopSlot = offer.SlotIndex;
                }
            }

            if (bestShopSlot >= 0)
            {
                return new FleetcrawlPurchaseRequest
                {
                    Channel = FleetcrawlOfferChannel.CurrencyShop,
                    SlotIndex = bestShopSlot
                };
            }

            return null;
        }

        private static int ComputeLootScore(in FleetcrawlLootOfferEntry offer)
        {
            var comboBits = CountComboFlags(offer.ComboTags);
            var behaviorBits = CountBehaviorFlags(offer.WeaponBehaviors);
            var qualityScore = (int)offer.Quality * 100;
            var archetypeBias = offer.Archetype switch
            {
                FleetcrawlLootArchetype.Trinket => 55,
                FleetcrawlLootArchetype.HullSegment => 45,
                FleetcrawlLootArchetype.ModuleLimb => 40,
                _ => 24
            };

            return archetypeBias +
                   qualityScore +
                   comboBits * 16 +
                   behaviorBits * 20 +
                   math.max(0, offer.ModuleSocketCount) * 22 +
                   math.max(0, offer.StackCount - 1) * 10;
        }

        private static int ComputeShopScore(in FleetcrawlCurrencyShopOfferEntry offer)
        {
            var score = CountComboFlags(offer.ComboTags) * 6;
            if (offer.SkuId.Equals(new FixedString64Bytes("ent_radar_uplink")))
            {
                score += 30;
            }
            score += math.max(0, 200 - offer.PriceShards);
            return score;
        }

        private static int FindLootOfferIndexBySlot(DynamicBuffer<FleetcrawlLootOfferEntry> offers, int slotIndex)
        {
            for (var i = 0; i < offers.Length; i++)
            {
                if (offers[i].SlotIndex == slotIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int FindShopOfferIndexBySlot(DynamicBuffer<FleetcrawlCurrencyShopOfferEntry> offers, int slotIndex)
        {
            for (var i = 0; i < offers.Length; i++)
            {
                if (offers[i].SlotIndex == slotIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        private static FleetcrawlResolvedUpgradeStats ResolveDelta(in FleetcrawlResolvedUpgradeStats before, in FleetcrawlResolvedUpgradeStats after)
        {
            return new FleetcrawlResolvedUpgradeStats
            {
                TurnRateMultiplier = SafeRatio(after.TurnRateMultiplier, before.TurnRateMultiplier),
                AccelerationMultiplier = SafeRatio(after.AccelerationMultiplier, before.AccelerationMultiplier),
                DecelerationMultiplier = SafeRatio(after.DecelerationMultiplier, before.DecelerationMultiplier),
                MaxSpeedMultiplier = SafeRatio(after.MaxSpeedMultiplier, before.MaxSpeedMultiplier),
                CooldownMultiplier = SafeRatio(after.CooldownMultiplier, before.CooldownMultiplier),
                DamageMultiplier = SafeRatio(after.DamageMultiplier, before.DamageMultiplier)
            };
        }

        private static float SafeRatio(float after, float before)
        {
            return before > 1e-6f ? math.max(0.01f, after / before) : math.max(0.01f, after);
        }

        private void ApplyDeltaToPlayers(ref SystemState state, in FleetcrawlResolvedUpgradeStats delta)
        {
            foreach (var (carrierRef, movementRef) in SystemAPI.Query<RefRW<Carrier>, RefRW<VesselMovement>>().WithAll<Space4XRunPlayerTag>())
            {
                var carrier = carrierRef.ValueRO;
                carrier.Speed *= delta.MaxSpeedMultiplier;
                carrier.Acceleration *= delta.AccelerationMultiplier;
                carrier.Deceleration *= delta.DecelerationMultiplier;
                carrier.TurnSpeed *= delta.TurnRateMultiplier;
                carrierRef.ValueRW = carrier;

                var movement = movementRef.ValueRO;
                movement.BaseSpeed *= delta.MaxSpeedMultiplier;
                movement.Acceleration *= delta.AccelerationMultiplier;
                movement.Deceleration *= delta.DecelerationMultiplier;
                movement.TurnSpeed *= delta.TurnRateMultiplier;
                movementRef.ValueRW = movement;
            }

            foreach (var weapons in SystemAPI.Query<DynamicBuffer<WeaponMount>>().WithAll<Space4XRunPlayerTag>())
            {
                var weaponBuffer = weapons;
                for (var i = 0; i < weaponBuffer.Length; i++)
                {
                    var mount = weaponBuffer[i];
                    mount.Weapon.BaseDamage = math.max(0.01f, mount.Weapon.BaseDamage * delta.DamageMultiplier);
                    mount.Weapon.CooldownTicks = (ushort)math.max(1, (int)math.round(mount.Weapon.CooldownTicks * delta.CooldownMultiplier));
                    weaponBuffer[i] = mount;
                }
            }
        }

        private static void ApplyShopReward(EntityManager em, Entity directorEntity, in FixedString64Bytes skuId)
        {
            if (!em.HasComponent<RunCurrency>(directorEntity))
            {
                em.AddComponentData(directorEntity, new RunCurrency());
            }
            if (!em.HasComponent<Space4XRunRerollTokens>(directorEntity))
            {
                em.AddComponentData(directorEntity, new Space4XRunRerollTokens());
            }

            if (skuId.Equals(new FixedString64Bytes("ent_radar_uplink")))
            {
                var reroll = em.GetComponentData<Space4XRunRerollTokens>(directorEntity);
                reroll.Value += 1;
                em.SetComponentData(directorEntity, reroll);
                return;
            }

            var currency = em.GetComponentData<RunCurrency>(directorEntity);
            if (skuId.Equals(new FixedString64Bytes("soft_pack_small")))
            {
                currency.Value += 65;
            }
            else if (skuId.Equals(new FixedString64Bytes("soft_pack_medium")))
            {
                currency.Value += 130;
            }
            else
            {
                currency.Value += 35;
            }
            em.SetComponentData(directorEntity, currency);
        }

        private void ClearPurchaseRequests(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<FleetcrawlPurchaseRequest>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static int CountComboFlags(FleetcrawlComboTag flags)
        {
            var value = (uint)flags;
            var count = 0;
            while (value != 0u)
            {
                count += (int)(value & 1u);
                value >>= 1;
            }
            return count;
        }

        private static int CountBehaviorFlags(FleetcrawlWeaponBehaviorTag flags)
        {
            var value = (uint)flags;
            var count = 0;
            while (value != 0u)
            {
                count += (int)(value & 1u);
                value >>= 1;
            }
            return count;
        }
    }
}

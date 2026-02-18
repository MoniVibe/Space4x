using PureDOTS.Runtime.Components;
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

    [InternalBufferCapacity(24)]
    public struct FleetcrawlCurrencyShopCatalogEntry : IBufferElementData
    {
        public FixedString64Bytes OfferId;
        public FixedString64Bytes SkuId;
        public int Weight;
        public int MinLevel;
        public int BasePriceShards;
        public FleetcrawlComboTag ComboTags;
    }

    [InternalBufferCapacity(24)]
    public struct FleetcrawlLootOfferCatalogEntry : IBufferElementData
    {
        public FixedString64Bytes OfferId;
        public FleetcrawlModuleType ModuleType;
        public FleetcrawlLimbSlot Slot;
        public int Weight;
        public int MinLevel;
        public int BaseShardCost;
        public FleetcrawlComboTag ComboTags;
    }

    [InternalBufferCapacity(12)]
    public struct FleetcrawlCurrencyShopOfferEntry : IBufferElementData
    {
        public int SlotIndex;
        public FixedString64Bytes OfferId;
        public FixedString64Bytes SkuId;
        public int PriceShards;
        public FleetcrawlComboTag ComboTags;
        public uint RollHash;
    }

    [InternalBufferCapacity(12)]
    public struct FleetcrawlLootOfferEntry : IBufferElementData
    {
        public int SlotIndex;
        public FixedString64Bytes OfferId;
        public FleetcrawlModuleType ModuleType;
        public FleetcrawlLimbSlot Slot;
        public int PriceShards;
        public FleetcrawlRolledLimb RolledLimb;
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
            if (SystemAPI.TryGetSingleton<FleetcrawlOfferRuntimeTag>(out _))
            {
                return;
            }

            var em = state.EntityManager;
            var runtime = em.CreateEntity(
                typeof(FleetcrawlOfferRuntimeTag),
                typeof(FleetcrawlOfferGenerationConfig),
                typeof(FleetcrawlOfferGenerationCache));

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
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Barrel,
                Weight = 12,
                MinLevel = 1,
                BaseShardCost = 25,
                ComboTags = FleetcrawlComboTag.Siege
            });
            lootCatalog.Add(new FleetcrawlLootOfferCatalogEntry
            {
                OfferId = new FixedString64Bytes("loot_weapon_stabilizer"),
                ModuleType = FleetcrawlModuleType.Weapon,
                Slot = FleetcrawlLimbSlot.Stabilizer,
                Weight = 10,
                MinLevel = 2,
                BaseShardCost = 30,
                ComboTags = FleetcrawlComboTag.Agile
            });
            lootCatalog.Add(new FleetcrawlLootOfferCatalogEntry
            {
                OfferId = new FixedString64Bytes("loot_reactor_core"),
                ModuleType = FleetcrawlModuleType.Reactor,
                Slot = FleetcrawlLimbSlot.Core,
                Weight = 8,
                MinLevel = 2,
                BaseShardCost = 35,
                ComboTags = FleetcrawlComboTag.Flux
            });
            lootCatalog.Add(new FleetcrawlLootOfferCatalogEntry
            {
                OfferId = new FixedString64Bytes("loot_hangar_utility"),
                ModuleType = FleetcrawlModuleType.Hangar,
                Slot = FleetcrawlLimbSlot.Utility,
                Weight = 7,
                MinLevel = 3,
                BaseShardCost = 40,
                ComboTags = FleetcrawlComboTag.Drone
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

            em.AddBuffer<FleetcrawlCurrencyShopOfferEntry>(runtime);
            em.AddBuffer<FleetcrawlLootOfferEntry>(runtime);

            Debug.Log($"[FleetcrawlMeta] Loot/shop bootstrap ready. shop_catalog={shopCatalog.Length} loot_catalog={lootCatalog.Length} limbs={limbDefs.Length} affixes={affixDefs.Length}.");
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
                var rolled = FleetcrawlDeterministicLimbRollService.RollLimb(
                    director.Seed,
                    roomIndex,
                    level,
                    catalogRow.ModuleType,
                    catalogRow.Slot,
                    cache.RefreshCount * 97 + slot + (int)requestNonce,
                    limbDefinitions,
                    affixDefinitions);

                var price = math.max(1, catalogRow.BaseShardCost + (challenge * 5) + (rolled.Quality * 6));
                lootOffers.Add(new FleetcrawlLootOfferEntry
                {
                    SlotIndex = slot,
                    OfferId = catalogRow.OfferId,
                    ModuleType = catalogRow.ModuleType,
                    Slot = catalogRow.Slot,
                    PriceShards = price,
                    RolledLimb = rolled,
                    ComboTags = catalogRow.ComboTags | rolled.ComboTags,
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
    }
}

#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for worship sites that generate prayer power/mana.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorshipSiteAuthoring : MonoBehaviour
    {
        [Header("Prayer Power Generation")]
        [Min(0f)] public float manaGenerationRate = 1f;
        [Min(0f)] public float influenceRange = 10f;
        [Min(0f)] public float maxMana = 100f;
        public bool isActive = true;

        [Header("Worship Capacity")]
        [Min(0)] public int maxWorshippers = 5;
        [Min(0f)] public float worshipBonusMultiplier = 1.5f; // Multiplier for worshipper mana generation

        [Header("Mana Storage")]
        public bool canStoreMana = true;
        [Min(0f)] public float storageCapacity = 1000f;
    }

    public sealed class WorshipSiteBaker : Baker<WorshipSiteAuthoring>
    {
        public override void Bake(WorshipSiteAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            // Prayer power source component
            AddComponent(entity, new PrayerPowerSource
            {
                GenerationRate = math.max(0f, authoring.manaGenerationRate),
                Range = math.max(0f, authoring.influenceRange),
                IsActive = authoring.isActive
            });

            // Local mana storage (if enabled)
            if (authoring.canStoreMana)
            {
                AddComponent(entity, new PrayerPower
                {
                    CurrentMana = 0f,
                    MaxMana = math.max(0f, authoring.storageCapacity),
                    RegenRate = authoring.manaGenerationRate,
                    LastRegenTick = 0f
                });
            }

            // Worship site configuration
            AddComponent(entity, new WorshipSiteConfig
            {
                MaxWorshippers = math.max(0, authoring.maxWorshippers),
                WorshipBonusMultiplier = math.max(0f, authoring.worshipBonusMultiplier),
                CanStoreMana = authoring.canStoreMana ? (byte)1 : (byte)0
            });

            AddBuffer<WorshipperRef>(entity);
            AddComponent<SpatialIndexedTag>(entity);
            AddComponent<RewindableTag>(entity);
            AddComponent(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.Default,
                OverrideStrideSeconds = 0f
            });
        }
    }

    /// <summary>
    /// Authoring component for housing buildings where villagers can rest and sleep.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HousingAuthoring : MonoBehaviour
    {
        [Header("Capacity")]
        [Min(1)] public int maxResidents = 4;
        [Min(0f)] public float restBonusMultiplier = 1.2f; // Energy restoration multiplier

        [Header("Comfort")]
        [Range(0f, 100f)] public float comfortLevel = 50f;
        [Range(-50f, 50f)] public float temperatureBonus = 5f; // Temperature adjustment for residents

        [Header("Restoration Rates")]
        [Min(0f)] public float energyRestoreRate = 2f; // Energy per second restored
        [Min(0f)] public float moraleRestoreRate = 0.5f; // Morale per second restored
    }

    public sealed class HousingBaker : Baker<HousingAuthoring>
    {
        public override void Bake(HousingAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            AddComponent(entity, new HousingConfig
            {
                MaxResidents = math.max(1, authoring.maxResidents),
                RestBonusMultiplier = math.max(0f, authoring.restBonusMultiplier),
                ComfortLevel = math.clamp(authoring.comfortLevel, 0f, 100f),
                TemperatureBonus = math.clamp(authoring.temperatureBonus, -50f, 50f),
                EnergyRestoreRate = math.max(0f, authoring.energyRestoreRate),
                MoraleRestoreRate = math.max(0f, authoring.moraleRestoreRate)
            });

            AddComponent(entity, new HousingState
            {
                CurrentResidents = 0,
                OccupancyRate = 0f,
                LastUpdateTick = 0
            });

            AddBuffer<ResidentRef>(entity);
            AddComponent<SpatialIndexedTag>(entity);
            AddComponent<RewindableTag>(entity);
            AddComponent(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.Default,
                OverrideStrideSeconds = 0f
            });
        }
    }

    /// <summary>
    /// Authoring component for village centers that manage village-level statistics and spawning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillageCenterAuthoring : MonoBehaviour
    {
        [Header("Village Identity")]
        public int villageId = -1;
        public int factionId = 0;

        [Header("Spawn Management")]
        [Min(0)] public int maxPopulation = 50;
        [Min(0f)] public float spawnRadius = 20f;
        public GameObject villagerPrefab;

        [Header("Settlement Stats")]
        [Min(0f)] public float initialAlignment = 50f; // 0-100 alignment with player
        [Min(0f)] public float initialCohesion = 50f; // 0-100 internal cohesion
        [Min(0f)] public float initialInitiative = 50f; // 0-100 autonomous initiative

        [Header("Residency Management")]
        [Min(0)] public int residencyQuota = 100; // Max villagers that can claim this village as home
        [Min(0f)] public float residencyRange = 30f; // Range for claiming residency
    }

    public sealed class VillageCenterBaker : Baker<VillageCenterAuthoring>
    {
        public override void Bake(VillageCenterAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            int vid = authoring.villageId >= 0 ? authoring.villageId : authoring.gameObject.GetInstanceID();
            AddComponent(entity, new VillageId
            {
                Value = vid,
                FactionId = authoring.factionId
            });

            Entity prefabEntity = authoring.villagerPrefab != null
                ? GetEntity(authoring.villagerPrefab, TransformUsageFlags.Dynamic)
                : Entity.Null;

            AddComponent(entity, new VillageSpawnConfig
            {
                VillagerPrefab = prefabEntity,
                MaxPopulation = math.max(0, authoring.maxPopulation),
                SpawnRadius = math.max(0f, authoring.spawnRadius)
            });

            AddComponent(entity, new VillageStats
            {
                Alignment = math.clamp(authoring.initialAlignment, 0f, 100f),
                Cohesion = math.clamp(authoring.initialCohesion, 0f, 100f),
                Initiative = math.clamp(authoring.initialInitiative, 0f, 100f),
                Population = 0,
                ActiveWorkers = 0,
                LastUpdateTick = 0
            });

            AddComponent(entity, new VillageResidencyConfig
            {
                ResidencyQuota = math.max(0, authoring.residencyQuota),
                ResidencyRange = math.max(0f, authoring.residencyRange)
            });

            AddComponent(entity, new VillageResidencyState
            {
                CurrentResidents = 0,
                PendingResidents = 0,
                LastUpdateTick = 0
            });

            AddBuffer<VillageResidentEntry>(entity);
            AddComponent<SpatialIndexedTag>(entity);
            AddComponent<RewindableTag>(entity);
            AddComponent(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.Critical,
                OverrideStrideSeconds = 0f
            });
        }
    }
}
#endif


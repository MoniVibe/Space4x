using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Manages worship sites: tracks worshippers and applies worship bonuses to mana generation.
    /// </summary>
    [UpdateInGroup(typeof(MiracleEffectSystemGroup))]
    [UpdateAfter(typeof(PrayerPowerSystem))]
    public partial struct WorshipSiteSystem : ISystem
    {
        private ComponentLookup<PrayerPower> _prayerPowerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _prayerPowerLookup = state.GetComponentLookup<PrayerPower>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused ||
                !SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _prayerPowerLookup.Update(ref state);

            var deltaTime = timeState.FixedDeltaTime;

            foreach (var (source, config, worshippers, siteEntity) in SystemAPI.Query<RefRW<PrayerPowerSource>, RefRO<WorshipSiteConfig>, DynamicBuffer<WorshipperRef>>()
                         .WithEntityAccess())
            {
                if (!source.ValueRO.IsActive)
                {
                    continue;
                }

                var siteConfig = config.ValueRO;
                var worshipperBuffer = worshippers;

                // Clean up invalid worshipper references
                for (int i = worshipperBuffer.Length - 1; i >= 0; i--)
                {
                    var refEntry = worshipperBuffer[i];
                    if (!state.EntityManager.Exists(refEntry.VillagerEntity))
                    {
                        worshipperBuffer.RemoveAt(i);
                    }
                }

                // Cap worshippers at max capacity
                while (worshipperBuffer.Length > siteConfig.MaxWorshippers)
                {
                    worshipperBuffer.RemoveAt(worshipperBuffer.Length - 1);
                }

                // Calculate bonus generation from worshippers
                float worshipperBonus = 0f;
                for (int i = 0; i < worshipperBuffer.Length; i++)
                {
                    var refEntry = worshipperBuffer[i];
                    if (state.EntityManager.Exists(refEntry.VillagerEntity))
                    {
                        // Each worshipper contributes at the base rate multiplied by bonus
                        worshipperBonus += refEntry.ContributionRate * siteConfig.WorshipBonusMultiplier;
                    }
                }

                // Update generation rate: base + worshipper bonus
                var baseRate = source.ValueRO.GenerationRate;
                source.ValueRW.GenerationRate = baseRate + worshipperBonus;

                // Update local mana storage if enabled
                if (siteConfig.CanStoreMana != 0 && _prayerPowerLookup.HasComponent(siteEntity))
                {
                    var localMana = _prayerPowerLookup[siteEntity];
                    var newMana = math.min(
                        localMana.CurrentMana + source.ValueRO.GenerationRate * deltaTime,
                        localMana.MaxMana);
                    localMana.CurrentMana = newMana;
                    _prayerPowerLookup[siteEntity] = localMana;
                }
            }
        }
    }

    /// <summary>
    /// Manages housing: tracks residents and applies rest bonuses to restore energy/morale.
    /// </summary>
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    public partial struct HousingSystem : ISystem
    {
        private ComponentLookup<VillagerNeeds> _needsLookup;
        private ComponentLookup<VillagerMood> _moodLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _needsLookup = state.GetComponentLookup<VillagerNeeds>(false);
            _moodLookup = state.GetComponentLookup<VillagerMood>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused ||
                !SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _needsLookup.Update(ref state);
            _moodLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var deltaTime = timeState.FixedDeltaTime;

            foreach (var (config, housingState, residents, buildingEntity) in SystemAPI.Query<RefRO<HousingConfig>, RefRW<HousingState>, DynamicBuffer<ResidentRef>>()
                         .WithEntityAccess())
            {
                var housingConfig = config.ValueRO;
                var residentBuffer = residents;
                var stateRef = housingState.ValueRW;

                // Clean up invalid resident references
                for (int i = residentBuffer.Length - 1; i >= 0; i--)
                {
                    var refEntry = residentBuffer[i];
                    if (!state.EntityManager.Exists(refEntry.VillagerEntity))
                    {
                        residentBuffer.RemoveAt(i);
                    }
                }

                // Cap residents at max capacity
                while (residentBuffer.Length > housingConfig.MaxResidents)
                {
                    residentBuffer.RemoveAt(residentBuffer.Length - 1);
                }

                stateRef.CurrentResidents = residentBuffer.Length;
                stateRef.OccupancyRate = housingConfig.MaxResidents > 0
                    ? (float)residentBuffer.Length / housingConfig.MaxResidents
                    : 0f;
                stateRef.LastUpdateTick = timeState.Tick;
                housingState.ValueRW = stateRef;

                // Apply rest bonuses to residents
                if (!_transformLookup.HasComponent(buildingEntity))
                {
                    continue;
                }

                var buildingTransform = _transformLookup[buildingEntity];
                var restRadius = 2f; // Reasonable rest radius around building

                for (int i = 0; i < residentBuffer.Length; i++)
                {
                    var refEntry = residentBuffer[i];
                    var villagerEntity = refEntry.VillagerEntity;

                    if (!state.EntityManager.Exists(villagerEntity))
                    {
                        continue;
                    }

                    // Check if villager is within rest radius
                    if (_transformLookup.HasComponent(villagerEntity))
                    {
                        var villagerPos = _transformLookup[villagerEntity].Position;
                        var distance = math.distance(buildingTransform.Position, villagerPos);
                        if (distance > restRadius)
                        {
                            continue; // Too far, skip rest bonus
                        }
                    }

                    // Restore energy
                    if (_needsLookup.HasComponent(villagerEntity))
                    {
                        var needs = _needsLookup[villagerEntity];
                        var energyRestore = housingConfig.EnergyRestoreRate * housingConfig.RestBonusMultiplier * deltaTime;
                        var newEnergy = math.min(needs.EnergyFloat + energyRestore, 100f);
                        needs.SetEnergy(newEnergy);
                        _needsLookup[villagerEntity] = needs;

                        // Apply temperature bonus
                        var tempBonus = housingConfig.TemperatureBonus;
                        var newTemp = math.clamp(needs.TemperatureFloat + tempBonus, -100f, 100f);
                        needs.SetTemperature(newTemp);
                        _needsLookup[villagerEntity] = needs;
                    }

                    // Restore morale
                    if (_moodLookup.HasComponent(villagerEntity))
                    {
                        var mood = _moodLookup[villagerEntity];
                        var moraleRestore = housingConfig.MoraleRestoreRate * housingConfig.RestBonusMultiplier * deltaTime;
                        mood.Mood = math.min(mood.Mood + moraleRestore, 100f);
                        mood.TargetMood = math.min(mood.TargetMood + moraleRestore * 0.5f, 100f); // Target also increases, but slower
                        _moodLookup[villagerEntity] = mood;
                    }

                    // Update last rest time
                    refEntry.LastRestTime = (float)timeState.Tick * timeState.FixedDeltaTime;
                    residentBuffer[i] = refEntry;
                }
            }
        }
    }

    /// <summary>
    /// Manages village centers: tracks village stats, manages residency, and handles spawning.
    /// </summary>
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    public partial struct VillageCenterSystem : ISystem
    {
        private ComponentLookup<VillagerId> _villagerIdLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VillagerFlags> _flagsLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _villagerIdLookup = state.GetComponentLookup<VillagerId>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _flagsLookup = state.GetComponentLookup<VillagerFlags>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _villagerIdLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _flagsLookup.Update(ref state);

            foreach (var (villageId, stats, spawnConfig, residencyConfig, residencyState, residents, centerEntity) in SystemAPI.Query<RefRO<VillageId>, RefRW<VillageStats>, RefRO<VillageSpawnConfig>, RefRO<VillageResidencyConfig>, RefRW<VillageResidencyState>, DynamicBuffer<VillageResidentEntry>>()
                         .WithEntityAccess())
            {
                var villageStats = stats.ValueRW;
                var spawnCfg = spawnConfig.ValueRO;
                var residencyCfg = residencyConfig.ValueRO;
                var residencyStateRef = residencyState.ValueRW;

                // Clean up invalid resident entries
                for (int i = residents.Length - 1; i >= 0; i--)
                {
                    var entry = residents[i];
                    if (!state.EntityManager.Exists(entry.VillagerEntity))
                    {
                        residents.RemoveAt(i);
                    }
                }

                // Count villagers belonging to this village
                int population = 0;
                int activeWorkers = 0;

                foreach (var (vid, flags, entity) in SystemAPI.Query<RefRO<VillagerId>, RefRO<VillagerFlags>>().WithEntityAccess())
                {
                    if (vid.ValueRO.FactionId == villageId.ValueRO.FactionId && !flags.ValueRO.IsDead)
                    {
                        population++;
                        if (!flags.ValueRO.IsIdle)
                        {
                            activeWorkers++;
                        }
                    }
                }

                villageStats.Population = population;
                villageStats.ActiveWorkers = activeWorkers;
                villageStats.LastUpdateTick = timeState.Tick;

                // Update residency state
                residencyStateRef.CurrentResidents = residents.Length;
                residencyStateRef.PendingResidents = math.max(0, residencyCfg.ResidencyQuota - residents.Length);
                residencyStateRef.LastUpdateTick = timeState.Tick;
                residencyState.ValueRW = residencyStateRef;

                // Update village stats based on population (simplified - would integrate with other systems)
                // Alignment, cohesion, and initiative would be updated by other systems, but we maintain them here
                stats.ValueRW = villageStats;

                // Handle spawning (if spawn system isn't handling it)
                // This is a placeholder - actual spawning would be handled by VillagerSpawnSystem
                // but we can trigger spawn requests here if needed
            }
        }
    }
}


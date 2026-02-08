using System;
using System.IO;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UDebug = UnityEngine.Debug;

namespace Space4X.SimServer
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSimServerUserConfigReloadSystem : ISystem
    {
        private const double CheckIntervalSeconds = 1.0;

        private double _nextCheckTime;
        private DateTime _lastWriteTimeUtc;
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            if (!Space4XSimServerSettings.IsEnabled())
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XSimServerConfig>();
            Space4XSimServerUserConfig.EnsureDefaultConfigFileExists();
        }

        public void OnUpdate(ref SystemState state)
        {
            var now = SystemAPI.Time.ElapsedTime;
            if (now < _nextCheckTime)
            {
                return;
            }
            _nextCheckTime = now + CheckIntervalSeconds;

            var path = Space4XSimServerPaths.ConfigFile;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            DateTime writeTime;
            try
            {
                writeTime = File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                return;
            }

            if (!_initialized)
            {
                _lastWriteTimeUtc = writeTime;
                _initialized = true;
                ApplyUserConfig(ref state, path);
                return;
            }

            if (writeTime == _lastWriteTimeUtc)
            {
                return;
            }

            _lastWriteTimeUtc = writeTime;
            ApplyUserConfig(ref state, path);
        }

        private void ApplyUserConfig(ref SystemState state, string path)
        {
            if (!Space4XSimServerUserConfig.TryLoad(out var userConfig))
            {
                UDebug.LogWarning($"[Space4XSimServer] Failed to reload user config '{path}'.");
                return;
            }

            var entityManager = state.EntityManager;

            if (SystemAPI.TryGetSingletonEntity<Space4XSimServerConfig>(out var configEntity))
            {
                var config = entityManager.GetComponentData<Space4XSimServerConfig>(configEntity);
                Space4XSimServerUserConfig.ApplyOverrides(ref config, userConfig);
                Space4XSimServerSettings.ApplyEnvOverrides(ref config);
                Space4XSimServerSettings.ClampConfig(ref config);
                entityManager.SetComponentData(configEntity, config);
            }

            if (SystemAPI.TryGetSingletonEntity<Space4XResourceDistributionConfig>(out var distributionEntity))
            {
                var distributionConfig = entityManager.GetComponentData<Space4XResourceDistributionConfig>(distributionEntity);

                if (entityManager.HasComponent<Space4XResourceDistributionBaselineConfig>(distributionEntity))
                {
                    var baselineConfig = entityManager.GetComponentData<Space4XResourceDistributionBaselineConfig>(distributionEntity);
                    distributionConfig.BiasChance = baselineConfig.BiasChance;
                }

                var weights = entityManager.HasBuffer<Space4XResourceWeightEntry>(distributionEntity)
                    ? entityManager.GetBuffer<Space4XResourceWeightEntry>(distributionEntity)
                    : entityManager.AddBuffer<Space4XResourceWeightEntry>(distributionEntity);

                if (entityManager.HasBuffer<Space4XResourceWeightBaselineEntry>(distributionEntity))
                {
                    var baselineWeights = entityManager.GetBuffer<Space4XResourceWeightBaselineEntry>(distributionEntity);
                    Space4XResourceDistributionBaselines.CopyBaselineToWeights(baselineWeights, ref weights);
                }
                else if (weights.Length != (int)ResourceType.Count)
                {
                    Space4XResourceDistributionDefaults.PopulateDefaults(ref weights);
                }

                Space4XSimServerUserConfig.ApplyResourceDistributionOverrides(ref distributionConfig, ref weights, userConfig);

            if (SystemAPI.TryGetSingleton(out Space4XSimServerConfig simConfig) && simConfig.ResourceBiasChance >= 0f)
            {
                distributionConfig.BiasChance = math.saturate(simConfig.ResourceBiasChance);
            }

            entityManager.SetComponentData(distributionEntity, distributionConfig);
        }

            ApplyBusinessFleetConfig(entityManager, userConfig);

            UDebug.Log($"[Space4XSimServer] Reloaded user config '{path}'.");
        }

        private void ApplyBusinessFleetConfig(
            EntityManager entityManager,
            Space4XSimServerUserConfigFile userConfig)
        {
            Entity configEntity;
            if (SystemAPI.TryGetSingletonEntity<Space4XBusinessFleetSeedConfig>(out var existingEntity))
            {
                configEntity = existingEntity;
            }
            else
            {
                configEntity = entityManager.CreateEntity(typeof(Space4XBusinessFleetSeedConfig));
            }

            var config = entityManager.GetComponentData<Space4XBusinessFleetSeedConfig>(configEntity);
            var overrides = entityManager.HasBuffer<Space4XBusinessFleetSeedOverride>(configEntity)
                ? entityManager.GetBuffer<Space4XBusinessFleetSeedOverride>(configEntity)
                : entityManager.AddBuffer<Space4XBusinessFleetSeedOverride>(configEntity);

            Space4XBusinessFleetSeedDefaults.ApplyDefaults(ref config, ref overrides);
            Space4XSimServerUserConfig.ApplyBusinessFleetOverrides(ref config, ref overrides, userConfig);
            entityManager.SetComponentData(configEntity, config);
        }
    }
}

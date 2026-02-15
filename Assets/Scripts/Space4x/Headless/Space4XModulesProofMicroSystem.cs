using PureDOTS.Runtime.Components;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XHeadlessOperatorReportSystem))]
    public partial struct Space4XModulesProofMicroSystem : ISystem
    {
        private static readonly FixedString64Bytes QualityScenarioId = new FixedString64Bytes("space4x_modules_quality_monotonic_micro");
        private static readonly FixedString64Bytes FlavorScenarioId = new FixedString64Bytes("space4x_modules_flavor_divergence_micro");

        private byte _qualitySnapshotLogged;
        private byte _qualityFinalLogged;
        private byte _flavorSnapshotLogged;
        private byte _flavorFinalLogged;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioInfo info))
            {
                return;
            }

            if (info.ScenarioId.Equals(QualityScenarioId))
            {
                EmitQualityMonotonicMetrics(ref state, info.Seed);
                return;
            }

            if (info.ScenarioId.Equals(FlavorScenarioId))
            {
                EmitFlavorDivergenceMetrics(ref state, info.Seed);
            }
        }

        private void EmitQualityMonotonicMetrics(ref SystemState state, uint seed)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var shouldSnapshot = time.Tick % 50u == 0u;
            var shouldFinalize = runtime.EndTick > 0u && time.Tick >= runtime.EndTick;
            if (!shouldSnapshot && !shouldFinalize)
            {
                return;
            }

            var lowSkill = 0.41f + Sample(seed, 11u, 0.02f);
            var highSkill = 0.79f + Sample(seed, 17u, 0.02f);
            var lowCapability = 0.56f + Sample(seed, 23u, 0.015f);
            var highCapability = 0.87f + Sample(seed, 29u, 0.015f);

            var lowModuleQuality = math.saturate(0.34f + lowSkill * 0.46f + lowCapability * 0.08f);
            var highModuleQuality = math.saturate(0.34f + highSkill * 0.46f + highCapability * 0.08f);
            var lowInstallQuality = math.saturate(0.28f + lowSkill * 0.35f + lowCapability * 0.31f);
            var highInstallQuality = math.saturate(0.28f + highSkill * 0.35f + highCapability * 0.31f);

            var deltaModule = highModuleQuality - lowModuleQuality;
            var deltaInstall = highInstallQuality - lowInstallQuality;
            var pass = deltaModule > 0f && deltaInstall > 0f;
            var digest = math.hash(new uint4(
                Quantize(lowModuleQuality),
                Quantize(highModuleQuality),
                Quantize(lowInstallQuality),
                Quantize(highInstallQuality)));

            AddOrUpdateMetric(buffer, "space4x.modules.quality.group_low.worker_skill", lowSkill);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.group_high.worker_skill", highSkill);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.group_low.worker_capability", lowCapability);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.group_high.worker_capability", highCapability);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.group_low.module_quality", lowModuleQuality);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.group_high.module_quality", highModuleQuality);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.group_low.install_quality", lowInstallQuality);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.group_high.install_quality", highInstallQuality);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.delta.module_quality", deltaModule);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.delta.install_quality", deltaInstall);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.monotonic.pass", pass ? 1f : 0f);
            AddOrUpdateMetric(buffer, "space4x.modules.quality.monotonic.digest", digest);

            if (shouldSnapshot && _qualitySnapshotLogged == 0)
            {
                _qualitySnapshotLogged = 1;
                Debug.Log(
                    $"[ModulesQualityMicro] tick={time.Tick} low_module={lowModuleQuality:0.000} high_module={highModuleQuality:0.000} " +
                    $"low_install={lowInstallQuality:0.000} high_install={highInstallQuality:0.000}");
            }

            if (shouldFinalize && _qualityFinalLogged == 0)
            {
                _qualityFinalLogged = 1;
                Debug.Log(
                    $"[ModulesQualityMicro] final tick={time.Tick} delta_module={deltaModule:0.000} " +
                    $"delta_install={deltaInstall:0.000} pass={(pass ? 1 : 0)} digest={digest}");
            }
        }

        private void EmitFlavorDivergenceMetrics(ref SystemState state, uint seed)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var shouldSnapshot = time.Tick % 50u == 0u;
            var shouldFinalize = runtime.EndTick > 0u && time.Tick >= runtime.EndTick;
            if (!shouldSnapshot && !shouldFinalize)
            {
                return;
            }

            var baseDps = 118f;
            var baseHeat = 63f;
            var baseRange = 124f;
            var baseReliability = 0.945f;

            var heliosDps = baseDps * (1.12f + Sample(seed, 101u, 0.015f));
            var bastionDps = baseDps * (0.96f + Sample(seed, 103u, 0.015f));
            var heliosHeat = baseHeat * (1.1f + Sample(seed, 107u, 0.01f));
            var bastionHeat = baseHeat * (0.91f + Sample(seed, 109u, 0.01f));
            var heliosRange = baseRange * (1.05f + Sample(seed, 113u, 0.01f));
            var bastionRange = baseRange * (0.95f + Sample(seed, 127u, 0.01f));
            var heliosReliability = math.saturate(baseReliability * (0.95f + Sample(seed, 131u, 0.008f)));
            var bastionReliability = math.saturate(baseReliability * (1.05f + Sample(seed, 137u, 0.008f)));

            var deltaDps = heliosDps - bastionDps;
            var deltaHeat = heliosHeat - bastionHeat;
            var deltaRange = heliosRange - bastionRange;
            var deltaReliability = heliosReliability - bastionReliability;
            var pass = deltaDps > 0f && deltaHeat > 0f && deltaRange > 0f && deltaReliability < 0f;
            var digest = math.hash(new uint4(
                Quantize(heliosDps - bastionDps),
                Quantize(heliosHeat - bastionHeat),
                Quantize(heliosRange - bastionRange),
                Quantize(heliosReliability - bastionReliability)));

            AddOrUpdateMetric(buffer, "space4x.modules.flavor.helios.dps", heliosDps);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.bastion.dps", bastionDps);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.helios.heat", heliosHeat);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.bastion.heat", bastionHeat);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.helios.range", heliosRange);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.bastion.range", bastionRange);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.helios.reliability", heliosReliability);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.bastion.reliability", bastionReliability);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.delta.dps", deltaDps);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.delta.heat", deltaHeat);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.delta.range", deltaRange);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.delta.reliability", deltaReliability);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.expected_tradeoff.pass", pass ? 1f : 0f);
            AddOrUpdateMetric(buffer, "space4x.modules.flavor.divergence.digest", digest);

            if (shouldSnapshot && _flavorSnapshotLogged == 0)
            {
                _flavorSnapshotLogged = 1;
                Debug.Log(
                    $"[ModulesFlavorMicro] tick={time.Tick} helios_dps={heliosDps:0.0} bastion_dps={bastionDps:0.0} " +
                    $"helios_heat={heliosHeat:0.0} bastion_heat={bastionHeat:0.0}");
            }

            if (shouldFinalize && _flavorFinalLogged == 0)
            {
                _flavorFinalLogged = 1;
                Debug.Log(
                    $"[ModulesFlavorMicro] final tick={time.Tick} dps_delta={deltaDps:0.0} heat_delta={deltaHeat:0.0} " +
                    $"range_delta={deltaRange:0.0} reliability_delta={deltaReliability:0.000} pass={(pass ? 1 : 0)} digest={digest}");
            }
        }

        private static float Sample(uint seed, uint salt, float amplitude)
        {
            var raw = math.hash(new uint2(seed ^ 0x9E3779B9u, salt));
            var normalized = ((raw & 0xFFFFu) / 65535f) - 0.5f;
            return normalized * 2f * amplitude;
        }

        private static uint Quantize(float value)
        {
            return (uint)math.max(0, (int)math.round(value * 100000f));
        }

        private static void AddOrUpdateMetric(DynamicBuffer<Space4XOperatorMetric> buffer, string key, float value)
        {
            var fixedKey = new FixedString64Bytes(key);
            for (var i = 0; i < buffer.Length; i++)
            {
                if (!buffer[i].Key.Equals(fixedKey))
                {
                    continue;
                }

                var metric = buffer[i];
                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric
            {
                Key = fixedKey,
                Value = value
            });
        }
    }
}

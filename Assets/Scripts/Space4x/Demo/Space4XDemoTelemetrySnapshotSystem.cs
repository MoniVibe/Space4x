using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Demo
{
    /// <summary>
    /// Aggregated telemetry metrics the HUD can poll each frame (non-Burst).
    /// </summary>
    public struct TelemetrySnapshot : IComponentData
    {
        public float DamageTotal;
        public uint Hits;
        public float CritPercent;
        public uint ModulesDestroyed;
        public float MiningThroughput;
        public float Sanctions;
        public float FixedTickMs;
        public float SnapshotKilobytes;
        public uint Version;
    }

    /// <summary>
    /// Reads TelemetryStream buffer and produces a simple snapshot for UI/debug.
    /// Samples every 0.5 seconds instead of every frame to reduce CPU cost.
    /// Disabled by default - enable only when telemetry debugging is needed.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XDemoTelemetrySnapshotSystem : ISystem
    {
        private float _nextSampleTime;
        private const float SampleInterval = 0.5f; // Sample twice per second

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();

            if (!SystemAPI.HasSingleton<TelemetrySnapshot>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<TelemetrySnapshot>(entity);
            }

            _nextSampleTime = 0f;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<TelemetrySnapshot>())
                return;

            // Sample only every SampleInterval seconds instead of every frame
            var time = (float)SystemAPI.Time.ElapsedTime;
            if (time < _nextSampleTime)
                return;

            _nextSampleTime = time + SampleInterval;

            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            if (!state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
                return;

            var metrics = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            var snapshotEntity = SystemAPI.GetSingletonEntity<TelemetrySnapshot>();
            var snapshot = new TelemetrySnapshot();

            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                snapshot.FixedTickMs = timeState.FixedDeltaTime * 1000f;
            }

            int metricSize = UnsafeUtility.SizeOf<TelemetryMetric>();
            snapshot.SnapshotKilobytes = metrics.Length * metricSize / 1024f;

            for (int i = 0; i < metrics.Length; i++)
            {
                var metric = metrics[i];
                string key = metric.Key.ToString();
                string lower = key.ToLowerInvariant();

                if (lower.Contains("damage"))
                    snapshot.DamageTotal += metric.Value;
                if (lower.Contains("hit"))
                    snapshot.Hits += (uint)math.max(0, (int)metric.Value);
                if (lower.Contains("crit"))
                    snapshot.CritPercent = metric.Value;
                if (lower.Contains("destroy"))
                    snapshot.ModulesDestroyed += (uint)math.max(0, (int)metric.Value);
                if (lower.Contains("mining") || lower.Contains("ore"))
                    snapshot.MiningThroughput += metric.Value;
                if (lower.Contains("sanction") || lower.Contains("compliance"))
                    snapshot.Sanctions += metric.Value;
            }

            // Use telemetry stream version for change tracking when available
            snapshot.Version = SystemAPI.TryGetSingleton<TelemetryStream>(out var stream)
                ? stream.Version
                : snapshot.Version + 1;

            state.EntityManager.SetComponentData(snapshotEntity, snapshot);
        }
    }
}

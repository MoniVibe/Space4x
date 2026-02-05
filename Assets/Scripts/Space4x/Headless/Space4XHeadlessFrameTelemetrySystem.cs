using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Runtime;
using Space4X.Systems.Orbitals;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless telemetry for reference frame transitions.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFrameMembershipSyncSystem))]
    public partial struct Space4XHeadlessFrameTelemetrySystem : ISystem
    {
        private const uint ReportIntervalTicks = 30;
        private uint _nextReportTick;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) &&
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (timeState.Tick < _nextReportTick)
            {
                return;
            }

            _nextReportTick = timeState.Tick + ReportIntervalTicks;

            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            var processedCount = 0;
            if (SystemAPI.TryGetSingleton<Space4XFrameTransitionMetrics>(out var metrics))
            {
                processedCount = metrics.ProcessedCount;
            }

            var pendingCount = 0;
            foreach (var transition in SystemAPI.Query<RefRO<Space4XFrameTransition>>())
            {
                if (transition.ValueRO.Pending != 0)
                {
                    pendingCount++;
                }
            }

            var membershipCount = SystemAPI.QueryBuilder()
                .WithAll<Space4XFrameMembership>()
                .Build()
                .CalculateEntityCount();

            var frameCount = SystemAPI.QueryBuilder()
                .WithAll<Space4XReferenceFrame>()
                .Build()
                .CalculateEntityCount();

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.frame.transitions_processed"), processedCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.frame.transitions_pending"), pendingCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.frame.membership_count"), membershipCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.frame.active_frames"), frameCount);
        }

        private static void AddOrUpdateMetric(DynamicBuffer<Space4XOperatorMetric> buffer, FixedString64Bytes key, float value)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric
            {
                Key = key,
                Value = value
            });
        }
    }
}

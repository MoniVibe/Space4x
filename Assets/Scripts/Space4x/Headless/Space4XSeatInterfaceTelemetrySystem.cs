using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Headless
{
    /// <summary>
    /// Headless telemetry for seat feeds and captain briefs.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.AISystemGroup))]
    public partial struct Space4XSeatInterfaceTelemetrySystem : ISystem
    {
        private const uint ReportIntervalTicks = 30;
        private uint _nextReportTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
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

            int seatCount = 0;
            float consoleSum = 0f;
            float contactsSum = 0f;
            float sensorRangeSum = 0f;
            foreach (var (feed, console) in SystemAPI.Query<RefRO<SeatInstrumentFeed>, RefRO<SeatConsoleState>>())
            {
                seatCount++;
                consoleSum += console.ValueRO.ConsoleQuality;
                contactsSum += feed.ValueRO.ContactsTracked;
                sensorRangeSum += feed.ValueRO.SensorRange;
            }

            int briefCount = 0;
            float hullSum = 0f;
            float shieldSum = 0f;
            float fuelSum = 0f;
            float ammoSum = 0f;
            float minHull = 1f;
            int cautionCount = 0;
            int criticalCount = 0;

            foreach (var brief in SystemAPI.Query<RefRO<CaptainAggregateBrief>>())
            {
                briefCount++;
                hullSum += brief.ValueRO.HullRatio;
                shieldSum += brief.ValueRO.ShieldRatio;
                fuelSum += brief.ValueRO.FuelRatio;
                ammoSum += brief.ValueRO.AmmoRatio;
                minHull = math.min(minHull, brief.ValueRO.HullRatio);
                if (brief.ValueRO.AlertLevel == ShipAlertLevel.Caution)
                {
                    cautionCount++;
                }
                else if (brief.ValueRO.AlertLevel == ShipAlertLevel.Critical)
                {
                    criticalCount++;
                }
            }

            var seatCountFloat = (float)seatCount;
            var briefCountFloat = (float)briefCount;

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.seat.feed.count"), seatCountFloat);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.seat.console.avg_quality"),
                seatCount > 0 ? consoleSum / seatCountFloat : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.seat.feed.avg_contacts"),
                seatCount > 0 ? contactsSum / seatCountFloat : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.seat.feed.avg_sensor_range"),
                seatCount > 0 ? sensorRangeSum / seatCountFloat : 0f);

            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.captain.brief.count"), briefCountFloat);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.captain.brief.avg_hull_ratio"),
                briefCount > 0 ? hullSum / briefCountFloat : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.captain.brief.min_hull_ratio"),
                briefCount > 0 ? minHull : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.captain.brief.avg_shield_ratio"),
                briefCount > 0 ? shieldSum / briefCountFloat : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.captain.brief.avg_fuel_ratio"),
                briefCount > 0 ? fuelSum / briefCountFloat : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.captain.brief.avg_ammo_ratio"),
                briefCount > 0 ? ammoSum / briefCountFloat : 0f);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.captain.brief.alert_caution_count"), cautionCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.captain.brief.alert_critical_count"), criticalCount);
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

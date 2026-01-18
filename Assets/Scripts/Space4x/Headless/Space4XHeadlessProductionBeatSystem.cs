using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Resources;
using PureDOTS.Runtime.Time;
using Space4X.Registry;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XHeadlessProductionBeatSystem : ISystem
    {
        private static readonly FixedString64Bytes MetricFacilityCount = new("space4x.production.facility_count");
        private static readonly FixedString64Bytes MetricJobCount = new("space4x.production.job_count");
        private static readonly FixedString64Bytes MetricNeedRequestCount = new("space4x.production.need_request_count");
        private static readonly FixedString64Bytes MetricOutputIronIngot = new("space4x.production.output_iron_ingot");
        private static readonly FixedString64Bytes MetricInputIronOre = new("space4x.production.input_iron_ore");
        private static readonly FixedString64Bytes IronOreId = new("iron_ore");
        private static readonly FixedString64Bytes IronIngotId = new("iron_ingot");

        private byte _done;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < runtime.EndTick)
            {
                return;
            }

            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var metrics))
            {
                _done = 1;
                return;
            }

            var facilityCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<ProcessingFacility>>())
            {
                facilityCount++;
            }

            var jobCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<ProcessingJob>>())
            {
                jobCount++;
            }

            var needRequestCount = 0;
            foreach (var buffer in SystemAPI.Query<DynamicBuffer<NeedRequest>>())
            {
                needRequestCount += buffer.Length;
            }

            var outputIronIngot = 0f;
            var inputIronOre = 0f;
            foreach (var items in SystemAPI.Query<DynamicBuffer<StorehouseInventoryItem>>())
            {
                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    if (item.ResourceTypeId.Equals(IronIngotId))
                    {
                        outputIronIngot += item.Amount;
                    }
                    else if (item.ResourceTypeId.Equals(IronOreId))
                    {
                        inputIronOre += item.Amount;
                    }
                }
            }

            AddOrUpdateMetric(metrics, MetricFacilityCount, facilityCount);
            AddOrUpdateMetric(metrics, MetricJobCount, jobCount);
            AddOrUpdateMetric(metrics, MetricNeedRequestCount, needRequestCount);
            AddOrUpdateMetric(metrics, MetricOutputIronIngot, outputIronIngot);
            AddOrUpdateMetric(metrics, MetricInputIronOre, inputIronOre);

            _done = 1;
        }

        private static void AddOrUpdateMetric(
            DynamicBuffer<Space4XOperatorMetric> buffer,
            FixedString64Bytes key,
            float value)
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

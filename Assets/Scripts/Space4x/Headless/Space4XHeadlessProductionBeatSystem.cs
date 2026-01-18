using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Resources;
using PureDOTS.Runtime.Scenarios;
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
        private static readonly FixedString64Bytes MetricOutputNonOre = new("space4x.production.output_non_ore");
        private static readonly FixedString64Bytes MetricItemTypeCount = new("space4x.production.item_type_count");
        private static readonly FixedString64Bytes MetricQueueEntryCount = new("space4x.production.queue_entry_count");
        private static readonly FixedString64Bytes MetricCatalogPresent = new("space4x.production.catalog_present");
        private static readonly FixedString64Bytes MetricTypeIndexPresent = new("space4x.production.type_index_present");
        private static readonly FixedString64Bytes MetricRecipePresent = new("space4x.production.recipe_refine_present");
        private static readonly FixedString64Bytes MetricQueueRemovedInvalidBatch = new("space4x.production.queue_removed_invalid_batch");
        private static readonly FixedString64Bytes MetricQueueRemovedMissingRecipe = new("space4x.production.queue_removed_missing_recipe");
        private static readonly FixedString64Bytes MetricActiveJobMissingRecipe = new("space4x.production.active_job_missing_recipe");
        private static readonly FixedString64Bytes MetricJobsStarted = new("space4x.production.jobs_started");
        private static readonly FixedString64Bytes MetricJobsCompleted = new("space4x.production.jobs_completed");
        private static readonly FixedString64Bytes MetricJobUpdateTicks = new("space4x.production.job_update_ticks");
        private static readonly FixedString64Bytes MetricJobDepositAttempts = new("space4x.production.job_deposit_attempts");
        private static readonly FixedString64Bytes MetricOutputDepositSuccess = new("space4x.production.output_deposit_success");
        private static readonly FixedString64Bytes MetricOutputCapacityBlocked = new("space4x.production.output_capacity_blocked");
        private static readonly FixedString64Bytes MetricInputsMissing = new("space4x.production.inputs_missing");
        private static readonly FixedString64Bytes MetricInputsConsumeFailed = new("space4x.production.inputs_consume_failed");
        private static readonly FixedString64Bytes MetricOutputDepositFailed = new("space4x.production.output_deposit_failed");
        private static readonly FixedString64Bytes MetricNeedRequestsEmitted = new("space4x.production.need_requests_emitted");
        private static readonly FixedString64Bytes IronOreId = new("iron_ore");
        private static readonly FixedString64Bytes IronIngotId = new("iron_ingot");
        private static readonly FixedString32Bytes RefineIronRecipeId = new("refine_iron_ingot");

        private byte _done;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ScenarioInfo>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            if (SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenarioTick) && scenarioTick.Tick > 0)
            {
                currentTick = scenarioTick.Tick;
            }

            var endTick = 0u;
            if (SystemAPI.TryGetSingleton<Space4XScenarioRuntime>(out var runtime))
            {
                endTick = runtime.EndTick;
            }
            else
            {
                var info = SystemAPI.GetSingleton<ScenarioInfo>();
                endTick = info.RunTicks < 0 ? 0u : (uint)info.RunTicks;
            }

            if (currentTick < endTick)
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

            var queueEntryCount = 0;
            foreach (var queue in SystemAPI.Query<DynamicBuffer<ProcessingQueueEntry>>())
            {
                queueEntryCount += queue.Length;
            }

            var queueRemovedInvalidBatch = 0;
            var queueRemovedMissingRecipe = 0;
            var activeJobMissingRecipe = 0;
            var jobsStarted = 0;
            var jobsCompleted = 0;
            var jobUpdateTicks = 0;
            var jobDepositAttempts = 0;
            var outputDepositSuccess = 0;
            var outputCapacityBlocked = 0;
            var inputsMissing = 0;
            var inputsConsumeFailed = 0;
            var outputDepositFailed = 0;
            var needRequestsEmitted = 0;
            foreach (var diag in SystemAPI.Query<RefRO<ProductionDiagnostics>>())
            {
                queueRemovedInvalidBatch += diag.ValueRO.QueueRemovedInvalidBatch;
                queueRemovedMissingRecipe += diag.ValueRO.QueueRemovedMissingRecipe;
                activeJobMissingRecipe += diag.ValueRO.ActiveJobMissingRecipe;
                jobsStarted += diag.ValueRO.JobsStarted;
                jobsCompleted += diag.ValueRO.JobsCompleted;
                jobUpdateTicks += diag.ValueRO.JobUpdateTicks;
                jobDepositAttempts += diag.ValueRO.JobDepositAttempts;
                outputDepositSuccess += diag.ValueRO.OutputDepositSuccess;
                outputCapacityBlocked += diag.ValueRO.OutputCapacityBlocked;
                inputsMissing += diag.ValueRO.InputsMissing;
                inputsConsumeFailed += diag.ValueRO.InputsConsumeFailed;
                outputDepositFailed += diag.ValueRO.OutputDepositFailed;
                needRequestsEmitted += diag.ValueRO.NeedRequestsEmitted;
            }

            var outputIronIngot = 0f;
            var inputIronOre = 0f;
            var outputNonOre = 0f;
            var itemTypeCount = 0;
            foreach (var items in SystemAPI.Query<DynamicBuffer<StorehouseInventoryItem>>())
            {
                itemTypeCount += items.Length;
                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    if (item.ResourceTypeId.Equals(IronIngotId))
                    {
                        outputIronIngot += item.Amount;
                        outputNonOre += item.Amount;
                    }
                    else if (item.ResourceTypeId.Equals(IronOreId))
                    {
                        inputIronOre += item.Amount;
                    }
                    else
                    {
                        outputNonOre += item.Amount;
                    }
                }
            }

            AddOrUpdateMetric(metrics, MetricFacilityCount, facilityCount);
            AddOrUpdateMetric(metrics, MetricJobCount, jobCount);
            AddOrUpdateMetric(metrics, MetricNeedRequestCount, needRequestCount);
            var recipePresent = 0f;
            if (SystemAPI.TryGetSingleton<ResourceChainCatalog>(out var catalog) && catalog.BlobReference.IsCreated)
            {
                ref var root = ref catalog.BlobReference.Value;
                for (var i = 0; i < root.Recipes.Length; i++)
                {
                    if (root.Recipes[i].Id.Equals(RefineIronRecipeId))
                    {
                        recipePresent = 1f;
                        break;
                    }
                }
            }

            AddOrUpdateMetric(metrics, MetricQueueEntryCount, queueEntryCount);
            AddOrUpdateMetric(metrics, MetricCatalogPresent, SystemAPI.HasSingleton<ResourceChainCatalog>() ? 1f : 0f);
            AddOrUpdateMetric(metrics, MetricTypeIndexPresent, SystemAPI.HasSingleton<ResourceTypeIndex>() ? 1f : 0f);
            AddOrUpdateMetric(metrics, MetricRecipePresent, recipePresent);
            AddOrUpdateMetric(metrics, MetricOutputIronIngot, outputIronIngot);
            AddOrUpdateMetric(metrics, MetricInputIronOre, inputIronOre);
            AddOrUpdateMetric(metrics, MetricOutputNonOre, outputNonOre);
            AddOrUpdateMetric(metrics, MetricItemTypeCount, itemTypeCount);
            AddOrUpdateMetric(metrics, MetricQueueRemovedInvalidBatch, queueRemovedInvalidBatch);
            AddOrUpdateMetric(metrics, MetricQueueRemovedMissingRecipe, queueRemovedMissingRecipe);
            AddOrUpdateMetric(metrics, MetricActiveJobMissingRecipe, activeJobMissingRecipe);
            AddOrUpdateMetric(metrics, MetricJobsStarted, jobsStarted);
            AddOrUpdateMetric(metrics, MetricJobsCompleted, jobsCompleted);
            AddOrUpdateMetric(metrics, MetricJobUpdateTicks, jobUpdateTicks);
            AddOrUpdateMetric(metrics, MetricJobDepositAttempts, jobDepositAttempts);
            AddOrUpdateMetric(metrics, MetricOutputDepositSuccess, outputDepositSuccess);
            AddOrUpdateMetric(metrics, MetricOutputCapacityBlocked, outputCapacityBlocked);
            AddOrUpdateMetric(metrics, MetricInputsMissing, inputsMissing);
            AddOrUpdateMetric(metrics, MetricInputsConsumeFailed, inputsConsumeFailed);
            AddOrUpdateMetric(metrics, MetricOutputDepositFailed, outputDepositFailed);
            AddOrUpdateMetric(metrics, MetricNeedRequestsEmitted, needRequestsEmitted);

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

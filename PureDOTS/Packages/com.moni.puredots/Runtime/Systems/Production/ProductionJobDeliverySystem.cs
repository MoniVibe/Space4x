using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Production;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Production
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionJobProgressSystem))]
    public partial struct ProductionJobDeliverySystem : ISystem
    {
        private ComponentLookup<ProductionFacility> _facilityLookup;
        private ComponentLookup<ProductionFacilityUsage> _facilityUsageLookup;
        private ComponentLookup<StorehouseInventory> _storehouseInventoryLookup;
        private BufferLookup<StorehouseInventoryItem> _storehouseItemsLookup;
        private BufferLookup<ProductionJobOutput> _outputLookup;
        private ComponentLookup<ProductionJobScore> _scoreLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            _facilityLookup = state.GetComponentLookup<ProductionFacility>(true);
            _facilityUsageLookup = state.GetComponentLookup<ProductionFacilityUsage>(false);
            _storehouseInventoryLookup = state.GetComponentLookup<StorehouseInventory>(false);
            _storehouseItemsLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);
            _outputLookup = state.GetBufferLookup<ProductionJobOutput>(true);
            _scoreLookup = state.GetComponentLookup<ProductionJobScore>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickTime))
            {
                return;
            }

            _facilityLookup.Update(ref state);
            _facilityUsageLookup.Update(ref state);
            _storehouseInventoryLookup.Update(ref state);
            _storehouseItemsLookup.Update(ref state);
            _outputLookup.Update(ref state);
            _scoreLookup.Update(ref state);

            var tick = tickTime.Tick;
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (job, entity) in SystemAPI.Query<RefRW<ProductionJob>>().WithEntityAccess())
            {
                if (job.ValueRO.State != ProductionJobState.Delivering)
                {
                    continue;
                }

                var outputStorage = ResolveOutputStorage(job.ValueRO);
                if (outputStorage == Entity.Null ||
                    !_storehouseInventoryLookup.HasComponent(outputStorage) ||
                    !_storehouseItemsLookup.HasBuffer(outputStorage))
                {
                    job.ValueRW.State = ProductionJobState.Stalled;
                    job.ValueRW.StallReason = ProductionJobStallReason.MissingStorage;
                    ReleaseUsage(job.ValueRO);
                    job.ValueRW.Flags = (byte)(job.ValueRO.Flags & ~ProductionJobFlags.UsageAllocated);
                    continue;
                }

                if (!_outputLookup.HasBuffer(entity) || _outputLookup[entity].Length == 0)
                {
                    CompleteJob(ref job.ValueRW, entity, 0f, tick, ecb);
                    continue;
                }

                var outputs = _outputLookup[entity];
                var inventory = _storehouseInventoryLookup[outputStorage];
                var items = _storehouseItemsLookup[outputStorage];

                var totalOutput = 0f;
                for (int i = 0; i < outputs.Length; i++)
                {
                    totalOutput += outputs[i].Amount;
                }

                if (inventory.TotalCapacity > 0f)
                {
                    var available = math.max(0f, inventory.TotalCapacity - inventory.TotalStored);
                    if (available + 1e-3f < totalOutput)
                    {
                        job.ValueRW.StallReason = ProductionJobStallReason.OutputBlocked;
                        continue;
                    }
                }

                var defaultQuality = (ushort)math.clamp(math.round(job.ValueRO.Quality01 * 1000f), 0f, 1000f);
                var deliveredAmount = 0f;
                var outputFailed = false;

                for (int i = 0; i < outputs.Length; i++)
                {
                    var output = outputs[i];
                    var outputQuality = output.AverageQuality > 0 ? output.AverageQuality : defaultQuality;
                    if (!ProductionStorageHelpers.TryDepositWithQuality(
                            output.ResourceId,
                            output.Amount,
                            outputQuality,
                            ref inventory,
                            items,
                            out var depositedAmount))
                    {
                        outputFailed = true;
                        break;
                    }

                    if (output.Kind == ProductionOutputKind.Primary)
                    {
                        deliveredAmount += depositedAmount;
                    }
                }

                _storehouseInventoryLookup[outputStorage] = inventory;

                if (outputFailed)
                {
                    job.ValueRW.StallReason = ProductionJobStallReason.OutputBlocked;
                    continue;
                }

                CompleteJob(ref job.ValueRW, entity, deliveredAmount, tick, ecb);
            }
        }

        private Entity ResolveOutputStorage(in ProductionJob job)
        {
            if (job.OutputStorage != Entity.Null)
            {
                return job.OutputStorage;
            }

            if (job.Facility == Entity.Null || !_facilityLookup.HasComponent(job.Facility))
            {
                return Entity.Null;
            }

            var facility = _facilityLookup[job.Facility];
            return facility.OutputStorage;
        }

        private void CompleteJob(ref ProductionJob job, Entity entity, float deliveredAmount, uint tick, EntityCommandBuffer ecb)
        {
            var scoreValue = ProductionScoreHelpers.ComputeScore(
                job.BaseValue,
                job.Quality01,
                deliveredAmount,
                job.TotalTicks);

            if (_scoreLookup.HasComponent(entity))
            {
                _scoreLookup[entity] = new ProductionJobScore
                {
                    Score = scoreValue,
                    BaseValue = job.BaseValue,
                    DeliveredAmount = deliveredAmount,
                    Quality01 = job.Quality01,
                    TimeCostTicks = job.TotalTicks,
                    ScoredTick = tick
                };
            }
            else
            {
                ecb.AddComponent(entity, new ProductionJobScore
                {
                    Score = scoreValue,
                    BaseValue = job.BaseValue,
                    DeliveredAmount = deliveredAmount,
                    Quality01 = job.Quality01,
                    TimeCostTicks = job.TotalTicks,
                    ScoredTick = tick
                });
            }

            job.State = ProductionJobState.Done;
            job.StallReason = ProductionJobStallReason.None;
            job.LastUpdateTick = tick;
            ReleaseUsage(job);
            job.Flags = (byte)(job.Flags & ~(ProductionJobFlags.InputsReserved | ProductionJobFlags.UsageAllocated));
        }

        private void ReleaseUsage(in ProductionJob job)
        {
            if ((job.Flags & ProductionJobFlags.UsageAllocated) == 0 ||
                job.Facility == Entity.Null ||
                !_facilityUsageLookup.HasComponent(job.Facility))
            {
                return;
            }

            var usage = _facilityUsageLookup[job.Facility];
            ProductionUsageHelpers.Release(ref usage, job);
            _facilityUsageLookup[job.Facility] = usage;
        }
    }
}

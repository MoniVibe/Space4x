using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Production;
using PureDOTS.Runtime.Resource;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Production
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionJobAllocationSystem))]
    public partial struct ProductionJobExecutionSystem : ISystem
    {
        private ComponentLookup<ProductionFacility> _facilityLookup;
        private ComponentLookup<ProductionFacilityUsage> _facilityUsageLookup;
        private ComponentLookup<StorehouseInventory> _storehouseInventoryLookup;
        private BufferLookup<StorehouseInventoryItem> _storehouseItemsLookup;
        private BufferLookup<ProductionInputReservation> _reservationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ResourceTypeIndex>();
            _facilityLookup = state.GetComponentLookup<ProductionFacility>(true);
            _facilityUsageLookup = state.GetComponentLookup<ProductionFacilityUsage>(false);
            _storehouseInventoryLookup = state.GetComponentLookup<StorehouseInventory>(false);
            _storehouseItemsLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);
            _reservationLookup = state.GetBufferLookup<ProductionInputReservation>(false);
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

            if (!SystemAPI.TryGetSingleton<ResourceTypeIndex>(out var resourceTypeIndex) ||
                !resourceTypeIndex.Catalog.IsCreated)
            {
                return;
            }

            _facilityLookup.Update(ref state);
            _facilityUsageLookup.Update(ref state);
            _storehouseInventoryLookup.Update(ref state);
            _storehouseItemsLookup.Update(ref state);
            _reservationLookup.Update(ref state);

            var catalog = resourceTypeIndex.Catalog;
            var tick = tickTime.Tick;

            foreach (var (job, entity) in SystemAPI.Query<RefRW<ProductionJob>>().WithEntityAccess())
            {
                if (job.ValueRO.State != ProductionJobState.Allocated)
                {
                    continue;
                }

                var inputStorage = ResolveInputStorage(job.ValueRO);
                if (inputStorage == Entity.Null ||
                    !_storehouseInventoryLookup.HasComponent(inputStorage) ||
                    !_storehouseItemsLookup.HasBuffer(inputStorage))
                {
                    StallJob(ref job.ValueRW, ProductionJobStallReason.MissingStorage);
                    ReleaseUsage(job.ValueRO);
                    job.ValueRW.Flags = (byte)(job.ValueRO.Flags & ~ProductionJobFlags.UsageAllocated);
                    ClearReservations(entity, catalog);
                    continue;
                }

                if (!_reservationLookup.HasBuffer(entity))
                {
                    if ((job.ValueRO.Flags & ProductionJobFlags.InputsReserved) != 0)
                    {
                        if (job.ValueRO.LastUpdateTick == tick)
                        {
                            continue;
                        }

                        StallJob(ref job.ValueRW, ProductionJobStallReason.ReservationFailed);
                        ReleaseUsage(job.ValueRO);
                        job.ValueRW.Flags = (byte)(job.ValueRO.Flags & ~ProductionJobFlags.UsageAllocated);
                        continue;
                    }

                    BeginExecution(ref job.ValueRW, tick);
                    continue;
                }

                var reservations = _reservationLookup[entity];
                if (reservations.Length == 0)
                {
                    BeginExecution(ref job.ValueRW, tick);
                    continue;
                }
                var inventory = _storehouseInventoryLookup[inputStorage];
                var items = _storehouseItemsLookup[inputStorage];
                var reservationFailed = false;

                for (int i = 0; i < reservations.Length; i++)
                {
                    var reservation = reservations[i];
                    if (reservation.Status != ProductionReservationStatus.Active)
                    {
                        continue;
                    }

                    if (!StorehouseMutationService.CommitWithdrawReservedOut(
                            reservation.ResourceTypeIndex,
                            reservation.ReservedAmount,
                            catalog,
                            ref inventory,
                            items,
                            out var withdrawnAmount))
                    {
                        reservationFailed = true;
                        break;
                    }

                    if (withdrawnAmount + 1e-3f < reservation.ReservedAmount)
                    {
                        reservationFailed = true;
                        break;
                    }
                }

                _storehouseInventoryLookup[inputStorage] = inventory;

                if (reservationFailed)
                {
                    StallJob(ref job.ValueRW, ProductionJobStallReason.ReservationFailed);
                    ReleaseUsage(job.ValueRO);
                    job.ValueRW.Flags = (byte)(job.ValueRO.Flags & ~ProductionJobFlags.UsageAllocated);
                    ClearReservations(entity, catalog);
                    continue;
                }

                reservations.Clear();
                BeginExecution(ref job.ValueRW, tick);
            }
        }

        private Entity ResolveInputStorage(in ProductionJob job)
        {
            if (job.InputStorage != Entity.Null)
            {
                return job.InputStorage;
            }

            if (job.Facility == Entity.Null || !_facilityLookup.HasComponent(job.Facility))
            {
                return Entity.Null;
            }

            var facility = _facilityLookup[job.Facility];
            return facility.InputStorage;
        }

        private void StallJob(ref ProductionJob job, ProductionJobStallReason reason)
        {
            job.State = ProductionJobState.Stalled;
            job.StallReason = reason;
            job.Flags = (byte)(job.Flags & ~ProductionJobFlags.InputsReserved);
        }

        private void BeginExecution(ref ProductionJob job, uint tick)
        {
            job.Flags = (byte)(job.Flags & ~ProductionJobFlags.InputsReserved);
            job.State = ProductionJobState.Executing;
            job.StallReason = ProductionJobStallReason.None;
            job.StartTick = job.StartTick == 0 ? tick : job.StartTick;
            job.LastUpdateTick = tick;
            if (job.TotalTicks == 0)
            {
                job.TotalTicks = 1;
            }
            if (job.RemainingTicks == 0)
            {
                job.RemainingTicks = job.TotalTicks;
            }
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

        private void ClearReservations(Entity jobEntity, BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            if (!_reservationLookup.HasBuffer(jobEntity))
            {
                return;
            }

            var reservations = _reservationLookup[jobEntity];
            for (int i = 0; i < reservations.Length; i++)
            {
                var reservation = reservations[i];
                if (reservation.Storage == Entity.Null || !_storehouseItemsLookup.HasBuffer(reservation.Storage))
                {
                    continue;
                }

                var items = _storehouseItemsLookup[reservation.Storage];
                StorehouseMutationService.CancelReserveOut(
                    reservation.ResourceTypeIndex,
                    reservation.ReservedAmount,
                    catalog,
                    items);
            }

            reservations.Clear();
        }
    }
}

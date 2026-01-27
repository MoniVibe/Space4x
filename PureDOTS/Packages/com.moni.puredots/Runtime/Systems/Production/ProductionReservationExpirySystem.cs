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
    [UpdateBefore(typeof(ProductionJobAllocationSystem))]
    public partial struct ProductionReservationExpirySystem : ISystem
    {
        private ComponentLookup<ProductionFacilityUsage> _facilityUsageLookup;
        private BufferLookup<StorehouseInventoryItem> _storehouseItemsLookup;
        private BufferLookup<ProductionInputReservation> _reservationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ResourceTypeIndex>();
            _facilityUsageLookup = state.GetComponentLookup<ProductionFacilityUsage>(false);
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

            _facilityUsageLookup.Update(ref state);
            _storehouseItemsLookup.Update(ref state);
            _reservationLookup.Update(ref state);

            var tick = tickTime.Tick;
            var catalog = resourceTypeIndex.Catalog;

            foreach (var (job, entity) in SystemAPI.Query<RefRW<ProductionJob>>().WithEntityAccess())
            {
                if ((job.ValueRO.Flags & ProductionJobFlags.UsageAllocated) != 0 &&
                    (job.ValueRO.State == ProductionJobState.Cancelled ||
                     job.ValueRO.State == ProductionJobState.Done))
                {
                    ReleaseFacilityUsage(job.ValueRO);
                    job.ValueRW.Flags = (byte)(job.ValueRO.Flags & ~ProductionJobFlags.UsageAllocated);
                }

                if (!_reservationLookup.HasBuffer(entity))
                {
                    continue;
                }

                var reservations = _reservationLookup[entity];
                var expiredReservation = false;

                for (int i = reservations.Length - 1; i >= 0; i--)
                {
                    var reservation = reservations[i];
                    if (reservation.Status != ProductionReservationStatus.Active)
                    {
                        reservations.RemoveAtSwapBack(i);
                        continue;
                    }

                    if (reservation.ExpiryTick == 0 || tick < reservation.ExpiryTick)
                    {
                        continue;
                    }

                    ReleaseReservation(reservation, catalog);
                    reservation.Status = ProductionReservationStatus.Expired;
                    reservations.RemoveAtSwapBack(i);
                    expiredReservation = true;
                }

                if (!expiredReservation)
                {
                    continue;
                }

                job.ValueRW.State = ProductionJobState.Stalled;
                job.ValueRW.StallReason = ProductionJobStallReason.ReservationFailed;
                job.ValueRW.Flags = (byte)(job.ValueRO.Flags & ~ProductionJobFlags.InputsReserved);

                if ((job.ValueRO.Flags & ProductionJobFlags.UsageAllocated) != 0)
                {
                    ReleaseFacilityUsage(job.ValueRO);
                    job.ValueRW.Flags = (byte)(job.ValueRW.Flags & ~ProductionJobFlags.UsageAllocated);
                }
            }
        }

        private void ReleaseReservation(
            in ProductionInputReservation reservation,
            BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            if (reservation.Storage == Entity.Null ||
                !_storehouseItemsLookup.HasBuffer(reservation.Storage))
            {
                return;
            }

            var items = _storehouseItemsLookup[reservation.Storage];
            StorehouseMutationService.CancelReserveOut(
                reservation.ResourceTypeIndex,
                reservation.ReservedAmount,
                catalog,
                items);
        }

        private void ReleaseFacilityUsage(in ProductionJob job)
        {
            var facilityEntity = job.Facility;
            if (facilityEntity == Entity.Null || !_facilityUsageLookup.HasComponent(facilityEntity))
            {
                return;
            }

            var usage = _facilityUsageLookup[facilityEntity];
            ProductionUsageHelpers.Release(ref usage, job);
            _facilityUsageLookup[facilityEntity] = usage;
        }
    }
}

using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Production;
using PureDOTS.Runtime.Resource;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Production
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionReservationExpirySystem))]
    public partial struct ProductionJobAllocationSystem : ISystem
    {
        private const uint DefaultReservationExpiryTicks = 600;

        private ComponentLookup<ProductionFacility> _facilityLookup;
        private ComponentLookup<ProductionFacilityUsage> _facilityUsageLookup;
        private BufferLookup<ProductionJobInput> _inputLookup;
        private BufferLookup<ProductionInputReservation> _reservationLookup;
        private BufferLookup<StorehouseInventoryItem> _storehouseItemsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<ResourceTypeIndex>();
            _facilityLookup = state.GetComponentLookup<ProductionFacility>(true);
            _facilityUsageLookup = state.GetComponentLookup<ProductionFacilityUsage>(false);
            _inputLookup = state.GetBufferLookup<ProductionJobInput>(true);
            _reservationLookup = state.GetBufferLookup<ProductionInputReservation>(false);
            _storehouseItemsLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);
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
            _inputLookup.Update(ref state);
            _reservationLookup.Update(ref state);
            _storehouseItemsLookup.Update(ref state);

            var catalog = resourceTypeIndex.Catalog;
            var tick = tickTime.Tick;

            var expiryTicks = DefaultReservationExpiryTicks;
            var allowPartialReservations = false;
            if (SystemAPI.TryGetSingleton<ProductionReservationPolicy>(out var policy))
            {
                expiryTicks = policy.DefaultExpiryTicks > 0 ? policy.DefaultExpiryTicks : DefaultReservationExpiryTicks;
                allowPartialReservations = policy.AllowPartialReservations != 0;
            }

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (job, entity) in SystemAPI.Query<RefRW<ProductionJob>>().WithEntityAccess())
            {
                if (job.ValueRO.State != ProductionJobState.Planned &&
                    job.ValueRO.State != ProductionJobState.Stalled)
                {
                    continue;
                }

                if ((job.ValueRO.Flags & ProductionJobFlags.InputsReserved) != 0)
                {
                    ReleaseReservations(entity, catalog);
                    job.ValueRW.Flags = (byte)(job.ValueRO.Flags & ~ProductionJobFlags.InputsReserved);
                }

                if ((job.ValueRO.Flags & ProductionJobFlags.UsageAllocated) != 0)
                {
                    ReleaseFacilityUsage(job.ValueRO);
                    job.ValueRW.Flags = (byte)(job.ValueRO.Flags & ~ProductionJobFlags.UsageAllocated);
                }

                if (!TryResolveFacility(job.ValueRO, out var facility))
                {
                    job.ValueRW.State = ProductionJobState.Stalled;
                    job.ValueRW.StallReason = ProductionJobStallReason.MissingCapacity;
                    continue;
                }

                if (!_facilityUsageLookup.HasComponent(job.ValueRO.Facility))
                {
                    job.ValueRW.State = ProductionJobState.Stalled;
                    job.ValueRW.StallReason = ProductionJobStallReason.MissingCapacity;
                    continue;
                }

                var inputStorage = ResolveInputStorage(job.ValueRO, facility);
                if (inputStorage == Entity.Null || !_storehouseItemsLookup.HasBuffer(inputStorage))
                {
                    job.ValueRW.State = ProductionJobState.Stalled;
                    job.ValueRW.StallReason = ProductionJobStallReason.MissingStorage;
                    continue;
                }

                if (!HasCapacity(ref job.ValueRW, facility, job.ValueRO.Facility))
                {
                    continue;
                }

                if (!_inputLookup.HasBuffer(entity) || _inputLookup[entity].Length == 0)
                {
                    AllocateAndCommit(job);
                    job.ValueRW.Flags = (byte)(job.ValueRO.Flags | ProductionJobFlags.UsageAllocated);
                    job.ValueRW.State = ProductionJobState.Allocated;
                    job.ValueRW.StallReason = ProductionJobStallReason.None;
                    job.ValueRW.LastUpdateTick = tick;
                    continue;
                }

                var inputs = _inputLookup[entity];
                var storehouseItems = _storehouseItemsLookup[inputStorage];
                var reservations = new NativeList<ProductionInputReservation>(inputs.Length, Allocator.Temp);
                var reservationFailed = false;

                for (int i = 0; i < inputs.Length; i++)
                {
                    var input = inputs[i];
                    if (!TryResolveResourceTypeIndex(input.ResourceId, catalog, out var inputResourceTypeIndex))
                    {
                        reservationFailed = true;
                        break;
                    }

                    if (!StorehouseMutationService.TryReserveOut(
                            inputResourceTypeIndex,
                            input.Amount,
                            allowPartialReservations,
                            catalog,
                            storehouseItems,
                            out var reservedAmount))
                    {
                        reservationFailed = true;
                        break;
                    }

                    reservations.Add(new ProductionInputReservation
                    {
                        Storage = inputStorage,
                        ResourceTypeIndex = inputResourceTypeIndex,
                        ReservedAmount = reservedAmount,
                        ExpiryTick = tick + expiryTicks,
                        Status = ProductionReservationStatus.Active
                    });
                }

                if (reservationFailed)
                {
                    RollbackReservations(reservations, catalog);
                    reservations.Dispose();
                    job.ValueRW.State = ProductionJobState.Stalled;
                    job.ValueRW.StallReason = ProductionJobStallReason.MissingInputs;
                    continue;
                }

                ApplyReservations(entity, reservations, ecb);
                reservations.Dispose();
                AllocateAndCommit(job);
                job.ValueRW.Flags = (byte)(job.ValueRO.Flags | ProductionJobFlags.InputsReserved | ProductionJobFlags.UsageAllocated);
                job.ValueRW.State = ProductionJobState.Allocated;
                job.ValueRW.StallReason = ProductionJobStallReason.None;
                job.ValueRW.LastUpdateTick = tick;
            }
        }

        private bool TryResolveFacility(in ProductionJob job, out ProductionFacility facility)
        {
            facility = default;
            if (job.Facility == Entity.Null || !_facilityLookup.HasComponent(job.Facility))
            {
                return false;
            }

            facility = _facilityLookup[job.Facility];
            return true;
        }

        private Entity ResolveInputStorage(in ProductionJob job, in ProductionFacility facility)
        {
            if (job.InputStorage != Entity.Null)
            {
                return job.InputStorage;
            }

            return facility.InputStorage;
        }

        private bool HasCapacity(ref ProductionJob job, in ProductionFacility facility, Entity facilityEntity)
        {
            var usage = _facilityUsageLookup[facilityEntity];

            if (job.RequiredLanes > 0 && usage.LanesInUse + job.RequiredLanes > facility.LaneCapacity)
            {
                job.State = ProductionJobState.Stalled;
                job.StallReason = ProductionJobStallReason.MissingCapacity;
                return false;
            }

            if (job.RequiredSeats > 0 && usage.SeatsInUse + job.RequiredSeats > facility.SeatCapacity)
            {
                job.State = ProductionJobState.Stalled;
                job.StallReason = ProductionJobStallReason.MissingSeats;
                return false;
            }

            if (job.RequiredPower > 0f && usage.PowerInUse + job.RequiredPower > facility.PowerCapacity)
            {
                job.State = ProductionJobState.Stalled;
                job.StallReason = ProductionJobStallReason.MissingPower;
                return false;
            }

            return true;
        }

        private void AllocateAndCommit(RefRW<ProductionJob> job)
        {
            var usage = _facilityUsageLookup[job.ValueRO.Facility];
            ProductionUsageHelpers.Allocate(ref usage, job.ValueRO);
            _facilityUsageLookup[job.ValueRO.Facility] = usage;
        }

        private void ApplyReservations(
            Entity entity,
            NativeList<ProductionInputReservation> reservations,
            EntityCommandBuffer ecb)
        {
            if (_reservationLookup.HasBuffer(entity))
            {
                var buffer = _reservationLookup[entity];
                buffer.Clear();
                for (int i = 0; i < reservations.Length; i++)
                {
                    buffer.Add(reservations[i]);
                }
                return;
            }

            var ecbBuffer = ecb.AddBuffer<ProductionInputReservation>(entity);
            for (int i = 0; i < reservations.Length; i++)
            {
                ecbBuffer.Add(reservations[i]);
            }
        }

        private void ReleaseFacilityUsage(in ProductionJob job)
        {
            if (job.Facility == Entity.Null || !_facilityUsageLookup.HasComponent(job.Facility))
            {
                return;
            }

            var usage = _facilityUsageLookup[job.Facility];
            ProductionUsageHelpers.Release(ref usage, job);
            _facilityUsageLookup[job.Facility] = usage;
        }

        private void ReleaseReservations(Entity jobEntity, BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
            if (!_reservationLookup.HasBuffer(jobEntity))
            {
                return;
            }

            var buffer = _reservationLookup[jobEntity];
            for (int i = 0; i < buffer.Length; i++)
            {
                var reservation = buffer[i];
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

            buffer.Clear();
        }

        private void RollbackReservations(
            NativeList<ProductionInputReservation> reservations,
            BlobAssetReference<ResourceTypeIndexBlob> catalog)
        {
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
        }

        private static bool TryResolveResourceTypeIndex(
            FixedString64Bytes resourceId,
            BlobAssetReference<ResourceTypeIndexBlob> catalog,
            out ushort resourceTypeIndex)
        {
            var resolvedIndex = catalog.Value.LookupIndex(resourceId);
            if (resolvedIndex < 0)
            {
                resourceTypeIndex = ushort.MaxValue;
                return false;
            }

            resourceTypeIndex = (ushort)resolvedIndex;
            return true;
        }
    }
}

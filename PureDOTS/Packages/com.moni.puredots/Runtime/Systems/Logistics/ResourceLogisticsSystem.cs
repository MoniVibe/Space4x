using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Logistics;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;

namespace PureDOTS.Systems.Logistics
{
    /// <summary>
    /// System that manages resource logistics orders and shipments.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial struct ResourceLogisticsSystem : ISystem
    {
        private ComponentLookup<Shipment> _shipmentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _shipmentLookup = state.GetComponentLookup<Shipment>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            _shipmentLookup.Update(ref state);

            foreach (var (order, entity) in SystemAPI.Query<RefRW<LogisticsOrder>>().WithEntityAccess())
            {
                var currentOrder = order.ValueRO;
                if (currentOrder.ShipmentEntity == Entity.Null ||
                    !_shipmentLookup.HasComponent(currentOrder.ShipmentEntity))
                {
                    continue;
                }

                var shipment = _shipmentLookup[currentOrder.ShipmentEntity];
                switch (shipment.Status)
                {
                    case ShipmentStatus.Created:
                    case ShipmentStatus.Loading:
                        SetStatusIfDifferent(ref order.ValueRW, LogisticsOrderStatus.Dispatched);
                        break;
                    case ShipmentStatus.InTransit:
                    case ShipmentStatus.Unloading:
                    case ShipmentStatus.Rerouting:
                        SetStatusIfDifferent(ref order.ValueRW, LogisticsOrderStatus.InTransit);
                        break;
                    case ShipmentStatus.Delivered:
                        SetStatusIfDifferent(ref order.ValueRW, LogisticsOrderStatus.Delivered);
                        break;
                    case ShipmentStatus.Failed:
                        SetStatusIfDifferent(ref order.ValueRW, LogisticsOrderStatus.Failed);
                        break;
                }
            }
        }

        [BurstCompile]
        private static void SetStatusIfDifferent(ref LogisticsOrder order, LogisticsOrderStatus desiredStatus)
        {
            if (order.Status != desiredStatus)
            {
                order.Status = desiredStatus;
            }
        }
    }
}


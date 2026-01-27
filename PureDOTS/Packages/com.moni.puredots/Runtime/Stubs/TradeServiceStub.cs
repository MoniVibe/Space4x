// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Trade
{
    public static class TradeServiceStub
    {
        public static void SetTradeIntent(EntityManager manager, Entity entity, int targetEntityId, byte action)
        {
            if (!manager.HasComponent<TradeIntent>(entity))
            {
                manager.AddComponentData(entity, new TradeIntent
                {
                    TargetEntityId = targetEntityId,
                    Action = action
                });
            }
            else
            {
                var intent = manager.GetComponentData<TradeIntent>(entity);
                intent.TargetEntityId = targetEntityId;
                intent.Action = action;
                manager.SetComponentData(entity, intent);
            }
        }
    }
}

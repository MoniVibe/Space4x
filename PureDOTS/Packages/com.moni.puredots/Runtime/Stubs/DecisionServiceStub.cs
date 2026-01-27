// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Decision
{
    public static class DecisionServiceStub
    {
        public static void EnsureTicket(EntityManager manager, Entity entity)
        {
            if (!manager.HasComponent<DecisionTicket>(entity))
            {
                manager.AddComponentData(entity, new DecisionTicket
                {
                    TicketId = entity.Index,
                    State = 0
                });
            }

            if (!manager.HasBuffer<DecisionRequestElement>(entity))
            {
                manager.AddBuffer<DecisionRequestElement>(entity);
            }
        }
    }
}

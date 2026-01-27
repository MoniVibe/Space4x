// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Motivation
{
    public static class AmbitionServiceStub
    {
        public static void RegisterAmbition(EntityManager manager, Entity entity, int ambitionId, byte priority)
        {
            if (!manager.HasComponent<AmbitionState>(entity))
            {
                manager.AddComponentData(entity, new AmbitionState
                {
                    AmbitionId = ambitionId,
                    Priority = priority,
                    Progress = 0f
                });
            }
        }

        public static void QueueDesire(EntityManager manager, Entity entity, int desireId, byte priority)
        {
            if (!manager.HasBuffer<DesireElement>(entity))
            {
                manager.AddBuffer<DesireElement>(entity);
            }

            var buffer = manager.GetBuffer<DesireElement>(entity);
            buffer.Add(new DesireElement
            {
                DesireId = desireId,
                Priority = priority
            });
        }
    }
}

using Unity.Entities;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Time
{
    public static class HeadlessExitUtility
    {
        public static void Request(EntityManager entityManager, uint tick, int exitCode)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<HeadlessExitRequest>());
            var entity = query.IsEmptyIgnoreFilter
                ? entityManager.CreateEntity(typeof(HeadlessExitRequest))
                : query.GetSingletonEntity();

            entityManager.SetComponentData(entity, new HeadlessExitRequest
            {
                ExitCode = exitCode,
                RequestedTick = tick
            });
        }
    }
}


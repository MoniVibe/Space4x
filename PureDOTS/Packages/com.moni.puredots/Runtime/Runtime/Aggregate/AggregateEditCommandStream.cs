using Unity.Entities;

namespace PureDOTS.Runtime.Aggregate
{
    public struct AggregateEditCommandStreamTag : IComponentData
    {
    }

    public struct AggregateEditCommandStreamSingleton : IComponentData
    {
        public Entity Stream;
    }

    public static class AggregateEditCommandStreamUtility
    {
        public static Entity EnsureStream(EntityManager entityManager)
        {
            var streamEntity = EnsureStreamEntity(entityManager);
            EnsureSingletonReference(entityManager, streamEntity);
            return streamEntity;
        }

        public static bool TryGetStream(EntityManager entityManager, out Entity streamEntity)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AggregateEditCommandStreamSingleton>());

            if (query.IsEmptyIgnoreFilter)
            {
                streamEntity = Entity.Null;
                return false;
            }

            var singletonEntity = query.GetSingletonEntity();
            var singleton = entityManager.GetComponentData<AggregateEditCommandStreamSingleton>(singletonEntity);

            if (!entityManager.Exists(singleton.Stream))
            {
                streamEntity = Entity.Null;
                return false;
            }

            streamEntity = singleton.Stream;

            if (!entityManager.HasBuffer<AggregateEditCommand>(streamEntity))
            {
                entityManager.AddBuffer<AggregateEditCommand>(streamEntity);
            }

            return true;
        }

        private static Entity EnsureStreamEntity(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AggregateEditCommandStreamTag>());
            Entity streamEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                streamEntity = entityManager.CreateEntity(typeof(AggregateEditCommandStreamTag));
            }
            else
            {
                streamEntity = query.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<AggregateEditCommand>(streamEntity))
            {
                entityManager.AddBuffer<AggregateEditCommand>(streamEntity);
            }

            return streamEntity;
        }

        private static void EnsureSingletonReference(EntityManager entityManager, Entity streamEntity)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<AggregateEditCommandStreamSingleton>());

            if (query.IsEmptyIgnoreFilter)
            {
                var singletonEntity = entityManager.CreateEntity(typeof(AggregateEditCommandStreamSingleton));
                entityManager.SetComponentData(singletonEntity, new AggregateEditCommandStreamSingleton
                {
                    Stream = streamEntity
                });
            }
            else
            {
                var singletonEntity = query.GetSingletonEntity();
                var singleton = entityManager.GetComponentData<AggregateEditCommandStreamSingleton>(singletonEntity);
                if (singleton.Stream != streamEntity)
                {
                    singleton.Stream = streamEntity;
                    entityManager.SetComponentData(singletonEntity, singleton);
                }
            }
        }
    }
}

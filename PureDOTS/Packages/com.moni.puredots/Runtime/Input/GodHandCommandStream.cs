using Unity.Entities;

namespace PureDOTS.Input
{
    /// <summary>
    /// Tag placed on the entity that stores GodHand command events.
    /// </summary>
    public struct GodHandCommandStreamTag : IComponentData
    {
    }

    /// <summary>
    /// Singleton pointing at the active GodHand command stream entity.
    /// </summary>
    public struct GodHandCommandStreamSingleton : IComponentData
    {
        public Entity Entity;
    }

    /// <summary>
    /// Helper methods for creating and querying the GodHand command event stream.
    /// </summary>
    public static class GodHandCommandStreamUtility
    {
        /// <summary>
        /// Ensures the stream entity exists, has the proper buffer, and the singleton points at it.
        /// </summary>
        public static Entity EnsureStream(EntityManager entityManager)
        {
            var streamEntity = EnsureStreamEntity(entityManager);
            EnsureSingletonReference(entityManager, streamEntity);
            return streamEntity;
        }

        /// <summary>
        /// Attempts to get the existing stream entity without creating new structural changes.
        /// </summary>
        public static bool TryGetStream(EntityManager entityManager, out Entity streamEntity)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GodHandCommandStreamSingleton>());

            if (query.IsEmptyIgnoreFilter)
            {
                streamEntity = Entity.Null;
                return false;
            }

            var singletonEntity = query.GetSingletonEntity();
            var singleton = entityManager.GetComponentData<GodHandCommandStreamSingleton>(singletonEntity);

            if (!entityManager.Exists(singleton.Entity))
            {
                streamEntity = Entity.Null;
                return false;
            }

            streamEntity = singleton.Entity;

            if (!entityManager.HasBuffer<GodHandCommandEvent>(streamEntity))
            {
                entityManager.AddBuffer<GodHandCommandEvent>(streamEntity);
            }

            return true;
        }

        private static Entity EnsureStreamEntity(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GodHandCommandStreamTag>());
            Entity streamEntity;

            if (query.IsEmptyIgnoreFilter)
            {
                streamEntity = entityManager.CreateEntity(typeof(GodHandCommandStreamTag));
            }
            else
            {
                streamEntity = query.GetSingletonEntity();
            }

            if (!entityManager.HasBuffer<GodHandCommandEvent>(streamEntity))
            {
                entityManager.AddBuffer<GodHandCommandEvent>(streamEntity);
            }

            return streamEntity;
        }

        private static void EnsureSingletonReference(EntityManager entityManager, Entity streamEntity)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GodHandCommandStreamSingleton>());

            if (query.IsEmptyIgnoreFilter)
            {
                var singletonEntity = entityManager.CreateEntity(typeof(GodHandCommandStreamSingleton));
                entityManager.SetComponentData(singletonEntity, new GodHandCommandStreamSingleton
                {
                    Entity = streamEntity
                });
            }
            else
            {
                var singletonEntity = query.GetSingletonEntity();
                var singleton = entityManager.GetComponentData<GodHandCommandStreamSingleton>(singletonEntity);
                if (singleton.Entity != streamEntity)
                {
                    singleton.Entity = streamEntity;
                    entityManager.SetComponentData(singletonEntity, singleton);
                }
            }
        }
    }
}

using Unity.Entities;

namespace Unity.Entities
{
    public sealed partial class EntityManagerLookupBootstrapSystem : SystemBase
    {
        protected override void OnUpdate()
        {
        }
    }

    public static class EntityManagerComponentDataExtensions
    {
        public static void AddComponent<T>(this EntityManager entityManager, Entity entity, T componentData)
            where T : unmanaged, IComponentData
        {
            entityManager.AddComponentData(entity, componentData);
        }

        public static EntityStorageInfoLookup GetEntityStorageInfoLookup(this EntityManager entityManager)
        {
            ref var state = ref GetLookupState(entityManager);
            return state.GetEntityStorageInfoLookup();
        }

        public static ComponentLookup<T> GetComponentLookup<T>(this EntityManager entityManager, bool isReadOnly = false)
            where T : unmanaged, IComponentData
        {
            ref var state = ref GetLookupState(entityManager);
            return state.GetComponentLookup<T>(isReadOnly);
        }

        public static BufferLookup<T> GetBufferLookup<T>(this EntityManager entityManager, bool isReadOnly = false)
            where T : unmanaged, IBufferElementData
        {
            ref var state = ref GetLookupState(entityManager);
            return state.GetBufferLookup<T>(isReadOnly);
        }

        private static ref SystemState GetLookupState(EntityManager entityManager)
        {
            var world = entityManager.World;
            var system = world.GetOrCreateSystemManaged<EntityManagerLookupBootstrapSystem>();
            return ref system.CheckedStateRef;
        }
    }
}

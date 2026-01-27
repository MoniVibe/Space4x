using Unity.Entities;

namespace PureDOTS.Runtime.Time
{
    /// <summary>
    /// Helper methods for reading effective delta time.
    /// All time-consuming systems should use these helpers instead of raw delta.
    /// </summary>
    public static class TimeAwareHelpers
    {
        /// <summary>
        /// Gets the effective delta time for an entity.
        /// If entity has EffectiveDeltaTime component, returns that value.
        /// Otherwise returns the global delta time.
        /// </summary>
        public static float GetEffectiveDelta(EntityManager entityManager, Entity entity, float globalDelta)
        {
            if (entityManager.HasComponent<EffectiveDeltaTime>(entity))
            {
                return entityManager.GetComponentData<EffectiveDeltaTime>(entity).Value;
            }
            return globalDelta;
        }

        /// <summary>
        /// Gets the effective delta time for an entity using ComponentLookup.
        /// More efficient for batch operations.
        /// </summary>
        public static float GetEffectiveDelta(ComponentLookup<EffectiveDeltaTime> lookup, Entity entity, float globalDelta)
        {
            if (lookup.HasComponent(entity))
            {
                return lookup[entity].Value;
            }
            return globalDelta;
        }

        /// <summary>
        /// Gets the effective delta time for an entity using RefRO.
        /// </summary>
        public static float GetEffectiveDelta(RefRO<EffectiveDeltaTime>? effectiveDelta, float globalDelta)
        {
            if (effectiveDelta.HasValue)
            {
                return effectiveDelta.Value.ValueRO.Value;
            }
            return globalDelta;
        }
    }
}




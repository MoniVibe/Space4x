using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Systems.Spatial
{
    /// <summary>
    /// Example system demonstrating spatial grid usage for interactions.
    /// This is the recommended approach for most entity interactions.
    /// 
    /// Use this pattern instead of DOTS Physics for:
    /// - Range queries (finding nearby entities)
    /// - Proximity checks (is entity A near entity B?)
    /// - Area-of-effect calculations
    /// - AI sensing (vision, hearing)
    /// 
    /// Only use DOTS Physics when:
    /// - You need realistic collision response (bouncing, pushing)
    /// - You need precise collision shapes
    /// - The gameplay mechanic requires physical simulation
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct SpatialInteractionExampleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // This system is an example - disabled by default
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Example: Find nearby entities using spatial grid
            // This is much faster than iterating all entities or using physics queries
            
            // Example usage of helper methods (uncomment to test):
            // var results = new NativeList<Entity>(Allocator.Temp);
            // FindNearbyVillagers(ref state, float3.zero, 10f, ref results);
            // results.Dispose();
        }

        /// <summary>
        /// Example: Find all villagers within range of a position.
        /// Uses spatial grid for O(1) cell lookup instead of O(n) iteration.
        /// Note: This is a non-static helper that can be called from OnUpdate.
        /// </summary>
        private void FindNearbyVillagers(
            ref SystemState state,
            float3 position,
            float range,
            ref NativeList<Entity> results)
        {
            // In a real implementation, this would query the spatial grid
            // For now, demonstrate the pattern with a simple query
            foreach (var (transform, entity) in
                SystemAPI.Query<RefRO<LocalTransform>>()
                    .WithAll<VillagerId>()
                    .WithEntityAccess())
            {
                float distance = math.distance(position, transform.ValueRO.Position);
                if (distance <= range)
                {
                    results.Add(entity);
                }
            }
        }

        /// <summary>
        /// Example: Check if two entities are within interaction range.
        /// Fast distance check - no physics required.
        /// Note: This is a non-static helper that can be called from OnUpdate.
        /// </summary>
        private bool AreEntitiesInRange(
            ref SystemState state,
            Entity entityA,
            Entity entityB,
            float range)
        {
            if (!SystemAPI.HasComponent<LocalTransform>(entityA) ||
                !SystemAPI.HasComponent<LocalTransform>(entityB))
            {
                return false;
            }

            var posA = SystemAPI.GetComponent<LocalTransform>(entityA).Position;
            var posB = SystemAPI.GetComponent<LocalTransform>(entityB).Position;
            return math.distancesq(posA, posB) <= range * range;
        }

        /// <summary>
        /// Example: Perform a raycast using spatial grid.
        /// Use for projectile hit detection instead of physics bodies.
        /// Note: This is a non-static helper that can be called from OnUpdate.
        /// </summary>
        private bool SpatialRaycast(
            ref SystemState state,
            float3 origin,
            float3 direction,
            float maxDistance,
            out Entity hitEntity,
            out float3 hitPoint)
        {
            hitEntity = Entity.Null;
            hitPoint = float3.zero;

            // In a real implementation, this would use the spatial grid's raycast
            // For demonstration, show the pattern
            float3 endPoint = origin + direction * maxDistance;
            float closestDistance = float.MaxValue;

            foreach (var (transform, entity) in
                SystemAPI.Query<RefRO<LocalTransform>>()
                    .WithAll<SpatialIndexedTag>()
                    .WithEntityAccess())
            {
                // Simple sphere intersection test
                float3 toEntity = transform.ValueRO.Position - origin;
                float projection = math.dot(toEntity, direction);
                
                if (projection < 0 || projection > maxDistance)
                {
                    continue;
                }

                float3 closestPoint = origin + direction * projection;
                float distanceToLine = math.distance(closestPoint, transform.ValueRO.Position);
                
                // Assume 1 unit radius for simplicity
                if (distanceToLine < 1f && projection < closestDistance)
                {
                    closestDistance = projection;
                    hitEntity = entity;
                    hitPoint = closestPoint;
                }
            }

            return hitEntity != Entity.Null;
        }
    }

    /// <summary>
    /// System that initializes entities with spatial grid markers.
    /// Adds UsesSpatialGrid component to entities that should use grid-based interactions.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SpatialGridInitializationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // This system runs once to mark entities for spatial grid usage
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Add UsesSpatialGrid to all spatial-indexed entities that don't have it
            foreach (var (spatialTag, entity) in
                SystemAPI.Query<RefRO<SpatialIndexedTag>>()
                    .WithNone<UsesSpatialGrid>()
                    .WithEntityAccess())
            {
                state.EntityManager.AddComponentData(entity, new UsesSpatialGrid
                {
                    QueryRadius = 10f,
                    Flags = SpatialQueryFlags.Queryable | SpatialQueryFlags.CanQuery
                });
            }
        }
    }
}


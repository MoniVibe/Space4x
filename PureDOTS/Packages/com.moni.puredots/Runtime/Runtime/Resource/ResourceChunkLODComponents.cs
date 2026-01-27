using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Rendering;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Resource
{
    /// <summary>
    /// Helper methods for adding LOD and physics components to resource chunks.
    /// </summary>
    public static class ResourceChunkLODHelpers
    {
        /// <summary>
        /// Adds LOD components to a resource chunk entity.
        /// </summary>
        public static void AddLODComponents(EntityManager entityManager, Entity entity, float cullDistance = 150f)
        {
            // Add render LOD data
            entityManager.AddComponentData(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = 0.3f, // Lower importance than villagers
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            // Add cullable marker
            entityManager.AddComponentData(entity, new RenderCullable
            {
                CullDistance = cullDistance,
                Priority = 64 // Lower priority than villagers
            });

            // Add sample index for density control
            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entity.Index, 100);
            entityManager.AddComponentData(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 100,
                ShouldRender = 1
            });
        }

        /// <summary>
        /// Adds LOD components to a resource chunk entity using ECB.
        /// </summary>
        public static void AddLODComponents(EntityCommandBuffer ecb, Entity entity, int entityIndex, float cullDistance = 150f)
        {
            ecb.AddComponent(entity, new RenderLODData
            {
                CameraDistance = 0f,
                ImportanceScore = 0.3f,
                RecommendedLOD = 0,
                LastUpdateTick = 0
            });

            ecb.AddComponent(entity, new RenderCullable
            {
                CullDistance = cullDistance,
                Priority = 64
            });

            var sampleIndex = RenderLODHelpers.CalculateSampleIndex(entityIndex, 100);
            ecb.AddComponent(entity, new RenderSampleIndex
            {
                SampleIndex = sampleIndex,
                SampleModulus = 100,
                ShouldRender = 1
            });
        }

        /// <summary>
        /// Adds ballistic motion components for a thrown resource chunk.
        /// </summary>
        public static void AddBallisticMotion(
            EntityManager entityManager,
            Entity entity,
            float3 initialVelocity,
            float3 gravity,
            float maxFlightTime = 5f)
        {
            entityManager.AddComponentData(entity, new BallisticMotion
            {
                Velocity = initialVelocity,
                Gravity = gravity,
                FlightTime = 0f,
                MaxFlightTime = maxFlightTime,
                Flags = BallisticMotionFlags.Active | BallisticMotionFlags.UseGravity
            });

            entityManager.AddComponentData(entity, new GroundCollisionCheck
            {
                HeightOffset = 0f,
                BreakVelocityThreshold = 5f,
                Flags = 0
            });
        }

        /// <summary>
        /// Adds ballistic motion components for a thrown resource chunk using ECB.
        /// </summary>
        public static void AddBallisticMotion(
            EntityCommandBuffer ecb,
            Entity entity,
            float3 initialVelocity,
            float3 gravity,
            float maxFlightTime = 5f)
        {
            ecb.AddComponent(entity, new BallisticMotion
            {
                Velocity = initialVelocity,
                Gravity = gravity,
                FlightTime = 0f,
                MaxFlightTime = maxFlightTime,
                Flags = BallisticMotionFlags.Active | BallisticMotionFlags.UseGravity
            });

            ecb.AddComponent(entity, new GroundCollisionCheck
            {
                HeightOffset = 0f,
                BreakVelocityThreshold = 5f,
                Flags = 0
            });
        }

        /// <summary>
        /// Calculates a ballistic arc velocity for throwing a chunk from source to target.
        /// </summary>
        public static float3 CalculateThrowVelocity(
            float3 sourcePosition,
            float3 targetPosition,
            float flightTime,
            float gravity = 9.81f)
        {
            return PhysicsInteractionHelpers.CalculateBallisticArc(
                sourcePosition, targetPosition, -gravity, flightTime);
        }

        /// <summary>
        /// Marks a resource chunk as thrown.
        /// </summary>
        public static void MarkAsThrown(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<ResourceChunkState>(entity))
            {
                var state = entityManager.GetComponentData<ResourceChunkState>(entity);
                state.Flags |= ResourceChunkFlags.Thrown;
                state.Carrier = Entity.Null;
                entityManager.SetComponentData(entity, state);
            }
        }

        /// <summary>
        /// Marks a resource chunk as landed (no longer thrown).
        /// </summary>
        public static void MarkAsLanded(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<ResourceChunkState>(entity))
            {
                var state = entityManager.GetComponentData<ResourceChunkState>(entity);
                state.Flags &= ~ResourceChunkFlags.Thrown;
                state.Velocity = float3.zero;
                entityManager.SetComponentData(entity, state);
            }

            // Remove ballistic motion components
            if (entityManager.HasComponent<BallisticMotion>(entity))
            {
                entityManager.RemoveComponent<BallisticMotion>(entity);
            }
            if (entityManager.HasComponent<GroundCollisionCheck>(entity))
            {
                entityManager.RemoveComponent<GroundCollisionCheck>(entity);
            }
        }
    }
}


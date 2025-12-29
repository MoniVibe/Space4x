using PureDOTS.Rendering;
using PureDOTS.Runtime.Physics;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Physics
{
    /// <summary>
    /// Ensures VesselPhysicalProperties.Radius matches the collider profile entry for its semantic key.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XColliderProfileSyncSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VesselPhysicalProperties>();
            state.RequireForUpdate<RenderSemanticKey>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PhysicsColliderProfileComponent>(out var profileComponent) ||
                !profileComponent.Profile.IsCreated)
            {
                return;
            }

            var entries = profileComponent.Profile.Value.Entries;

            foreach (var (physical, semanticKey) in SystemAPI.Query<RefRW<VesselPhysicalProperties>, RefRO<RenderSemanticKey>>())
            {
                if (!PhysicsColliderProfileHelpers.TryGetSpec(ref entries, semanticKey.ValueRO.Value, out var spec))
                {
                    continue;
                }

                var radius = ResolveRadius(spec);
                if (radius <= 0f)
                {
                    continue;
                }

                if (math.abs(physical.ValueRO.Radius - radius) > 0.01f)
                {
                    var updated = physical.ValueRO;
                    updated.Radius = radius;
                    physical.ValueRW = updated;
                }
            }
        }

        private static float ResolveRadius(in PhysicsColliderSpec spec)
        {
            return spec.Shape switch
            {
                PhysicsColliderShape.Box => math.cmax(spec.Dimensions) * 0.5f,
                PhysicsColliderShape.Capsule => spec.Dimensions.x,
                _ => spec.Dimensions.x
            };
        }
    }
}

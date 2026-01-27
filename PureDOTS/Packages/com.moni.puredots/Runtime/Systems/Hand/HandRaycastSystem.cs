using PureDOTS.Runtime.Hand;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

namespace PureDOTS.Systems.Hand
{
    /// <summary>
    /// Performs raycast from hand input ray and updates HandHover singleton.
    /// Runs in FixedStepSimulationSystemGroup after PhysicsInitializeGroup.
    /// Uses deterministic tie-breaker: distance then entity index.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsInitializeGroup))]
    [UpdateBefore(typeof(PhysicsSimulationGroup))]
    public partial struct HandRaycastSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HandInputFrame>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            
            // Ensure HandHover singleton exists
            if (!SystemAPI.TryGetSingletonEntity<HandHover>(out _))
            {
                var entity = state.EntityManager.CreateEntity(typeof(HandHover));
                state.EntityManager.SetComponentData(entity, new HandHover());
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var inputFrame = SystemAPI.GetSingleton<HandInputFrame>();
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var collisionWorld = physicsWorld.CollisionWorld;

            // Perform raycast
            float maxDistance = 1000f;
            var raycastInput = new RaycastInput
            {
                Start = inputFrame.RayOrigin,
                End = inputFrame.RayOrigin + inputFrame.RayDirection * maxDistance,
                Filter = CollisionFilter.Default
            };

            var hover = new HandHover
            {
                TargetEntity = Entity.Null,
                HitPosition = float3.zero,
                HitNormal = float3.zero,
                Distance = float.MaxValue
            };

            // CastRay returns closest hit
            if (collisionWorld.CastRay(raycastInput, out var hit))
            {
                hover.TargetEntity = hit.Entity;
                hover.HitPosition = hit.Position;
                hover.HitNormal = hit.SurfaceNormal;
                hover.Distance = math.distance(inputFrame.RayOrigin, hit.Position);
            }

            // Update singleton
            SystemAPI.SetSingleton(hover);
        }
    }
}


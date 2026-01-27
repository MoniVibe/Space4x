using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Miracles;
using PureDOTS.Systems.Physics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Sets initial velocity on miracle tokens after physics bootstrap.
    /// Runs before BuildPhysicsWorld to ensure velocity is set before physics simulation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ThrownObjectPrePhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsBodyBootstrapSystem))]
    [UpdateBefore(typeof(ThrownObjectGravitySystem))]
    public partial struct MiracleTokenVelocitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiracleToken>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (token, velocityRef, thrownRef) in SystemAPI
                         .Query<RefRO<MiracleToken>, RefRW<PhysicsVelocity>, RefRW<BeingThrown>>())
            {
                var tokenData = token.ValueRO;
                var velocity = velocityRef.ValueRO;

                // Check if velocity is still zero (bootstrap set it, but we haven't updated it yet)
                // Also check if LaunchVelocity is non-zero (token was just spawned)
                if (math.lengthsq(velocity.Linear) < 0.0001f && math.lengthsq(tokenData.LaunchVelocity) > 0.0001f)
                {
                    // Set physics velocity from stored launch velocity
                    var newVelocity = velocity;
                    newVelocity.Linear = tokenData.LaunchVelocity;
                    newVelocity.Angular = float3.zero;
                    velocityRef.ValueRW = newVelocity;

                    // Update BeingThrown to match
                    var thrown = thrownRef.ValueRO;
                    thrown.InitialVelocity = tokenData.LaunchVelocity;
                    thrownRef.ValueRW = thrown;
                }
            }
        }
    }
}


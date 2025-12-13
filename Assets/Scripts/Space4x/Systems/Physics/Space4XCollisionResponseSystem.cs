using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems.Physics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
#if UNITY_EDITOR
using Space4X.Debug;
#endif

namespace Space4X.Systems.Physics
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Processes physics collision events for Space4X and applies damage based on impulse.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsPostEventSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Physics.PhysicsEventSystem))]
    public partial struct Space4XCollisionResponseSystem : ISystem
    {
        // Damage per unit of impulse
        private const float DamagePerImpulse = 0.1f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PhysicsConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var config = SystemAPI.GetSingleton<PhysicsConfig>();

            // Skip during rewind playback
            if (rewindState.Mode == RewindMode.Playback)
            {
                return;
            }

            // Skip if Space4X physics is disabled
            if (!config.IsSpace4XPhysicsEnabled)
            {
                return;
            }

            // Skip during post-rewind settle frames
            if (PhysicsConfigHelpers.IsPostRewindSettleFrame(in config, timeState.Tick))
            {
                return;
            }

#if UNITY_EDITOR
            bool logCollisions = config.LogCollisions != 0;
#endif

            // Process collision events for all entities with PhysicsCollisionEventElement buffers
            foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<PhysicsCollisionEventElement>>()
                .WithEntityAccess())
            {
                for (int i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    
                    // Calculate damage from impulse
                    float damage = evt.Impulse * DamagePerImpulse;

                    // Apply damage (placeholder - can be extended with health components)
                    // For now, just log the collision
#if UNITY_EDITOR
                    if (logCollisions)
                    {
                        LogCollision(entity, evt.OtherEntity, evt.Impulse, damage);
                    }
#endif

                    // TODO: Apply damage to health components if they exist
                    // if (SystemAPI.HasComponent<Health>(entity))
                    // {
                    //     var health = SystemAPI.GetComponentRW<Health>(entity);
                    //     health.ValueRW.CurrentHealth -= damage;
                    // }
                }
            }
        }

#if UNITY_EDITOR
        [BurstDiscard]
        private static void LogCollision(Entity entity, Entity otherEntity, float impulse, float damage)
        {
            float roundedImpulse = math.round(impulse * 100f) * 0.01f;
            float roundedDamage = math.round(damage * 100f) * 0.01f;
            Space4XBurstDebug.Log($"[Space4XCollision] Entity {entity.Index} hit Entity {otherEntity.Index} impulse={roundedImpulse} damage={roundedDamage}");
        }
#else
        private static void LogCollision(Entity entity, Entity otherEntity, float impulse, float damage) { }
#endif
    }
}

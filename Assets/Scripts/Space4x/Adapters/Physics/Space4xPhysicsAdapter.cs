using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Launch;
using PureDOTS.Runtime.Physics;
using PureDOTS.Systems.Physics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Adapters.Physics
{
    /// <summary>
    /// Space4X-specific physics adapter.
    /// Selects physics provider via config and subscribes to collision events.
    /// This adapter does NOT fork PureDOTS systems - it only configures and consumes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsPostEventSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Physics.PhysicsEventSystem))]
    public partial struct Space4XPhysicsAdapter : ISystem
    {
        private const float DamagePerImpulse = 0.1f;
        private ComponentLookup<LaunchedProjectileTag> _launchedLookup;
        private BufferLookup<DamageEvent> _damageBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PhysicsConfig>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _launchedLookup = state.GetComponentLookup<LaunchedProjectileTag>(true);
            _damageBufferLookup = state.GetBufferLookup<DamageEvent>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var config = SystemAPI.GetSingleton<PhysicsConfig>();

            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Select provider based on config
            // Currently only Entities (Unity Physics) is supported
            if (config.ProviderId == PhysicsProviderIds.None)
            {
                // Physics disabled - adapter does nothing
                return;
            }

            if (!config.IsSpace4XPhysicsEnabled)
            {
                return;
            }

            if (PhysicsConfigHelpers.IsPostRewindSettleFrame(in config, timeState.Tick))
            {
                return;
            }

            if (config.ProviderId == PhysicsProviderIds.Entities)
            {
                // Using Unity Physics - events are already processed by PhysicsEventSystem
                // This adapter can subscribe to PhysicsCollisionEventElement buffers here
                // and translate them to Space4X-specific behavior (ship collisions, asteroid impacts, etc.)
                ProcessSpace4XCollisionEvents(ref state);
            }
            else if (config.ProviderId == PhysicsProviderIds.Havok)
            {
                // Havok provider not implemented yet
                // When implemented, this adapter would process Havok-specific events
            }
        }

        private void ProcessSpace4XCollisionEvents(ref SystemState state)
        {
            _launchedLookup.Update(ref state);
            _damageBufferLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<PhysicsCollisionEventElement>>()
                         .WithEntityAccess())
            {
                if (events.Length == 0)
                {
                    continue;
                }

                var hasDamageBuffer = _damageBufferLookup.HasBuffer(entity);
                var wroteBuffer = false;

                for (int i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    if (evt.OtherEntity == Entity.Null)
                    {
                        continue;
                    }

                    if (_launchedLookup.HasComponent(entity) || _launchedLookup.HasComponent(evt.OtherEntity))
                    {
                        continue;
                    }

                    if (evt.EventType != PhysicsCollisionEventType.Collision)
                    {
                        continue;
                    }

                    var damage = math.max(0f, evt.Impulse) * DamagePerImpulse;
                    if (damage <= 0f)
                    {
                        continue;
                    }

                    var damageEvent = new DamageEvent
                    {
                        SourceEntity = evt.OtherEntity,
                        TargetEntity = entity,
                        RawDamage = damage,
                        Type = DamageType.Physical,
                        Tick = evt.Tick,
                        Flags = DamageFlags.None
                    };

                    if (hasDamageBuffer)
                    {
                        var buffer = _damageBufferLookup[entity];
                        buffer.Add(damageEvent);
                    }
                    else
                    {
                        if (!wroteBuffer)
                        {
                            ecb.AddBuffer<DamageEvent>(entity);
                            wroteBuffer = true;
                        }

                        ecb.AppendToBuffer(entity, damageEvent);
                    }
                }
            }
        }
    }
}

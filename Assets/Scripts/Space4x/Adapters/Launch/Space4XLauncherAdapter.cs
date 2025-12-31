using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Combat;
using PureDOTS.Environment;
using PureDOTS.Runtime.Launch;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Swarms;
using PureDOTS.Systems.Physics;
using Space4X.Authoring;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Adapters.Launch
{
    /// <summary>
    /// Space4X-specific adapter for launcher mechanics.
    /// Reads Space4X input/AI commands and writes LaunchRequest entries.
    /// Also processes collision events for launched objects.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Launch.LaunchRequestIntakeSystem))]
    public partial struct Space4XLauncherInputAdapter : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            // Only process in Record mode
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();

            // Process launchers with pending launch commands
            // In a real implementation, this would read from an AI command buffer or player input
            // For now, this is a placeholder showing the adapter pattern

            foreach (var (config, launcherConfig, requestBuffer, transform, entity) in
                SystemAPI.Query<RefRO<LauncherConfig>, RefRO<Space4XLauncherConfig>, DynamicBuffer<LaunchRequest>, RefRO<LocalTransform>>()
                    .WithAll<Space4XLauncherTag>()
                    .WithEntityAccess())
            {
                // Example: Check for pending launch commands (would come from AI or input system)
                // ProcessLaunchCommands(ref requestBuffer, config, launcherConfig, transform, timeState.Tick);
            }
        }

        /// <summary>
        /// Helper to queue a launch from a Space4X launcher.
        /// Called by AI systems or player commands when triggering a launch.
        /// </summary>
        public static void QueueLaunch(
            ref DynamicBuffer<LaunchRequest> requestBuffer,
            Entity sourceEntity,
            Entity payloadEntity,
            float3 targetPosition,
            float3 launcherPosition,
            float speed)
        {
            // Calculate launch velocity (straight line in space, no gravity arc)
            var direction = math.normalize(targetPosition - launcherPosition);
            var velocity = direction * speed;

            requestBuffer.Add(new LaunchRequest
            {
                SourceEntity = sourceEntity,
                PayloadEntity = payloadEntity,
                LaunchTick = 0, // Immediate
                InitialVelocity = velocity,
                Flags = 0
            });
        }

        /// <summary>
        /// Helper to queue a delayed launch.
        /// Useful for coordinated fleet actions or timed releases.
        /// </summary>
        public static void QueueDelayedLaunch(
            ref DynamicBuffer<LaunchRequest> requestBuffer,
            Entity sourceEntity,
            Entity payloadEntity,
            float3 velocity,
            uint launchTick)
        {
            requestBuffer.Add(new LaunchRequest
            {
                SourceEntity = sourceEntity,
                PayloadEntity = payloadEntity,
                LaunchTick = launchTick,
                InitialVelocity = velocity,
                Flags = 0
            });
        }
    }

    /// <summary>
    /// Processes collision events for launched objects in Space4X.
    /// Translates generic collision events to Space4X-specific effects.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsPostEventSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Physics.PhysicsEventSystem))]
    public partial struct Space4XLauncherCollisionAdapter : ISystem
    {
        private const float DamagePerImpulse = 0.1f;
        private const float TorpedoImpactRadiusMin = 0.5f;
        private const float TorpedoImpactRadiusMax = 12f;
        private ComponentLookup<Space4XLauncherConfig> _launcherConfigLookup;
        private BufferLookup<DamageEvent> _damageBufferLookup;
        private ComponentLookup<TerrainChunk> _terrainChunkLookup;
        private ComponentLookup<TerrainVolume> _terrainVolumeLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PhysicsConfig>();
            state.RequireForUpdate<TerrainModificationQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _launcherConfigLookup = state.GetComponentLookup<Space4XLauncherConfig>(true);
            _damageBufferLookup = state.GetBufferLookup<DamageEvent>(false);
            _terrainChunkLookup = state.GetComponentLookup<TerrainChunk>(true);
            _terrainVolumeLookup = state.GetComponentLookup<TerrainVolume>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var config = SystemAPI.GetSingleton<PhysicsConfig>();

            // Only process in Record mode
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (config.ProviderId == PhysicsProviderIds.None || !config.IsSpace4XPhysicsEnabled)
            {
                return;
            }

            if (PhysicsConfigHelpers.IsPostRewindSettleFrame(in config, timeState.Tick))
            {
                return;
            }

            _launcherConfigLookup.Update(ref state);
            _damageBufferLookup.Update(ref state);
            _terrainChunkLookup.Update(ref state);
            _terrainVolumeLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var streamEntity = EnsureRequestStream(ref state, ecb);
            var hasQueue = SystemAPI.TryGetSingletonEntity<TerrainModificationQueue>(out var queueEntity);
            DynamicBuffer<TerrainModificationRequest> terrainRequests = default;
            if (hasQueue)
            {
                terrainRequests = SystemAPI.GetBuffer<TerrainModificationRequest>(queueEntity);
            }

            // Process collision events for launched objects
            foreach (var (projectileTag, collisionBuffer, entity) in
                SystemAPI.Query<RefRO<LaunchedProjectileTag>, DynamicBuffer<PhysicsCollisionEventElement>>()
                    .WithEntityAccess())
            {
                if (collisionBuffer.Length == 0)
                {
                    continue;
                }

                if (!_launcherConfigLookup.HasComponent(projectileTag.ValueRO.SourceLauncher))
                {
                    continue;
                }

                var launchConfig = _launcherConfigLookup[projectileTag.ValueRO.SourceLauncher];
                var launchType = launchConfig.LaunchType;

                for (int i = 0; i < collisionBuffer.Length; i++)
                {
                    var collision = collisionBuffer[i];

                    if (collision.EventType == PhysicsCollisionEventType.TriggerExit)
                    {
                        continue;
                    }

                    if (collision.OtherEntity == Entity.Null)
                    {
                        continue;
                    }

                    if (launchType == Space4XLaunchType.Torpedo)
                    {
                        if (collision.EventType != PhysicsCollisionEventType.Collision)
                        {
                            continue;
                        }

                        var damage = math.max(0f, collision.Impulse) * DamagePerImpulse;
                        if (damage <= 0f)
                        {
                            continue;
                        }

                        if (hasQueue)
                        {
                            Entity volumeEntity = Entity.Null;
                            if (_terrainVolumeLookup.HasComponent(collision.OtherEntity))
                            {
                                volumeEntity = collision.OtherEntity;
                            }
                            else if (_terrainChunkLookup.HasComponent(collision.OtherEntity))
                            {
                                volumeEntity = _terrainChunkLookup[collision.OtherEntity].VolumeEntity;
                            }

                            if (volumeEntity != Entity.Null)
                            {
                                var impactPosition = collision.ContactPoint;
                                if (math.lengthsq(impactPosition) < 1e-4f && _transformLookup.HasComponent(entity))
                                {
                                    impactPosition = _transformLookup[entity].Position;
                                }

                                var radius = math.clamp(collision.Impulse * 0.1f, TorpedoImpactRadiusMin, TorpedoImpactRadiusMax);
                                terrainRequests.Add(new TerrainModificationRequest
                                {
                                    Kind = TerrainModificationKind.Dig,
                                    Shape = TerrainModificationShape.Brush,
                                    ToolKind = TerrainModificationToolKind.Drill,
                                    Start = impactPosition,
                                    End = impactPosition,
                                    Radius = radius,
                                    Depth = radius,
                                    MaterialId = 0,
                                    DamageDelta = 0,
                                    DamageThreshold = 0,
                                    YieldMultiplier = 1f,
                                    HeatDelta = 0f,
                                    InstabilityDelta = 0f,
                                    Flags = TerrainModificationFlags.AffectsSurface | TerrainModificationFlags.AffectsVolume,
                                    RequestedTick = collision.Tick,
                                    Actor = entity,
                                    VolumeEntity = volumeEntity,
                                    Space = TerrainModificationSpace.World
                                });
                            }
                        }

                        var damageEvent = new DamageEvent
                        {
                            SourceEntity = projectileTag.ValueRO.SourceLauncher,
                            TargetEntity = collision.OtherEntity,
                            RawDamage = damage,
                            Type = DamageType.Physical,
                            Tick = collision.Tick,
                            Flags = DamageFlags.None
                        };

                        if (!_damageBufferLookup.HasBuffer(collision.OtherEntity))
                        {
                            ecb.AddBuffer<DamageEvent>(collision.OtherEntity);
                        }

                        ecb.AppendToBuffer(collision.OtherEntity, damageEvent);
                        ecb.RemoveComponent<LaunchedProjectileTag>(entity);
                        break;
                    }

                    if (launchType == Space4XLaunchType.CargoPod)
                    {
                        if (collision.EventType != PhysicsCollisionEventType.TriggerEnter)
                        {
                            continue;
                        }

                        ecb.AppendToBuffer(streamEntity, new Space4XCargoDeliveryRequest
                        {
                            SourceLauncher = projectileTag.ValueRO.SourceLauncher,
                            Payload = entity,
                            Target = collision.OtherEntity,
                            Tick = collision.Tick
                        });
                        ecb.RemoveComponent<LaunchedProjectileTag>(entity);
                        break;
                    }

                    if (launchType == Space4XLaunchType.Probe)
                    {
                        if (collision.EventType != PhysicsCollisionEventType.TriggerEnter)
                        {
                            continue;
                        }

                        ecb.AppendToBuffer(streamEntity, new Space4XProbeActivateRequest
                        {
                            SourceLauncher = projectileTag.ValueRO.SourceLauncher,
                            Payload = entity,
                            Target = collision.OtherEntity,
                            Tick = collision.Tick
                        });
                        ecb.RemoveComponent<LaunchedProjectileTag>(entity);
                        break;
                    }

                    if (launchType == Space4XLaunchType.Drone)
                    {
                        if (collision.EventType != PhysicsCollisionEventType.Collision &&
                            collision.EventType != PhysicsCollisionEventType.TriggerEnter)
                        {
                            continue;
                        }

                        if (!SystemAPI.HasComponent<DroneTag>(entity))
                        {
                            ecb.AddComponent<DroneTag>(entity);
                        }
                        ecb.RemoveComponent<LaunchedProjectileTag>(entity);
                        break;
                    }

                    if (launchType == Space4XLaunchType.EscapePod)
                    {
                        if (collision.EventType != PhysicsCollisionEventType.TriggerEnter)
                        {
                            continue;
                        }

                        ecb.AppendToBuffer(streamEntity, new Space4XCrewTransferRequest
                        {
                            SourceLauncher = projectileTag.ValueRO.SourceLauncher,
                            Payload = entity,
                            Target = collision.OtherEntity,
                            Tick = collision.Tick
                        });
                        ecb.RemoveComponent<LaunchedProjectileTag>(entity);
                        break;
                    }
                }
            }
        }

        private Entity EnsureRequestStream(ref SystemState state, EntityCommandBuffer ecb)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XLaunchRequestStream>(out var streamEntity))
            {
                if (!SystemAPI.HasBuffer<Space4XCargoDeliveryRequest>(streamEntity))
                {
                    ecb.AddBuffer<Space4XCargoDeliveryRequest>(streamEntity);
                }
                if (!SystemAPI.HasBuffer<Space4XProbeActivateRequest>(streamEntity))
                {
                    ecb.AddBuffer<Space4XProbeActivateRequest>(streamEntity);
                }
                if (!SystemAPI.HasBuffer<Space4XCrewTransferRequest>(streamEntity))
                {
                    ecb.AddBuffer<Space4XCrewTransferRequest>(streamEntity);
                }
                return streamEntity;
            }

            streamEntity = ecb.CreateEntity();
            ecb.AddComponent<Space4XLaunchRequestStream>(streamEntity);
            ecb.AddBuffer<Space4XCargoDeliveryRequest>(streamEntity);
            ecb.AddBuffer<Space4XProbeActivateRequest>(streamEntity);
            ecb.AddBuffer<Space4XCrewTransferRequest>(streamEntity);
            return streamEntity;
        }
    }
}

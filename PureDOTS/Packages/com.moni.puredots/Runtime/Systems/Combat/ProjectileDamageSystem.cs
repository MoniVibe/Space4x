using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.LowLevel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Processes projectile impacts and creates damage events.
    /// Handles pierce mechanics and projectile destruction.
    /// Runs before DamageApplicationSystem.
    /// Looks up damage values from ProjectileCatalog for game-agnostic behavior.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(DamageApplicationSystem))]
    public partial struct ProjectileDamageSystem : ISystem
    {
        private EntityStorageInfoLookup _entityLookup;
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<Damageable> _damageableLookup;
        private BufferLookup<DamageEvent> _damageBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ProjectileActive>();
            _entityLookup = state.GetEntityStorageInfoLookup();
            _healthLookup = state.GetComponentLookup<Health>(true);
            _damageableLookup = state.GetComponentLookup<Damageable>(true);
            _damageBufferLookup = state.GetBufferLookup<DamageEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Get projectile catalog for damage lookups
            if (!SystemAPI.TryGetSingleton<ProjectileCatalog>(out var projectileCatalog))
            {
                return;
            }

            var poolingEnabled = SystemAPI.TryGetSingleton<ProjectilePoolConfig>(out var poolConfig) &&
                                 poolConfig.Capacity > 0 &&
                                 poolConfig.Prefab != Entity.Null;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            _entityLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _damageableLookup.Update(ref state);
            _damageBufferLookup.Update(ref state);

            var jobHandle = new ProcessProjectileImpactsJob
            {
                CurrentTick = currentTick,
                Ecb = ecb,
                PoolingEnabled = poolingEnabled,
                EntityLookup = _entityLookup,
                HealthLookup = _healthLookup,
                DamageableLookup = _damageableLookup,
                DamageBuffers = _damageBufferLookup,
                ProjectileCatalog = projectileCatalog.Catalog
            }.ScheduleParallel(state.Dependency);

            state.Dependency = jobHandle;
        }

        [BurstCompile]
        public partial struct ProcessProjectileImpactsJob : IJobEntity
        {
            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            public bool PoolingEnabled;
            [ReadOnly] public EntityStorageInfoLookup EntityLookup;
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<Damageable> DamageableLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<DamageEvent> DamageBuffers;
            [ReadOnly] public BlobAssetReference<ProjectileCatalogBlob> ProjectileCatalog;

            void Execute(
                Entity projectileEntity,
                [EntityIndexInQuery] int entityInQueryIndex,
                ref ProjectileEntity projectile,
                ref DynamicBuffer<DamageEvent> damageEvents,
                EnabledRefRW<ProjectileActive> active,
                EnabledRefRW<ProjectileRecycleTag> recycleTag)
            {
                if (!active.ValueRO)
                {
                    return;
                }

                // Check if projectile has hit its target
                if (projectile.TargetEntity == Entity.Null || !EntityLookup.Exists(projectile.TargetEntity))
                {
                    // No target or target destroyed - retire projectile
                    RetireProjectile(entityInQueryIndex, projectileEntity, ref projectile, ref active, ref recycleTag);
                    return;
                }

                // Check if target is damageable
                if (!HealthLookup.HasComponent(projectile.TargetEntity) &&
                    !DamageableLookup.HasComponent(projectile.TargetEntity))
                {
                    // Target not damageable - retire projectile
                    RetireProjectile(entityInQueryIndex, projectileEntity, ref projectile, ref active, ref recycleTag);
                    return;
                }

                // Look up projectile damage from catalog (game-agnostic: games define specs)
                float damage = 10f; // Default fallback
                DamageType damageType = DamageType.Physical;
                DamageFlags damageFlags = DamageFlags.None;

                ref var spec = ref FindProjectileSpec(ProjectileCatalog, projectile.ProjectileId);
                if (!UnsafeRef.IsNull(ref spec))
                {
                    damage = spec.Damage.BaseDamage;
                    damageType = DetermineDamageTypeFromSpec(ref spec);
                    damageFlags = DetermineDamageFlagsFromSpec(ref spec);
                }

                // Create damage event
                var damageEvent = new DamageEvent
                {
                    SourceEntity = projectile.SourceEntity,
                    TargetEntity = projectile.TargetEntity,
                    RawDamage = damage,
                    Type = damageType,
                    Tick = CurrentTick,
                    Flags = damageFlags
                };

                // Add damage event to target
                if (DamageBuffers.HasBuffer(projectile.TargetEntity))
                {
                    var targetDamageEvents = DamageBuffers[projectile.TargetEntity];
                    targetDamageEvents.Add(damageEvent);
                }
                else
                {
                    // Create buffer if it doesn't exist
                    Ecb.AddBuffer<DamageEvent>(entityInQueryIndex, projectile.TargetEntity);
                }

                // Handle pierce mechanics
                projectile.HitsLeft--;
                if (projectile.HitsLeft <= 0)
                {
                    // Projectile exhausted - retire it
                    RetireProjectile(entityInQueryIndex, projectileEntity, ref projectile, ref active, ref recycleTag);
                }
                else
                {
                    // Projectile continues - clear target for next impact detection
                    projectile.TargetEntity = Entity.Null;
                }
            }

            private void RetireProjectile(
                int entityInQueryIndex,
                Entity projectileEntity,
                ref ProjectileEntity projectile,
                ref EnabledRefRW<ProjectileActive> active,
                ref EnabledRefRW<ProjectileRecycleTag> recycleTag)
            {
                if (PoolingEnabled)
                {
                    active.ValueRW = false;
                    recycleTag.ValueRW = true;
                    projectile.TargetEntity = Entity.Null;
                    projectile.Velocity = float3.zero;
                    projectile.HitsLeft = 0f;
                    return;
                }

                Ecb.DestroyEntity(entityInQueryIndex, projectileEntity);
            }

            private static ref ProjectileSpec FindProjectileSpec(
                BlobAssetReference<ProjectileCatalogBlob> catalog,
                FixedString64Bytes projectileId)
            {
                if (!catalog.IsCreated)
                {
                    return ref UnsafeRef.Null<ProjectileSpec>();
                }

                ref var catalogRef = ref catalog.Value;
                for (int i = 0; i < catalogRef.Projectiles.Length; i++)
                {
                    ref var projectileSpec = ref catalogRef.Projectiles[i];
                    if (projectileSpec.Id.Equals(projectileId))
                    {
                        return ref projectileSpec;
                    }
                }

                return ref UnsafeRef.Null<ProjectileSpec>();
            }

            /// <summary>
            /// Determines damage type from projectile spec.
            /// Games define projectile kinds; PureDOTS maps them to damage types.
            /// </summary>
            private static DamageType DetermineDamageTypeFromSpec(ref ProjectileSpec spec)
            {
                // Map projectile kind to damage type
                // Games can extend this by adding DamageType field to DamageModel
                var kind = (ProjectileKind)spec.Kind;
                return kind switch
                {
                    ProjectileKind.Beam => DamageType.Fire, // Energy weapons
                    ProjectileKind.Homing => DamageType.Physical, // Missiles
                    ProjectileKind.Ballistic => DamageType.Physical, // Kinetic
                    _ => DamageType.Physical
                };
            }

            /// <summary>
            /// Determines damage flags from projectile spec.
            /// </summary>
            private static DamageFlags DetermineDamageFlagsFromSpec(ref ProjectileSpec spec)
            {
                var flags = DamageFlags.None;

                // Pierce flag if projectile has pierce capability
                if (spec.Pierce > 0)
                {
                    flags |= DamageFlags.Pierce;
                }

                // Check OnHit effects for special flags
                if (spec.OnHit.Length > 0)
                {
                    for (int i = 0; i < spec.OnHit.Length; i++)
                    {
                        var effect = spec.OnHit[i];
                        if (effect.Kind == EffectOpKind.AoE)
                        {
                            flags |= DamageFlags.AoE;
                        }
                    }
                }

                return flags;
            }
        }
    }
}

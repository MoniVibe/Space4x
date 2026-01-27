using PureDOTS.Runtime.Buffs;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.LowLevel;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Processes projectile hit results and applies effect operations (Damage, AoE, Chain, Pierce, Status).
    /// Runs after ProjectileCollisionSystem in CombatSystemGroup.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(ProjectileCollisionSystem))]
    [UpdateBefore(typeof(DamageApplicationSystem))]
    public partial struct ProjectileEffectExecutionSystem : ISystem
    {
        private BufferLookup<DamageEvent> _damageBufferLookup;
        private BufferLookup<BuffApplicationRequest> _buffRequestBufferLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileEntity>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ProjectileActive>();
            _damageBufferLookup = state.GetBufferLookup<DamageEvent>();
            _buffRequestBufferLookup = state.GetBufferLookup<BuffApplicationRequest>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ProjectileCatalog>(out var projectileCatalog))
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Optional: get spatial grid for AoE/Chain queries
            var hasSpatialGrid = SystemAPI.TryGetSingleton<SpatialGridConfig>(out var spatialConfig) &&
                                 SystemAPI.TryGetSingleton<SpatialGridState>(out var spatialState);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Optional: get physics world for AoE overlap queries
            var hasPhysicsWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld);

            _damageBufferLookup.Update(ref state);
            _buffRequestBufferLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var poolingEnabled = SystemAPI.TryGetSingleton<ProjectilePoolConfig>(out var poolConfig) &&
                                 poolConfig.Capacity > 0 &&
                                 poolConfig.Prefab != Entity.Null;

            var job = new ProjectileEffectExecutionJob
            {
                ProjectileCatalog = projectileCatalog.Catalog,
                CurrentTick = currentTick,
                Ecb = ecb,
                PoolingEnabled = poolingEnabled,
                HasSpatialGrid = hasSpatialGrid,
                SpatialConfig = hasSpatialGrid ? spatialConfig : default,
                HasPhysicsWorld = hasPhysicsWorld,
                PhysicsWorld = hasPhysicsWorld ? physicsWorld : default,
                DamageBuffers = _damageBufferLookup,
                BuffRequestBuffers = _buffRequestBufferLookup,
                TransformLookup = _transformLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct ProjectileEffectExecutionJob : IJobEntity
        {
            [ReadOnly] public BlobAssetReference<ProjectileCatalogBlob> ProjectileCatalog;
            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            public bool PoolingEnabled;
            public bool HasSpatialGrid;
            [ReadOnly] public SpatialGridConfig SpatialConfig;
            public bool HasPhysicsWorld;
            [ReadOnly] public PhysicsWorldSingleton PhysicsWorld;
            [NativeDisableParallelForRestriction] public BufferLookup<DamageEvent> DamageBuffers;
            [NativeDisableParallelForRestriction] public BufferLookup<BuffApplicationRequest> BuffRequestBuffers;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity projectileEntity,
                ref ProjectileEntity projectile,
                DynamicBuffer<ProjectileHitResult> hitResults,
                EnabledRefRW<ProjectileActive> active,
                EnabledRefRW<ProjectileRecycleTag> recycleTag)
            {
                if (!active.ValueRO)
                {
                    return;
                }

                if (hitResults.Length == 0)
                {
                    return;
                }

                // Find projectile spec
                ref var spec = ref FindProjectileSpec(ProjectileCatalog, projectile.ProjectileId);
                if (UnsafeRef.IsNull(ref spec))
                {
                    return;
                }

                // Process each hit
                for (int hitIndex = 0; hitIndex < hitResults.Length; hitIndex++)
                {
                    var hit = hitResults[hitIndex];

                    // Skip invalid hits
                    if (hit.HitEntity == Entity.Null)
                    {
                        continue;
                    }

                    // Process all effect operations
                    for (int opIndex = 0; opIndex < spec.OnHit.Length; opIndex++)
                    {
                        var effectOp = spec.OnHit[opIndex];
                        ProcessEffectOp(
                            chunkIndex,
                            projectileEntity,
                            ref projectile,
                            ref spec,
                            effectOp,
                            hit,
                            chunkIndex);
                    }

                    // Decrement pierce count
                    projectile.HitsLeft -= 1f;
                    if (projectile.HitsLeft <= 0f)
                    {
                        // Projectile exhausted - retire it
                        RetireProjectile(chunkIndex, projectileEntity, ref projectile, ref active, ref recycleTag);
                        return;
                    }
                }

                // Clear hit results after processing
                hitResults.Clear();
            }

            private void RetireProjectile(
                int chunkIndex,
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

                Ecb.DestroyEntity(chunkIndex, projectileEntity);
            }

            private void ProcessEffectOp(
                int chunkIndex,
                Entity projectileEntity,
                ref ProjectileEntity projectile,
                ref ProjectileSpec spec,
                EffectOp effectOp,
                ProjectileHitResult hit,
                int entityInQueryIndex)
            {
                switch (effectOp.Kind)
                {
                    case EffectOpKind.Damage:
                        ApplyDamage(chunkIndex, projectile, ref spec, effectOp, hit);
                        break;

                    case EffectOpKind.AoE:
                        ApplyAoE(chunkIndex, projectile, ref spec, effectOp, hit);
                        break;

                    case EffectOpKind.Chain:
                        ApplyChain(chunkIndex, projectile, ref spec, effectOp, hit);
                        break;

                    case EffectOpKind.Status:
                        ApplyStatus(chunkIndex, projectile, effectOp, hit);
                        break;

                    case EffectOpKind.Knockback:
                        ApplyKnockback(chunkIndex, projectile, effectOp, hit);
                        break;

                    case EffectOpKind.SpawnSub:
                        ApplySpawnSub(chunkIndex, projectile, ref spec, effectOp, hit);
                        break;
                }
            }

            private void ApplyDamage(
                int chunkIndex,
                ProjectileEntity projectile,
                ref ProjectileSpec spec,
                EffectOp effectOp,
                ProjectileHitResult hit)
            {
                // Calculate base damage from spec and effect magnitude
                float baseDamage = spec.Damage.BaseDamage * effectOp.Magnitude;

                // Apply deterministic damage variation
                float damageMultiplier = CalculateDamageMultiplier(
                    projectile.Seed,
                    projectile.PelletIndex,
                    hit.HitEntity.Index, // Use entity index as target ID for deterministic rolls
                    spec.Damage);

                float finalDamage = baseDamage * damageMultiplier;

                // Check for critical hits
                bool isCritical = CalculateCriticalHit(
                    projectile.Seed,
                    projectile.PelletIndex,
                    hit.HitEntity.Index,
                    0.1f); // 10% crit chance - could be made configurable

                if (isCritical)
                {
                    finalDamage *= 2.0f; // 2x damage on crit - could be made configurable
                }

                // Add damage event to target's buffer
                if (DamageBuffers.HasBuffer(hit.HitEntity))
                {
                    var damageEvents = DamageBuffers[hit.HitEntity];
                    damageEvents.Add(new DamageEvent
                    {
                        SourceEntity = projectile.SourceEntity,
                        TargetEntity = hit.HitEntity,
                        RawDamage = finalDamage,
                        Type = DamageType.Physical, // Could be extended based on effectOp
                        Tick = CurrentTick,
                        Flags = (isCritical ? DamageFlags.Critical : DamageFlags.None) | DamageFlags.Pierce
                    });
                }
                else
                {
                    // Create buffer if it doesn't exist
                    Ecb.AddBuffer<DamageEvent>(chunkIndex, hit.HitEntity);
                }
            }

            private void ApplyAoE(
                int chunkIndex,
                ProjectileEntity projectile,
                ref ProjectileSpec spec,
                EffectOp effectOp,
                ProjectileHitResult hit)
            {
                float aoeRadius = effectOp.Aux > 0f ? effectOp.Aux : spec.AoERadius;
                if (aoeRadius <= 0f)
                {
                    return;
                }

                // Collect entities in AoE radius
                var aoeTargets = new NativeList<Entity>(32, Allocator.Temp);

                if (HasPhysicsWorld)
                {
                    // Use physics overlap sphere
                    var hits = new NativeList<DistanceHit>(32, Allocator.Temp);
                    PhysicsWorld.OverlapSphere(
                        hit.HitPosition,
                        aoeRadius,
                        ref hits,
                        new CollisionFilter
                        {
                            BelongsTo = 0xFFFFFFFF,
                            CollidesWith = spec.HitFilter,
                            GroupIndex = 0
                        });

                    for (int i = 0; i < hits.Length; i++)
                    {
                        var target = hits[i].Entity;
                        if (target != projectile.SourceEntity && target != hit.HitEntity)
                        {
                            aoeTargets.Add(target);
                        }
                    }

                    hits.Dispose();
                }
                // Spatial grid path omitted in the burst job to avoid SystemAPI access

                // Apply damage to all entities in AoE
                for (int i = 0; i < aoeTargets.Length; i++)
                {
                    var target = aoeTargets[i];
                    if (target == Entity.Null || target == hit.HitEntity)
                    {
                        continue; // Skip primary hit (already processed)
                    }

                    // Get target position for distance calculation
                    float3 targetPos = float3.zero;
                    if (TransformLookup.HasComponent(target))
                    {
                        targetPos = TransformLookup[target].Position;
                    }
                    else
                    {
                        continue; // Skip if no transform
                    }

                    // Calculate distance-based falloff
                    float distance = math.distance(hit.HitPosition, targetPos);
                    float falloff = math.saturate(1f - (distance / aoeRadius));
                    float baseAoEDamage = spec.Damage.BaseDamage * effectOp.Magnitude * falloff;

                    // Apply deterministic damage variation for AoE
                    float aoeDamageMultiplier = CalculateDamageMultiplier(
                        projectile.Seed,
                        projectile.PelletIndex,
                        target.Index,
                        spec.Damage);

                    float finalAoEDamage = baseAoEDamage * aoeDamageMultiplier;

                    // Check for critical hits on AoE targets
                    bool isAoECritical = CalculateCriticalHit(
                        projectile.Seed,
                        projectile.PelletIndex,
                        target.Index,
                        0.05f); // Lower crit chance for AoE (5%)

                    if (isAoECritical)
                    {
                        finalAoEDamage *= 1.5f; // 1.5x damage on AoE crit
                    }

                    // Add damage event to target's buffer
                    if (DamageBuffers.HasBuffer(target))
                    {
                        var damageEvents = DamageBuffers[target];
                        damageEvents.Add(new DamageEvent
                        {
                            SourceEntity = projectile.SourceEntity,
                            TargetEntity = target,
                            RawDamage = finalAoEDamage,
                            Type = DamageType.Physical,
                            Tick = CurrentTick,
                            Flags = (isAoECritical ? DamageFlags.Critical : DamageFlags.None) | DamageFlags.AoE | DamageFlags.Pierce
                        });
                    }
                    else
                    {
                        Ecb.AddBuffer<DamageEvent>(chunkIndex, target);
                    }
                }

                aoeTargets.Dispose();
            }

            private void ApplyChain(
                int chunkIndex,
                ProjectileEntity projectile,
                ref ProjectileSpec spec,
                EffectOp effectOp,
                ProjectileHitResult hit)
            {
                float chainRange = effectOp.Aux > 0f ? effectOp.Aux : spec.ChainRange;
                if (chainRange <= 0f)
                {
                    return;
                }

                int chainCount = (int)effectOp.Magnitude;
                if (chainCount <= 0)
                {
                    chainCount = 1;
                }

                // Find next target within chain range
                // Use spatial grid or physics query
                Entity currentTarget = hit.HitEntity;
                var chainedTargets = new NativeList<Entity>(chainCount, Allocator.Temp);
                chainedTargets.Add(currentTarget);

                for (int chainIndex = 0; chainIndex < chainCount; chainIndex++)
                {
                    Entity nextTarget = FindNextChainTarget(
                        currentTarget,
                        chainedTargets,
                        chainRange,
                        spec.HitFilter);

                    if (nextTarget == Entity.Null)
                    {
                        break; // No more valid targets
                    }

                    chainedTargets.Add(nextTarget);
                    currentTarget = nextTarget;

                    // Apply chain damage (reduced per hop)
                    float baseChainDamage = spec.Damage.BaseDamage * effectOp.Magnitude * math.pow(0.7f, chainIndex + 1);

                    // Apply deterministic damage variation for chain
                    float chainDamageMultiplier = CalculateDamageMultiplier(
                        projectile.Seed,
                        projectile.PelletIndex + chainIndex + 1, // Offset pellet index for chain hops
                        nextTarget.Index,
                        spec.Damage);

                    float finalChainDamage = baseChainDamage * chainDamageMultiplier;

                    // Check for critical hits on chain targets
                    bool isChainCritical = CalculateCriticalHit(
                        projectile.Seed,
                        projectile.PelletIndex + chainIndex + 1,
                        nextTarget.Index,
                        0.08f); // Moderate crit chance for chain (8%)

                    if (isChainCritical)
                    {
                        finalChainDamage *= 1.75f; // 1.75x damage on chain crit
                    }

                    // Add damage event to target's buffer
                    if (DamageBuffers.HasBuffer(nextTarget))
                    {
                        var damageEvents = DamageBuffers[nextTarget];
                        damageEvents.Add(new DamageEvent
                        {
                            SourceEntity = projectile.SourceEntity,
                            TargetEntity = nextTarget,
                            RawDamage = finalChainDamage,
                            Type = DamageType.Physical,
                            Tick = CurrentTick,
                            Flags = (isChainCritical ? DamageFlags.Critical : DamageFlags.None) | DamageFlags.Chain | DamageFlags.Pierce
                        });
                    }
                    else
                    {
                        Ecb.AddBuffer<DamageEvent>(chunkIndex, nextTarget);
                    }
                }

                chainedTargets.Dispose();
            }

            private void ApplyStatus(
                int chunkIndex,
                ProjectileEntity projectile,
                EffectOp effectOp,
                ProjectileHitResult hit)
            {
                if (effectOp.StatusId == 0)
                {
                    return; // Invalid status ID
                }

                // Create buff ID from status ID (placeholder numeric ID; map via catalog when available)
                var buffId = default(FixedString64Bytes);
                buffId.Append(effectOp.StatusId);

                // Add buff application request to target's buffer
                if (BuffRequestBuffers.HasBuffer(hit.HitEntity))
                {
                    var buffRequests = BuffRequestBuffers[hit.HitEntity];
                    buffRequests.Add(new BuffApplicationRequest
                    {
                        BuffId = buffId,
                        SourceEntity = projectile.SourceEntity,
                        DurationOverride = effectOp.Duration > 0f ? effectOp.Duration : 0f,
                        StacksToApply = 1
                    });
                }
                else
                {
                    Ecb.AddBuffer<BuffApplicationRequest>(chunkIndex, hit.HitEntity);
                }
            }

            private void ApplyKnockback(
                int chunkIndex,
                ProjectileEntity projectile,
                EffectOp effectOp,
                ProjectileHitResult hit)
            {
                // Apply knockback force
                // Would require Velocity component or similar
                // Placeholder for now
            }

            private void ApplySpawnSub(
                int chunkIndex,
                ProjectileEntity projectile,
                ref ProjectileSpec spec,
                EffectOp effectOp,
                ProjectileHitResult hit)
            {
                // Spawn sub-projectiles
                // Would create new ProjectileEntity with different spec
                // Placeholder for now
            }

            private Entity FindNextChainTarget(
                Entity currentTarget,
                NativeList<Entity> excludeList,
                float range,
                uint hitFilter)
            {
                // Find nearest valid target within range, excluding already chained targets
                if (!TransformLookup.HasComponent(currentTarget))
                {
                    return Entity.Null;
                }

                float3 currentPos = TransformLookup[currentTarget].Position;
                Entity nearestTarget = Entity.Null;
                float nearestDistSq = range * range;

                // Use physics overlap sphere to find candidates
                if (HasPhysicsWorld)
                {
                    var hits = new NativeList<DistanceHit>(32, Allocator.Temp);
                    PhysicsWorld.OverlapSphere(
                        currentPos,
                        range,
                        ref hits,
                        new CollisionFilter
                        {
                            BelongsTo = 0xFFFFFFFF,
                            CollidesWith = hitFilter,
                            GroupIndex = 0
                        });

                    for (int i = 0; i < hits.Length; i++)
                    {
                        var candidate = hits[i].Entity;

                        // Exclude already chained targets
                        bool isExcluded = false;
                        for (int e = 0; e < excludeList.Length; e++)
                        {
                            if (excludeList[e] == candidate)
                            {
                                isExcluded = true;
                                break;
                            }
                        }

                        if (isExcluded || !TransformLookup.HasComponent(candidate))
                        {
                            continue;
                        }

                        float3 candidatePos = TransformLookup[candidate].Position;
                        float distSq = math.lengthsq(candidatePos - currentPos);
                        if (distSq < nearestDistSq)
                        {
                            nearestDistSq = distSq;
                            nearestTarget = candidate;
                        }
                    }

                    hits.Dispose();
                }

                return nearestTarget;
            }

            private static ref ProjectileSpec FindProjectileSpec(
                BlobAssetReference<ProjectileCatalogBlob> catalog,
                FixedString64Bytes projectileId)
            {
                if (!catalog.IsCreated)
                {
                    return ref UnsafeRef.Null<ProjectileSpec>();
                }

                ref var projectiles = ref catalog.Value.Projectiles;
                for (int i = 0; i < projectiles.Length; i++)
                {
                    ref var projectileSpec = ref projectiles[i];
                    if (projectileSpec.Id.Equals(projectileId))
                    {
                        return ref projectileSpec;
                    }
                }

                return ref UnsafeRef.Null<ProjectileSpec>();
            }

            /// <summary>
            /// Calculates deterministic damage multiplier using shot seed.
            /// </summary>
            private static float CalculateDamageMultiplier(uint shotSeed, int pelletIndex, int targetId, DamageModel damageModel)
            {
                // Use independent hash stream for damage rolls
                uint h = math.hash(new uint4(shotSeed, (uint)pelletIndex, (uint)targetId, 0xA2C79B3Du));
                float u = (h * (1.0f / 4294967296.0f)); // Convert uint to [0,1)

                // Apply damage variance (±20% by default)
                const float variance = 0.2f;
                return 1.0f + (u - 0.5f) * variance * 2.0f;
            }

            /// <summary>
            /// Calculates deterministic critical hit using shot seed.
            /// </summary>
            private static bool CalculateCriticalHit(uint shotSeed, int pelletIndex, int targetId, float critChance)
            {
                // Use different hash stream for critical hits
                uint h = math.hash(new uint4(shotSeed, (uint)pelletIndex, (uint)targetId, 0xC3A5C85Cu));
                float u = (h * (1.0f / 4294967296.0f)); // Convert uint to [0,1)
                return u < critChance;
            }
        }

    }
}

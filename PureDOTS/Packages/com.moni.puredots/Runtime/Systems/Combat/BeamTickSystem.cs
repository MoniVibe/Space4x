using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.LowLevel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Handles beam weapons - instant hitscan raycast each tick while firing.
    /// Beam weapons don't create projectile entities; they apply damage directly.
    /// Runs in CombatSystemGroup after weapon fire systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct BeamTickSystem : ISystem
    {
        private BufferLookup<DamageEvent> _damageBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponMount>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _damageBufferLookup = state.GetBufferLookup<DamageEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<WeaponCatalog>(out var weaponCatalog) ||
                !SystemAPI.TryGetSingleton<ProjectileCatalog>(out var projectileCatalog))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld))
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var deltaTime = timeState.FixedDeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            _damageBufferLookup.Update(ref state);

            var job = new BeamTickJob
            {
                WeaponCatalog = weaponCatalog.Catalog,
                ProjectileCatalog = projectileCatalog.Catalog,
                PhysicsWorld = physicsWorld,
                CurrentTick = currentTick,
                DeltaTime = deltaTime,
                Ecb = ecb,
                DamageBuffers = _damageBufferLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct BeamTickJob : IJobEntity
        {
            [ReadOnly] public BlobAssetReference<WeaponCatalogBlob> WeaponCatalog;
            [ReadOnly] public BlobAssetReference<ProjectileCatalogBlob> ProjectileCatalog;
            [ReadOnly] public PhysicsWorldSingleton PhysicsWorld;
            public uint CurrentTick;
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [NativeDisableParallelForRestriction] public BufferLookup<DamageEvent> DamageBuffers;

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity weaponEntity,
                ref WeaponMount weaponMount,
                in TurretState turretState)
            {
                // Only process if weapon is firing
                if (!weaponMount.IsFiring)
                {
                    return;
                }

                // Find weapon spec
                ref var weaponSpec = ref FindWeaponSpec(WeaponCatalog, weaponMount.WeaponId);
                if (UnsafeRef.IsNull(ref weaponSpec))
                {
                    return;
                }

                // Find projectile spec
                ref var projectileSpec = ref FindProjectileSpec(ProjectileCatalog, weaponSpec.ProjectileId);
                if (UnsafeRef.IsNull(ref projectileSpec))
                {
                    return;
                }

                // Only process beam projectiles
                if ((ProjectileKind)projectileSpec.Kind != ProjectileKind.Beam)
                {
                    return;
                }

                // Beam range (use lifetime as range if speed is 0)
                float beamRange = projectileSpec.Speed > 0f ? projectileSpec.Speed * projectileSpec.Lifetime : projectileSpec.Lifetime * 1000f; // Default 1000 m/s if speed is 0

                // Perform hitscan raycast from muzzle
                float3 startPos = turretState.MuzzlePosition;
                float3 direction = turretState.MuzzleForward;
                float3 endPos = startPos + direction * beamRange;

                var filter = new CollisionFilter
                {
                    BelongsTo = 0xFFFFFFFF,
                    CollidesWith = projectileSpec.HitFilter,
                    GroupIndex = 0
                };

                var raycastInput = new RaycastInput
                {
                    Start = startPos,
                    End = endPos,
                    Filter = filter
                };

                if (PhysicsWorld.CastRay(raycastInput, out var hit))
                {
                    // Apply tick damage
                    float tickDamage = projectileSpec.Damage.BaseDamage * DeltaTime; // Damage per second

                    if (DamageBuffers.HasBuffer(hit.Entity))
                    {
                        var damageEvents = DamageBuffers[hit.Entity];
                        damageEvents.Add(new DamageEvent
                        {
                            SourceEntity = weaponEntity,
                            TargetEntity = hit.Entity,
                            RawDamage = tickDamage,
                            Type = DamageType.Physical,
                            Tick = CurrentTick,
                            Flags = DamageFlags.Pierce
                        });
                    }
                    else
                    {
                        Ecb.AddBuffer<DamageEvent>(chunkIndex, hit.Entity);
                    }

                    // Request beam visual (would be handled by presentation system)
                    // For now, this is a placeholder - presentation integration comes later
                }
            }

            private static ref WeaponSpec FindWeaponSpec(
                BlobAssetReference<WeaponCatalogBlob> catalog,
                FixedString64Bytes weaponId)
            {
                if (!catalog.IsCreated)
                {
                    return ref UnsafeRef.Null<WeaponSpec>();
                }

                ref var weapons = ref catalog.Value.Weapons;
                for (int i = 0; i < weapons.Length; i++)
                {
                    ref var spec = ref weapons[i];
                    if (spec.Id.Equals(weaponId))
                    {
                        return ref spec;
                    }
                }

                return ref UnsafeRef.Null<WeaponSpec>();
            }

            private static ref ProjectileSpec FindProjectileSpec(
                BlobAssetReference<ProjectileCatalogBlob> catalog,
                FixedString32Bytes projectileId)
            {
                if (!catalog.IsCreated)
                {
                    return ref UnsafeRef.Null<ProjectileSpec>();
                }

                ref var projectiles = ref catalog.Value.Projectiles;
                for (int i = 0; i < projectiles.Length; i++)
                {
                    ref var spec = ref projectiles[i];
                    if (spec.Id.Equals(projectileId))
                    {
                        return ref spec;
                    }
                }

                return ref UnsafeRef.Null<ProjectileSpec>();
            }
        }
    }
}


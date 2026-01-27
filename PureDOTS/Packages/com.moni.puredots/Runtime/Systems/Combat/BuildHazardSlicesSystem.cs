using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.LowLevel;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectileKind = PureDOTS.Runtime.Combat.ProjectileKind;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Builds HazardSlice entries from projectile states and specs.
    /// Predicts danger envelopes for the next LookaheadSecMax seconds.
    /// Extracts contagion probability, spray variance, and team mask from specs.
    /// Games define projectile specs; PureDOTS executes hazard prediction.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(ProjectileEffectExecutionSystem))]
    public partial struct BuildHazardSlicesSystem : ISystem
    {
        private EntityQuery _hazardSliceBufferQuery;
        private ComponentLookup<FactionId> _factionLookup;
        private ComponentLookup<WeaponMount> _weaponMountLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ProjectileEntity>();
            state.RequireForUpdate<ProjectileActive>();

            _hazardSliceBufferQuery = SystemAPI.QueryBuilder()
                .WithAll<HazardSlice>()
                .Build();

            _factionLookup = state.GetComponentLookup<FactionId>(true);
            _weaponMountLookup = state.GetComponentLookup<WeaponMount>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ProjectileCatalog>(out var projectileCatalog))
            {
                return;
            }
            if (!projectileCatalog.Catalog.IsCreated)
            {
                return;
            }

            // Get weapon catalog for spray variance lookup
            BlobAssetReference<WeaponCatalogBlob> weaponCatalog = default;
            if (SystemAPI.TryGetSingleton<WeaponCatalog>(out var wc))
            {
                weaponCatalog = wc.Catalog;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var deltaTime = timeState.DeltaTime;

            // Find or create hazard slice buffer singleton
            Entity sliceBufferEntity;
            if (!SystemAPI.TryGetSingletonEntity<HazardSliceBuffer>(out sliceBufferEntity))
            {
                sliceBufferEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<HazardSliceBuffer>(sliceBufferEntity);
                state.EntityManager.AddBuffer<HazardSlice>(sliceBufferEntity);
            }

            var sliceBuffer = SystemAPI.GetBuffer<HazardSlice>(sliceBufferEntity);
            sliceBuffer.Clear();

            // Find maximum lookahead from all avoidance profiles
            float maxLookaheadSec = 5f; // Default
            foreach (var profile in SystemAPI.Query<RefRO<AvoidanceProfile>>())
            {
                maxLookaheadSec = math.max(maxLookaheadSec, profile.ValueRO.LookaheadSec);
            }

            uint lookaheadTicks = (uint)math.ceil(maxLookaheadSec / deltaTime);

            // Update lookups
            _factionLookup.Update(ref state);
            _weaponMountLookup.Update(ref state);

            // Use a temporary native list per chunk to collect slices
            // We'll use a single-threaded approach for now to avoid parallel write conflicts
            var tempSlices = new NativeList<HazardSlice>(Allocator.TempJob);

            var job = new BuildHazardSlicesJob
            {
                ProjectileCatalog = projectileCatalog.Catalog,
                WeaponCatalog = weaponCatalog,
                FactionLookup = _factionLookup,
                WeaponMountLookup = _weaponMountLookup,
                CurrentTick = currentTick,
                LookaheadTicks = lookaheadTicks,
                DeltaTime = deltaTime,
                TempSlices = tempSlices
            };

            // Run single-threaded to avoid parallel write conflicts
            state.Dependency = job.Schedule(state.Dependency);
            state.Dependency.Complete();

            // Copy temp slices to buffer
            sliceBuffer.Clear();
            for (int i = 0; i < tempSlices.Length; i++)
            {
                sliceBuffer.Add(tempSlices[i]);
            }

            tempSlices.Dispose();
        }

        [BurstCompile]
        public partial struct BuildHazardSlicesJob : IJobEntity
        {
            [ReadOnly] public BlobAssetReference<ProjectileCatalogBlob> ProjectileCatalog;
            [ReadOnly] public BlobAssetReference<WeaponCatalogBlob> WeaponCatalog;
            [ReadOnly] public ComponentLookup<FactionId> FactionLookup;
            [ReadOnly] public ComponentLookup<WeaponMount> WeaponMountLookup;
            public uint CurrentTick;
            public uint LookaheadTicks;
            public float DeltaTime;
            [NativeDisableParallelForRestriction] public NativeList<HazardSlice> TempSlices;

            void Execute(
                [EntityIndexInQuery] int entityInQueryIndex,
                Entity entity,
                in ProjectileEntity projectile,
                in LocalTransform transform,
                in VelocitySample velocity,
                EnabledRefRO<ProjectileActive> active)
            {
                if (!active.ValueRO)
                {
                    return;
                }

                // Find projectile spec
                ref var spec = ref FindProjectileSpec(ProjectileCatalog, projectile.ProjectileId);
                if (UnsafeRef.IsNull(ref spec))
                {
                    return;
                }

                float3 pos = transform.Position;
                float3 vel = projectile.Velocity;
                float speed = math.length(vel);

                if (speed < 1e-6f)
                {
                    return; // Stationary projectiles don't create hazards
                }

                // Determine hazard kind from projectile spec
                HazardKind kind = 0;
                if (spec.AoERadius > 0f)
                {
                    kind |= HazardKind.AoE;
                }
                if (spec.ChainRange > 0f)
                {
                    kind |= HazardKind.Chain;
                }
                if ((ProjectileKind)spec.Kind == ProjectileKind.Homing)
                {
                    kind |= HazardKind.Homing;
                }

                // Extract contagion probability from OnHit effects (game-defined in spec)
                float contagionProb = ExtractContagionProbability(ref spec);

                // Check for plague/spray hazard kinds
                if (contagionProb > 0f)
                {
                    kind |= HazardKind.Plague;
                }

                // Extract spray variance from weapon spec (game-defined)
                float sprayVariance = ExtractSprayVariance(projectile.SourceEntity);

                if (sprayVariance > 0f)
                {
                    kind |= HazardKind.Spray;
                }

                // Extract team mask from source entity's faction (game-defined)
                uint teamMask = ExtractTeamMask(projectile.SourceEntity);

                // Predict trajectory for lookahead period
                float lifetime = spec.Lifetime;
                float remainingLifetime = lifetime - projectile.Age;
                float predictionTime = math.min(remainingLifetime, LookaheadTicks * DeltaTime);

                if (predictionTime <= 0f)
                {
                    return;
                }

                // Create segment slice (current position to predicted end)
                var segmentSlice = new HazardSlice
                {
                    Center = pos,
                    Vel = vel,
                    Radius0 = spec.AoERadius,
                    RadiusGrow = 0f, // No growth during flight
                    StartTick = CurrentTick,
                    EndTick = CurrentTick + (uint)math.ceil(predictionTime / DeltaTime),
                    Kind = kind,
                    ChainRadius = spec.ChainRange,
                    ContagionProb = contagionProb,
                    HomingConeCos = (ProjectileKind)spec.Kind == ProjectileKind.Homing ? math.cos(math.radians(45f)) : 0f,
                    SprayVariance = sprayVariance,
                    TeamMask = teamMask,
                    Seed = projectile.Seed
                };

                // Add segment slice
                TempSlices.Add(segmentSlice);

                // If AoE projectile, create impact slice
                if (spec.AoERadius > 0f && remainingLifetime > 0f)
                {
                    float3 impactPos = pos + vel * remainingLifetime;
                    float impactRadius = spec.AoERadius;
                    float blastRadius = impactRadius * 2f; // Blast wave expands to 2x radius

                    var impactSlice = new HazardSlice
                    {
                        Center = impactPos,
                        Vel = float3.zero, // Stationary blast
                        Radius0 = impactRadius,
                        RadiusGrow = (blastRadius - impactRadius) / 0.1f, // Expand over 0.1 seconds
                        StartTick = CurrentTick + (uint)math.ceil(remainingLifetime / DeltaTime),
                        EndTick = CurrentTick + (uint)math.ceil((remainingLifetime + 0.5f) / DeltaTime), // 0.5s blast duration
                        Kind = HazardKind.AoE,
                        ChainRadius = 0f,
                        ContagionProb = contagionProb, // Carry over contagion to impact
                        HomingConeCos = 0f,
                        SprayVariance = 0f,
                        TeamMask = teamMask,
                        Seed = projectile.Seed
                    };

                    // Add impact slice
                    TempSlices.Add(impactSlice);
                }
            }

            /// <summary>
            /// Extracts contagion probability from projectile OnHit effects.
            /// Games define Status effects with contagion in ProjectileSpec.OnHit.
            /// </summary>
            private static float ExtractContagionProbability(ref ProjectileSpec spec)
            {
                if (!spec.OnHit.Length.Equals(0))
                {
                    for (int i = 0; i < spec.OnHit.Length; i++)
                    {
                        var effect = spec.OnHit[i];
                        // Status effects with non-zero Aux are treated as contagion effects
                        // Games define contagion probability in EffectOp.Aux for Status effects
                        if (effect.Kind == EffectOpKind.Status && effect.Aux > 0f)
                        {
                            return effect.Aux; // Aux contains contagion probability (0-1)
                        }
                    }
                }
                return 0f;
            }

            /// <summary>
            /// Extracts spray variance from weapon that fired the projectile.
            /// Games define spread angle in WeaponSpec.SpreadDeg.
            /// </summary>
            private float ExtractSprayVariance(Entity sourceEntity)
            {
                if (sourceEntity == Entity.Null)
                {
                    return 0f;
                }

                // Look up weapon mount on source entity
                if (WeaponMountLookup.HasComponent(sourceEntity))
                {
                    var weaponMount = WeaponMountLookup[sourceEntity];

                    // Look up weapon spec from catalog
                    if (WeaponCatalog.IsCreated)
                    {
                        ref var catalog = ref WeaponCatalog.Value;
                        for (int i = 0; i < catalog.Weapons.Length; i++)
                        {
                            ref var weaponSpec = ref catalog.Weapons[i];
                            if (weaponSpec.Id.Equals(weaponMount.WeaponId))
                            {
                                // Convert spread degrees to radians
                                return math.radians(weaponSpec.SpreadDeg);
                            }
                        }
                    }
                }

                return 0f;
            }

            /// <summary>
            /// Extracts team mask from source entity's faction.
            /// Games define faction relationships; PureDOTS uses mask for friendly fire exclusion.
            /// </summary>
            private uint ExtractTeamMask(Entity sourceEntity)
            {
                if (sourceEntity == Entity.Null)
                {
                    return 0xFFFFFFFF; // No source = hits everyone
                }

                // Look up faction on source entity
                if (FactionLookup.HasComponent(sourceEntity))
                {
                    var faction = FactionLookup[sourceEntity];

                    // Create mask that excludes same faction
                    // Bit N is set if faction N is hostile to this faction
                    // For simplicity: same faction bit is 0 (friendly), all others are 1 (hostile)
                    uint factionBit = 1u << (faction.Value % 32);
                    return ~factionBit; // All bits except own faction
                }

                return 0xFFFFFFFF; // No faction = hits everyone
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
        }
    }

    /// <summary>
    /// Tag component marking the entity that holds the hazard slice buffer.
    /// </summary>
    public struct HazardSliceBuffer : IComponentData { }
}

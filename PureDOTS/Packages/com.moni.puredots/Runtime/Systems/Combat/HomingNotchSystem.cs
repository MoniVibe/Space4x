using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.LowLevel;
using PureDOTS.Runtime.Movement;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// For missiles in homing cone: computes lateral "notch" maneuver.
    /// V_lat = normalize(cross(vel_missile, up))
    /// Blends into V_avoid with weight = HomingRisk.
    /// Only applies to projectiles with Kind == Homing (verified via ProjectileCatalog).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AvoidanceSenseSystem))]
    public partial struct HomingNotchSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ProjectileActive>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Require projectile catalog for homing kind verification
            if (!SystemAPI.TryGetSingleton<ProjectileCatalog>(out var projectileCatalog))
            {
                return;
            }
            if (!projectileCatalog.Catalog.IsCreated)
            {
                return;
            }

            // Find homing projectiles
            var projectileQuery = SystemAPI.QueryBuilder()
                .WithAll<ProjectileEntity, LocalTransform>()
                .Build();

            if (projectileQuery.IsEmpty)
            {
                return;
            }

            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            transformLookup.Update(ref state);

            var job = new HomingNotchJob
            {
                TransformLookup = transformLookup,
                ProjectileCatalog = projectileCatalog.Catalog
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct HomingNotchJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public BlobAssetReference<ProjectileCatalogBlob> ProjectileCatalog;

            void Execute(
                Entity entity,
                ref HazardAvoidanceState avoidanceState,
                in ProjectileEntity projectile,
                in LocalTransform transform,
                EnabledRefRO<ProjectileActive> active)
            {
                if (!active.ValueRO)
                {
                    return;
                }

                // Verify projectile is homing type via catalog lookup (game-agnostic)
                ref var spec = ref FindProjectileSpec(ProjectileCatalog, projectile.ProjectileId);
                if (UnsafeRef.IsNull(ref spec))
                {
                    return;
                }

                // Only apply notch maneuver to homing projectiles
                if ((ProjectileKind)spec.Kind != ProjectileKind.Homing)
                {
                    return;
                }

                // Also require active target
                if (projectile.TargetEntity == Entity.Null)
                {
                    return;
                }

                // Get missile velocity
                float3 missileVel = projectile.Velocity;
                float velLength = math.length(missileVel);
                if (velLength < 1e-6f)
                {
                    return;
                }

                float3 missileDir = math.normalize(missileVel);

                // Compute lateral vector (perpendicular to velocity) using 3D-aware orthonormal basis
                // This works correctly regardless of missile orientation in 3D space
                OrientationHelpers.ComputeOrthonormalBasis(missileDir, OrientationHelpers.WorldUp, out float3 lateral, out _);
                float lateralLength = math.length(lateral);

                if (lateralLength > 1e-6f)
                {
                    lateral = math.normalize(lateral);

                    // Blend lateral notch into avoidance vector
                    // Weight based on homing risk and projectile turn rate (from spec)
                    float turnRateFactor = math.saturate(spec.TurnRateDeg / 180f); // Higher turn rate = harder to notch
                    float homingRisk = avoidanceState.AvoidanceUrgency * (0.5f + turnRateFactor * 0.3f);
                    float3 notchVector = lateral * homingRisk;

                    // Combine with existing avoidance
                    float3 combinedAvoidance = avoidanceState.CurrentAdjustment + notchVector;
                    float combinedLength = math.length(combinedAvoidance);

                    if (combinedLength > 1e-6f)
                    {
                        avoidanceState.CurrentAdjustment = math.normalize(combinedAvoidance) * math.min(combinedLength, 1f);
                    }
                }
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
}

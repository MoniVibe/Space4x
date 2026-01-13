using PureDOTS.Runtime;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems.Spatial;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Combat
{
    /// <summary>
    /// Space combat system for Space4X carriers/strike craft.
    /// Handles raycast-based combat between ships.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(SpatialGridBuildSystem))]
    public partial struct SpaceCombatSystem : ISystem
    {
        // Fallback query (used only if spatial singletons are missing)
        private EntityQuery _enemyQuery;

        // We only need the nearest target today; keep as const so tuning is easy later.
        private const int kNearestTargets = 1;

        private struct CombatTargetFilter : ISpatialQueryFilter
        {
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<PlatformTag> PlatformLookup;
            public Entity Excluded;

            public bool Accept(int descriptorIndex, in SpatialQueryDescriptor descriptor, in SpatialGridEntry entry)
            {
                var e = entry.Entity;
                if (e == Entity.Null || e == Excluded)
                {
                    return false;
                }

                if (!PlatformLookup.HasComponent(e) || !HealthLookup.HasComponent(e))
                {
                    return false;
                }

                // Optional: ignore dead targets (cheap + usually desirable).
                return HealthLookup[e].Current > 0f;
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ScenarioState>();

            _enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<Health, LocalTransform, PlatformTag>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var scenario = SystemAPI.GetSingleton<ScenarioState>();

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record || !scenario.EnableSpace4x)
            {
                return;
            }

            var currentTime = (float)timeState.WorldSeconds;

            // Prefer spatial grid (fast). Fall back if spatial config/state not present.
            var hasSpatial = SystemAPI.TryGetSingleton(out SpatialGridConfig spatialConfig)
                             && SystemAPI.TryGetSingleton(out SpatialGridState _);

            DynamicBuffer<SpatialGridCellRange> cellRanges = default;
            DynamicBuffer<SpatialGridEntry> gridEntries = default;

            if (hasSpatial)
            {
                var gridEntity = SystemAPI.GetSingletonEntity<SpatialGridConfig>();
                cellRanges = SystemAPI.GetBuffer<SpatialGridCellRange>(gridEntity);
                gridEntries = SystemAPI.GetBuffer<SpatialGridEntry>(gridEntity);
            }

            // Lookups
            var healthLookup = state.GetComponentLookup<Health>(false);       // RW (we apply damage)
            var healthLookupRO = state.GetComponentLookup<Health>(true);      // RO (for filter reads)
            var platformLookup = state.GetComponentLookup<PlatformTag>(true);
            var defenseLookup = state.GetComponentLookup<DefenseStats>(true);

            healthLookup.Update(ref state);
            healthLookupRO.Update(ref state);
            platformLookup.Update(ref state);
            defenseLookup.Update(ref state);

            // Scratch buffers allocated once per tick (no per-attacker allocs)
            var nearestResults = new NativeList<KNearestResult>(kNearestTargets, Allocator.Temp);

            // Fallback scratch arrays (built once per tick; still O(N) but avoids per-attacker allocs)
            NativeArray<Entity> enemyEntities = default;
            NativeArray<LocalTransform> enemyTransforms = default;
            if (!hasSpatial)
            {
                enemyEntities = _enemyQuery.ToEntityArray(Allocator.Temp);
                enemyTransforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            }

            // Query Space4X entities with AttackStats and PlatformTag
            foreach (var (attackStats, transform, entity) in SystemAPI.Query<
                         RefRW<AttackStats>,
                         RefRO<LocalTransform>>()
                     .WithAll<PlatformTag>()
                     .WithEntityAccess())
            {
                ref var attackStatsRef = ref attackStats.ValueRW;

                // Check cooldown
                if (currentTime - attackStatsRef.LastAttackTime < attackStatsRef.AttackCooldown)
                {
                    continue;
                }

                if (attackStatsRef.Range <= 0f)
                {
                    continue;
                }

                Entity nearestEnemy = Entity.Null;

                if (hasSpatial)
                {
                    // Spatial query: nearest target within Range. No global scans.
                    var pos = transform.ValueRO.Position;

                    var filter = new CombatTargetFilter
                    {
                        HealthLookup = healthLookupRO,
                        PlatformLookup = platformLookup,
                        Excluded = entity
                    };

                    SpatialQueryHelper.FindKNearestInRadius(
                        ref pos,
                        radius: attackStatsRef.Range,
                        k: kNearestTargets,
                        config: spatialConfig,
                        ranges: cellRanges,
                        entries: gridEntries,
                        results: ref nearestResults,
                        filter: filter);

                    if (nearestResults.Length > 0)
                    {
                        nearestEnemy = nearestResults[0].Entity;
                    }
                }
                else
                {
                    // Fallback: scan prebuilt arrays once-per-tick (no per-attacker allocations).
                    float bestDist = float.MaxValue;
                    var myPos = transform.ValueRO.Position;

                    for (int i = 0; i < enemyEntities.Length; i++)
                    {
                        var candidate = enemyEntities[i];
                        if (candidate == entity)
                        {
                            continue;
                        }

                        float distance = math.distance(myPos, enemyTransforms[i].Position);
                        if (distance <= attackStatsRef.Range && distance < bestDist)
                        {
                            bestDist = distance;
                            nearestEnemy = candidate;
                        }
                    }
                }

                // Attack if enemy found
                if (nearestEnemy != Entity.Null && healthLookup.HasComponent(nearestEnemy))
                {
                    var enemyHealth = healthLookup[nearestEnemy];
                    float damage = attackStatsRef.Damage;

                    // Apply defense if enemy has DefenseStats
                    if (defenseLookup.HasComponent(nearestEnemy))
                    {
                        var defense = defenseLookup[nearestEnemy];
                        damage = math.max(0f, damage - defense.Armor);
                    }

                    enemyHealth.Current = math.max(0f, enemyHealth.Current - damage);
                    healthLookup[nearestEnemy] = enemyHealth;

                    attackStatsRef.LastAttackTime = currentTime;
                }
            }

            if (enemyEntities.IsCreated)
            {
                enemyEntities.Dispose();
            }
            if (enemyTransforms.IsCreated)
            {
                enemyTransforms.Dispose();
            }
            nearestResults.Dispose();
        }
    }
}

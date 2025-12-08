using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Time;
using Space4X.Runtime;
using Space4X.Demo;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Combat
{
    /// <summary>
    /// Space combat system for Space4X carriers/strike craft.
    /// Handles raycast-based combat between ships.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SpaceCombatSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<DemoScenarioState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            var demoState = SystemAPI.GetSingleton<DemoScenarioState>();

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record || !demoState.EnableSpace4x)
            {
                return;
            }

            var currentTime = (float)timeState.WorldSeconds;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Query Space4X entities with Health, AttackStats, and PlatformTag
            foreach (var (health, attackStats, transform, entity) in SystemAPI.Query<
                    RefRW<Health>,
                    RefRW<AttackStats>,
                    RefRO<LocalTransform>>()
                .WithAll<PlatformTag>()
                .WithEntityAccess())
            {
                // Check cooldown
                ref var attackStatsRef = ref attackStats.ValueRW;
                if (currentTime - attackStatsRef.LastAttackTime < attackStatsRef.AttackCooldown)
                {
                    continue;
                }

                // Find nearest enemy within range
                Entity nearestEnemy = Entity.Null;
                float nearestDistance = float.MaxValue;
                float3 enemyPosition = float3.zero;

                var enemyQuery = state.GetEntityQuery(
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<PlatformTag>());
                var enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
                var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                for (int i = 0; i < enemyEntities.Length; i++)
                {
                    // Skip self
                    if (enemyEntities[i] == entity)
                    {
                        continue;
                    }

                    float distance = math.distance(transform.ValueRO.Position, enemyTransforms[i].Position);
                    if (distance <= attackStatsRef.Range && distance < nearestDistance)
                    {
                        nearestEnemy = enemyEntities[i];
                        nearestDistance = distance;
                        enemyPosition = enemyTransforms[i].Position;
                    }
                }

                enemyEntities.Dispose();
                enemyTransforms.Dispose();

                // Attack if enemy found (simple raycast/hitscan)
                if (nearestEnemy != Entity.Null && state.EntityManager.Exists(nearestEnemy))
                {
                    // Simple hitscan - for demo, just apply damage directly
                    // In full implementation, would use Unity Physics raycast
                    var enemyHealth = state.EntityManager.GetComponentData<Health>(nearestEnemy);
                    float damage = attackStatsRef.Damage;

                    // Apply defense if enemy has DefenseStats
                    if (state.EntityManager.HasComponent<DefenseStats>(nearestEnemy))
                    {
                        var defense = state.EntityManager.GetComponentData<DefenseStats>(nearestEnemy);
                        damage = math.max(0f, damage - defense.Armor);
                    }

                    enemyHealth.Current = math.max(0f, enemyHealth.Current - damage);
                    ecb.SetComponent(nearestEnemy, enemyHealth);

                    attackStatsRef.LastAttackTime = currentTime;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


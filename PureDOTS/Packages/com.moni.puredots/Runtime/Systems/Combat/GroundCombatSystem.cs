using PureDOTS.Runtime;
using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Groups;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Ground combat system for Godgame bands/villagers.
    /// Handles melee/range combat based on group stance.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FormationCombatSystem))]
    [UpdateAfter(typeof(CohesionEffectSystem))]
    public partial struct GroundCombatSystem : ISystem
    {
        private ComponentLookup<FormationBonus> _formationBonusLookup;
        private ComponentLookup<FormationIntegrity> _formationIntegrityLookup;
        private ComponentLookup<FormationCombatConfig> _formationConfigLookup;
        private ComponentLookup<CohesionCombatMultipliers> _cohesionMultipliersLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ScenarioState>();

            _formationBonusLookup = state.GetComponentLookup<FormationBonus>(true);
            _formationIntegrityLookup = state.GetComponentLookup<FormationIntegrity>(true);
            _formationConfigLookup = state.GetComponentLookup<FormationCombatConfig>(true);
            _cohesionMultipliersLookup = state.GetComponentLookup<CohesionCombatMultipliers>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenarioState) || !scenarioState.EnableGodgame)
            {
                return;
            }

            _formationBonusLookup.Update(ref state);
            _formationIntegrityLookup.Update(ref state);
            _formationConfigLookup.Update(ref state);
            _cohesionMultipliersLookup.Update(ref state);

            var currentTime = timeState.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Query entities with Health, AttackStats, and GroupMembership
            foreach (var (health, attackStats, transform, groupMembership, entity) in SystemAPI.Query<
                RefRW<Health>,
                RefRW<AttackStats>,
                RefRO<LocalTransform>,
                RefRO<GroupMembership>>()
                .WithEntityAccess())
            {
                // Check if entity's group has Attack stance
                var groupEntity = groupMembership.ValueRO.Group;
                if (!state.EntityManager.Exists(groupEntity))
                {
                    continue;
                }

                var groupStance = state.EntityManager.GetComponentData<GroupStanceState>(groupEntity);
                if (groupStance.Stance != GroupStance.Attack)
                {
                    continue;
                }

                // Check cooldown
                ref var attackStatsRef = ref attackStats.ValueRW;
                if (currentTime - attackStatsRef.LastAttackTime < attackStatsRef.AttackCooldown)
                {
                    continue;
                }

                // Find nearest enemy within range
                Entity nearestEnemy = Entity.Null;
                float nearestDistance = float.MaxValue;

                foreach (var (enemyHealth, enemyTransform, enemyEntity) in SystemAPI.Query<RefRO<Health>, RefRO<LocalTransform>>().WithEntityAccess())
                {
                    // Skip self
                    if (enemyEntity == entity)
                    {
                        continue;
                    }

                    float distance = math.distance(transform.ValueRO.Position, enemyTransform.ValueRO.Position);
                    if (distance <= attackStatsRef.Range && distance < nearestDistance)
                    {
                        nearestEnemy = enemyEntity;
                        nearestDistance = distance;
                    }
                }

                // Attack if enemy found
                if (nearestEnemy != Entity.Null && state.EntityManager.Exists(nearestEnemy))
                {
                    var enemyHealth = state.EntityManager.GetComponentData<Health>(nearestEnemy);
                    float damage = attackStatsRef.Damage;

                    // Apply formation bonus (if in band with formation)
                    if (_formationBonusLookup.HasComponent(groupEntity) &&
                        _formationIntegrityLookup.HasComponent(groupEntity) &&
                        _formationConfigLookup.HasComponent(groupEntity))
                    {
                        var bonus = _formationBonusLookup[groupEntity];
                        var integrity = _formationIntegrityLookup[groupEntity];
                        var config = _formationConfigLookup[groupEntity];
                        damage *= FormationCombatService.GetFormationAttackBonus(bonus, integrity, config);
                    }

                    // Apply cohesion multipliers (if band has cohesion)
                    if (_cohesionMultipliersLookup.HasComponent(groupEntity))
                    {
                        var cohesion = _cohesionMultipliersLookup[groupEntity];
                        damage *= cohesion.DamageMultiplier;
                    }

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


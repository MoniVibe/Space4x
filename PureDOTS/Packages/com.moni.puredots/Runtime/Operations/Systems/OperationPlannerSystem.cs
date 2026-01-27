using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.IntergroupRelations;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Decides when organizations start operations based on grievances, threats, goals, and OrgPersona.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OrgPersonaUpdateSystem))]
    public partial struct OperationPlannerSystem : ISystem
    {
        private EntityQuery _orgQuery;
        private ComponentLookup<OrgPersona> _personaLookup;
        private ComponentLookup<OrgRelation> _relationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<OrgTag>();

            _orgQuery = SystemAPI.QueryBuilder()
                .WithAll<OrgTag, OrgId, OrgPersona>()
                .WithNone<OperationTag>() // Don't plan if already has active operation
                .Build();

            _personaLookup = state.GetComponentLookup<OrgPersona>(true);
            _relationLookup = state.GetComponentLookup<OrgRelation>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;
            
            _personaLookup.Update(ref state);
            _relationLookup.Update(ref state);

            // Check each org for operation planning
            foreach (var (orgId, entity) in SystemAPI.Query<RefRO<OrgId>>()
                .WithAll<OrgTag, OrgPersona>()
                .WithNone<OperationTag>()
                .WithEntityAccess())
            {
                if (!_personaLookup.HasComponent(entity))
                    continue;

                var persona = _personaLookup[entity];

                // Evaluate potential operations
                EvaluateBlockadeOperation(ref state, entity, orgId.ValueRO, persona, currentTick);
                EvaluateSiegeOperation(ref state, entity, orgId.ValueRO, persona, currentTick);
                EvaluateProtestOperation(ref state, entity, orgId.ValueRO, persona, currentTick);
                EvaluateCultRitualOperation(ref state, entity, orgId.ValueRO, persona, currentTick);
            }
        }

        [BurstCompile]
        private void EvaluateBlockadeOperation(
            ref SystemState state,
            Entity initiatorOrg,
            OrgId orgId,
            OrgPersona persona,
            uint currentTick)
        {
            // Only armies, navies, and factions can blockade
            if (orgId.Kind != OrgKind.Faction && orgId.Kind != OrgKind.Other)
                return;

            // Find hostile relations
            var hostileTargets = FindHostileTargets(ref state, initiatorOrg, -50f);

            foreach (var target in hostileTargets)
            {
                // Check if blockade is justified (low trust, high fear, or hostile attitude)
                var relation = GetRelation(ref state, initiatorOrg, target);
                if (relation.Attitude > -30f) // Not hostile enough
                    continue;

                // Vengeful + Bold orgs more likely to blockade
                float blockadeProbability = CalculateBlockadeProbability(persona, relation);

                // Simple probability check (in production, use proper RNG)
                if (blockadeProbability > 0.3f)
                {
                    // Create operation request (will be handled by OperationInitSystem)
                    RequestOperation(ref state, initiatorOrg, target, OperationKind.Blockade, currentTick);
                    break; // One operation at a time
                }
            }
        }

        [BurstCompile]
        private void EvaluateSiegeOperation(
            ref SystemState state,
            Entity initiatorOrg,
            OrgId orgId,
            OrgPersona persona,
            uint currentTick)
        {
            // Only armies and factions can siege
            if (orgId.Kind != OrgKind.Faction && orgId.Kind != OrgKind.Other)
                return;

            // Find very hostile relations or targets harboring enemies
            var hostileTargets = FindHostileTargets(ref state, initiatorOrg, -70f);

            foreach (var target in hostileTargets)
            {
                var relation = GetRelation(ref state, initiatorOrg, target);
                
                // Vengeful orgs more likely to siege
                float siegeProbability = CalculateSiegeProbability(persona, relation);

                if (siegeProbability > 0.2f)
                {
                    RequestOperation(ref state, initiatorOrg, target, OperationKind.Siege, currentTick);
                    break;
                }
            }
        }

        [BurstCompile]
        private void EvaluateProtestOperation(
            ref SystemState state,
            Entity initiatorOrg,
            OrgId orgId,
            OrgPersona persona,
            uint currentTick)
        {
            // Guilds, companies, and factions can protest
            if (orgId.Kind != OrgKind.Guild && orgId.Kind != OrgKind.Company && orgId.Kind != OrgKind.Faction)
                return;

            // Find targets with grievances (low attitude, low trust)
            var grievanceTargets = FindGrievanceTargets(ref state, initiatorOrg);

            foreach (var target in grievanceTargets)
            {
                var relation = GetRelation(ref state, initiatorOrg, target);
                
                // High grievance level triggers protest
                float grievanceLevel = CalculateGrievanceLevel(relation);
                
                if (grievanceLevel > 0.5f)
                {
                    RequestOperation(ref state, initiatorOrg, target, OperationKind.Protest, currentTick);
                    break;
                }
            }
        }

        [BurstCompile]
        private void EvaluateCultRitualOperation(
            ref SystemState state,
            Entity initiatorOrg,
            OrgId orgId,
            OrgPersona persona,
            uint currentTick)
        {
            // Only churches/cults can perform rituals
            if (orgId.Kind != OrgKind.Church)
                return;

            // Cults periodically perform rituals (simplified - check if enough time passed)
            // In production, check for specific triggers (need mana, need favor, etc.)
            var lastRitualTick = GetLastRitualTick(ref state, initiatorOrg);
            uint ticksSinceLastRitual = currentTick - lastRitualTick;

            // Perform ritual every ~1 hour (216000 ticks)
            if (ticksSinceLastRitual > 216000)
            {
                // Find suitable location (simplified - use initiator's location)
                var targetLocation = GetOrgLocation(ref state, initiatorOrg);
                if (targetLocation != Entity.Null)
                {
                    RequestOperation(ref state, initiatorOrg, initiatorOrg, OperationKind.CultRitual, currentTick);
                }
            }
        }

        [BurstCompile]
        private float CalculateBlockadeProbability(OrgPersona persona, OrgRelation relation)
        {
            float baseProbability = 0.1f;

            // Vengeful orgs more likely
            baseProbability += persona.VengefulForgiving * 0.3f;

            // Bold orgs more likely
            baseProbability += persona.CravenBold * 0.2f;

            // Hostile attitude increases probability
            if (relation.Attitude < -50f)
                baseProbability += 0.3f;

            // Low trust increases probability
            baseProbability += (1f - relation.Trust) * 0.2f;

            return math.clamp(baseProbability, 0f, 1f);
        }

        [BurstCompile]
        private float CalculateSiegeProbability(OrgPersona persona, OrgRelation relation)
        {
            float baseProbability = 0.05f;

            // Vengeful orgs much more likely
            baseProbability += persona.VengefulForgiving * 0.4f;

            // Very hostile attitude required
            if (relation.Attitude < -70f)
                baseProbability += 0.3f;

            // High fear of target increases probability
            baseProbability += relation.Fear * 0.2f;

            return math.clamp(baseProbability, 0f, 1f);
        }

        [BurstCompile]
        private float CalculateGrievanceLevel(OrgRelation relation)
        {
            // Grievance = low attitude + low trust
            float attitudeGrievance = math.max(0f, -relation.Attitude / 100f);
            float trustGrievance = 1f - relation.Trust;

            return (attitudeGrievance + trustGrievance) / 2f;
        }

        [BurstCompile]
        private NativeList<Entity> FindHostileTargets(ref SystemState state, Entity org, float minAttitude)
        {
            var targets = new NativeList<Entity>(Allocator.Temp);

            foreach (var (relation, entity) in SystemAPI.Query<RefRO<OrgRelation>>()
                .WithAll<OrgRelationTag>()
                .WithEntityAccess())
            {
                Entity target = Entity.Null;
                if (relation.ValueRO.OrgA == org)
                    target = relation.ValueRO.OrgB;
                else if (relation.ValueRO.OrgB == org)
                    target = relation.ValueRO.OrgA;

                if (target != Entity.Null && relation.ValueRO.Attitude <= minAttitude)
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        [BurstCompile]
        private NativeList<Entity> FindGrievanceTargets(ref SystemState state, Entity org)
        {
            var targets = new NativeList<Entity>(Allocator.Temp);

            foreach (var (relation, entity) in SystemAPI.Query<RefRO<OrgRelation>>()
                .WithAll<OrgRelationTag>()
                .WithEntityAccess())
            {
                Entity target = Entity.Null;
                if (relation.ValueRO.OrgA == org)
                    target = relation.ValueRO.OrgB;
                else if (relation.ValueRO.OrgB == org)
                    target = relation.ValueRO.OrgA;

                if (target != Entity.Null)
                {
                    float grievance = CalculateGrievanceLevel(relation.ValueRO);
                    if (grievance > 0.3f)
                    {
                        targets.Add(target);
                    }
                }
            }

            return targets;
        }

        [BurstCompile]
        private OrgRelation GetRelation(ref SystemState state, Entity orgA, Entity orgB)
        {
            foreach (var relation in SystemAPI.Query<RefRO<OrgRelation>>()
                .WithAll<OrgRelationTag>())
            {
                if ((relation.ValueRO.OrgA == orgA && relation.ValueRO.OrgB == orgB) ||
                    (relation.ValueRO.OrgA == orgB && relation.ValueRO.OrgB == orgA))
                {
                    return relation.ValueRO;
                }
            }

            // Return neutral relation if not found
            return new OrgRelation
            {
                OrgA = orgA,
                OrgB = orgB,
                Attitude = 0f,
                Trust = 0.5f,
                Fear = 0f,
                Respect = 0.5f,
                Dependence = 0f
            };
        }

        [BurstCompile]
        private uint GetLastRitualTick(ref SystemState state, Entity org)
        {
            // Check for existing ritual operations
            foreach (var (operation, entity) in SystemAPI.Query<RefRO<Operation>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                if (operation.ValueRO.InitiatorOrg == org && 
                    operation.ValueRO.Kind == OperationKind.CultRitual)
                {
                    return operation.ValueRO.StartedTick;
                }
            }

            return 0;
        }

        [BurstCompile]
        private Entity GetOrgLocation(ref SystemState state, Entity org)
        {
            // Simplified - in production, query actual location component
            // For now, return org itself as location
            return org;
        }

        [BurstCompile]
        private void RequestOperation(
            ref SystemState state,
            Entity initiatorOrg,
            Entity targetOrg,
            OperationKind kind,
            uint currentTick)
        {
            // Create operation request entity (will be processed by OperationInitSystem)
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new OperationRequest
            {
                InitiatorOrg = initiatorOrg,
                TargetOrg = targetOrg,
                TargetLocation = targetOrg, // Simplified
                Kind = kind,
                RequestTick = currentTick
            });
        }

    }
}


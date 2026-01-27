using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.IntergroupRelations;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Ends operations (success/failure/stalemate) and handles transformations into other operations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BlockadeExecutionSystem))]
    [UpdateAfter(typeof(SiegeExecutionSystem))]
    [UpdateAfter(typeof(OccupationExecutionSystem))]
    public partial struct OperationResolutionSystem : ISystem
    {
        private ComponentLookup<OrgRelation> _relationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<OperationTag>();

            _relationLookup = state.GetComponentLookup<OrgRelation>(false); // Need write access
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            _relationLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Process all resolving operations
            foreach (var (operation, rules, progress, entity) in 
                SystemAPI.Query<RefRW<Operation>, RefRO<OperationRules>, RefRO<OperationProgress>>()
                .WithAll<OperationTag>()
                .WithEntityAccess())
            {
                var op = operation.ValueRO;

                if (op.State != OperationState.Resolving)
                    continue;

                // Determine outcome
                var outcome = OperationHelpers.DetermineOutcome(progress.ValueRO, rules.ValueRO);

                if (outcome == OperationState.Ended)
                {
                    // Finalize operation
                    FinalizeOperation(ref state, op, progress.ValueRO, ref operation.ValueRW, ecb, currentTick);

                    // Check for transformation into another operation
                    CheckTransformation(ref state, op, progress.ValueRO, ecb, currentTick);
                }
            }
        }

        [BurstCompile]
        private void FinalizeOperation(
            ref SystemState state,
            Operation operation,
            OperationProgress progress,
            ref Operation operationRW,
            EntityCommandBuffer ecb,
            uint currentTick)
        {
            // Update relations based on outcome
            UpdateRelations(ref state, operation, progress, ecb, currentTick);

            // Mark operation as ended
            operationRW.State = OperationState.Ended;
            operationRW.LastUpdateTick = currentTick;

            // Note: In production, also:
            // - Create operation history/record
            // - Trigger narrative events
            // - Update statistics
        }

        [BurstCompile]
        private void UpdateRelations(
            ref SystemState state,
            Operation operation,
            OperationProgress progress,
            EntityCommandBuffer ecb,
            uint currentTick)
        {
            // Find relation between initiator and target
            foreach (var (relation, relationEntity) in SystemAPI.Query<RefRW<OrgRelation>>()
                .WithAll<OrgRelationTag>()
                .WithEntityAccess())
            {
                bool isRelevant = false;
                if ((relation.ValueRO.OrgA == operation.InitiatorOrg && relation.ValueRO.OrgB == operation.TargetOrg) ||
                    (relation.ValueRO.OrgA == operation.TargetOrg && relation.ValueRO.OrgB == operation.InitiatorOrg))
                {
                    isRelevant = true;
                }

                if (!isRelevant)
                    continue;

                // Update relation based on operation outcome and kind
                float relationDelta = CalculateRelationDelta(operation, progress);

                relation.ValueRW.Attitude += relationDelta;
                relation.ValueRW.Attitude = math.clamp(relation.ValueRW.Attitude, -100f, 100f);

                // Update trust based on operation success
                if (progress.SuccessMetric > 0.7f)
                {
                    // Successful operation: initiator gains trust from target (if positive) or loses it (if negative)
                    if (relationDelta > 0)
                        relation.ValueRW.Trust += 0.1f;
                    else
                        relation.ValueRW.Trust -= 0.1f;
                }
                else if (progress.SuccessMetric < 0.3f)
                {
                    // Failed operation: initiator loses trust
                    relation.ValueRW.Trust -= 0.1f;
                }

                relation.ValueRW.Trust = math.clamp(relation.ValueRW.Trust, 0f, 1f);
                relation.ValueRW.LastUpdateTick = currentTick;

                // Update relation kind based on new attitude
                relation.ValueRW.Kind = DetermineRelationKind(relation.ValueRW.Attitude);
            }
        }

        [BurstCompile]
        private float CalculateRelationDelta(Operation operation, OperationProgress progress)
        {
            // Relation change depends on operation kind and success
            float baseDelta = 0f;

            switch (operation.Kind)
            {
                case OperationKind.Blockade:
                    // Blockades worsen relations
                    baseDelta = -10f * progress.SuccessMetric; // More successful = worse relations
                    break;

                case OperationKind.Siege:
                    // Sieges significantly worsen relations
                    baseDelta = -30f * progress.SuccessMetric;
                    break;

                case OperationKind.Occupation:
                    // Occupations worsen relations, but less if successful
                    baseDelta = -20f + (10f * progress.SuccessMetric); // Successful = less bad
                    break;

                case OperationKind.Protest:
                    // Protests slightly worsen relations
                    baseDelta = -5f * progress.SuccessMetric;
                    break;

                case OperationKind.Riot:
                    // Riots worsen relations more than protests
                    baseDelta = -15f * progress.SuccessMetric;
                    break;

                case OperationKind.CultRitual:
                    // Rituals massively worsen relations when discovered
                    baseDelta = -50f; // Always bad
                    break;

                case OperationKind.Funeral:
                    // Funerals can improve relations (respect)
                    baseDelta = 5f * progress.SuccessMetric;
                    break;

                case OperationKind.Festival:
                case OperationKind.Circus:
                    // Festivals improve relations slightly
                    baseDelta = 3f * progress.SuccessMetric;
                    break;

                case OperationKind.DeserterSettlement:
                    // Desertion worsens relations with original org
                    baseDelta = -25f;
                    break;
            }

            return baseDelta;
        }

        [BurstCompile]
        private OrgRelationKind DetermineRelationKind(float attitude)
        {
            if (attitude >= 75f)
                return OrgRelationKind.Allied;
            if (attitude >= 50f)
                return OrgRelationKind.Friendly;
            if (attitude >= 25f)
                return OrgRelationKind.Friendly;
            if (attitude <= -75f)
                return OrgRelationKind.Hostile;
            if (attitude <= -50f)
                return OrgRelationKind.Hostile;
            if (attitude <= -25f)
                return OrgRelationKind.Rival;
            return OrgRelationKind.Neutral;
        }

        [BurstCompile]
        private void CheckTransformation(
            ref SystemState state,
            Operation operation,
            OperationProgress progress,
            EntityCommandBuffer ecb,
            uint currentTick)
        {
            // Check if operation should transform into another operation
            // Examples:
            // - Siege → Occupation (if successful)
            // - Protest → Riot (already handled in ProtestRiotSystem)
            // - Funeral → Cult of personality (if renown very high)

            switch (operation.Kind)
            {
                case OperationKind.Siege:
                    // Successful siege may transform into occupation
                    if (progress.SuccessMetric > 0.8f)
                    {
                        TransformToOccupation(ref state, operation, ecb, currentTick);
                    }
                    break;

                case OperationKind.Funeral:
                    // Very high renown funeral may create cult of personality
                    // Note: Would need to check FuneralParams, but it's not in the query
                    // In production, add this check with proper params access
                    break;
            }
        }

        [BurstCompile]
        private void TransformToOccupation(
            ref SystemState state,
            Operation originalOperation,
            EntityCommandBuffer ecb,
            uint currentTick)
        {
            // Create new occupation operation
            var occupationEntity = ecb.CreateEntity();
            ecb.AddComponent(occupationEntity, new OperationTag());

            var occupation = new Operation
            {
                Kind = OperationKind.Occupation,
                InitiatorOrg = originalOperation.InitiatorOrg,
                TargetOrg = originalOperation.TargetOrg,
                TargetLocation = originalOperation.TargetLocation,
                State = OperationState.Active,
                StartedTick = currentTick,
                LastUpdateTick = currentTick
            };
            ecb.AddComponent(occupationEntity, occupation);

            // Initialize occupation rules (lighter stance after successful siege)
            var rules = OperationHelpers.CreateDefaultRules(0.5f, 0.5f, OperationKind.Occupation);
            rules.Stance = 0.6f; // Medium-heavy occupation
            ecb.AddComponent(occupationEntity, rules);

            // Initialize progress
            var progress = new OperationProgress
            {
                ElapsedTicks = 0,
                SuccessMetric = 0.5f,
                Casualties = 0,
                Unrest = 0.3f, // Start with some unrest from siege
                SiegeSupplyLevel = 1f,
                TargetSupplyLevel = 0.5f, // Low supply from siege
                TargetMorale = 0.4f // Low morale from siege
            };
            ecb.AddComponent(occupationEntity, progress);

            // Initialize occupation params
            var occupationParams = OperationHelpers.CreateOccupationParams(0.6f);
            ecb.AddComponent(occupationEntity, occupationParams);

            // Initialize participants buffer
            ecb.AddBuffer<OperationParticipant>(occupationEntity);
        }
    }
}






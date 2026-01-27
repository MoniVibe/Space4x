using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Service for updating entity relations based on interactions.
    /// Event-driven: creates RecordInteractionRequest components for processing.
    /// </summary>
    [BurstCompile]
    public static class RelationUpdateService
    {
        /// <summary>
        /// Record an interaction between two entities and update their relations.
        /// Creates a RecordInteractionRequest component for processing by RelationInteractionSystem.
        /// </summary>
        [BurstCompile]
        public static void RecordInteraction(
            ref EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            in Entity source,
            in Entity target,
            InteractionOutcome outcome,
            bool isMutual = true)
        {
            var requestEntity = ecb.CreateEntity(sortKey);
            ecb.AddComponent(sortKey, requestEntity, new RecordInteractionRequest
            {
                EntityA = source,
                EntityB = target,
                Outcome = outcome,
                IntensityChange = 0, // Will be calculated by RelationInteractionSystem
                TrustChange = 0,      // Will be calculated by RelationInteractionSystem
                IsMutual = isMutual
            });
        }

        /// <summary>
        /// Record an interaction with explicit intensity change.
        /// </summary>
        [BurstCompile]
        public static void RecordInteraction(
            ref EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            in Entity source,
            in Entity target,
            InteractionOutcome outcome,
            sbyte intensityChange,
            sbyte trustChange,
            bool isMutual = true)
        {
            var requestEntity = ecb.CreateEntity(sortKey);
            ecb.AddComponent(sortKey, requestEntity, new RecordInteractionRequest
            {
                EntityA = source,
                EntityB = target,
                Outcome = outcome,
                IntensityChange = intensityChange,
                TrustChange = trustChange,
                IsMutual = isMutual
            });
        }

        /// <summary>
        /// Directly modify a relation value (bypasses event system).
        /// Use sparingly - prefer RecordInteraction for most cases.
        /// </summary>
        [BurstCompile]
        public static void ModifyRelation(
            ref DynamicBuffer<EntityRelation> relations,
            in Entity target,
            sbyte delta,
            uint currentTick)
        {
            int index = RelationCalculator.FindRelationIndex(relations, target);
            if (index >= 0)
            {
                var relation = relations[index];
                relation.Intensity = (sbyte)math.clamp(relation.Intensity + delta, -100, 100);
                relation.LastInteractionTick = currentTick;
                relation.InteractionCount++;
                
                // Update type based on new intensity
                relation.Type = RelationCalculator.DetermineRelationType(
                    relation.Intensity, relation.InteractionCount, relation.Type);
                
                relations[index] = relation;
            }
        }

        /// <summary>
        /// Get relation delta for an interaction type and outcome.
        /// Maps InteractionType to InteractionOutcome for compatibility.
        /// </summary>
        [BurstCompile]
        public static sbyte GetRelationDelta(
            RelationType currentType,
            InteractionOutcome outcome)
        {
            return RelationCalculator.CalculateIntensityChange(currentType, outcome);
        }
    }
}


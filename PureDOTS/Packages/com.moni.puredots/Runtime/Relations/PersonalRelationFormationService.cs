using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using PureDOTS.Runtime.Social;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Service for forming personal relations from interactions.
    /// </summary>
    [BurstCompile]
    public static class PersonalRelationFormationService
    {
        /// <summary>
        /// Form a personal relation between two entities.
        /// </summary>
        [BurstCompile]
        public static void FormPersonalRelation(
            ref DynamicBuffer<PersonalRelation> personalRelations,
            in Entity target,
            PersonalRelationType type,
            float strength,
            float trust,
            uint currentTick)
        {
            // Check if relation already exists
            int existingIndex = FindPersonalRelationIndex(personalRelations, target);
            if (existingIndex >= 0)
            {
                // Update existing relation
                var relation = personalRelations[existingIndex];
                relation.RelationType = type;
                relation.Strength = math.clamp(strength, -100f, 100f);
                relation.Trust = math.clamp(trust, 0f, 1f);
                relation.LastUpdateTick = currentTick;
                personalRelations[existingIndex] = relation;
            }
            else
            {
                // Add new relation
                personalRelations.Add(new PersonalRelation
                {
                    TargetEntity = target,
                    RelationType = type,
                    Strength = math.clamp(strength, -100f, 100f),
                    Trust = math.clamp(trust, 0f, 1f),
                    LastUpdateTick = currentTick
                });
            }
        }

        /// <summary>
        /// Determine relation type from formation trigger and relation score.
        /// </summary>
        [BurstCompile]
        public static PersonalRelationType DetermineRelationType(
            RelationFormationTrigger trigger,
            float relationScore)
        {
            return trigger switch
            {
                RelationFormationTrigger.SharedExperience => relationScore > 50f 
                    ? PersonalRelationType.Friend 
                    : PersonalRelationType.Comrade,
                RelationFormationTrigger.Betrayal => PersonalRelationType.Enemy,
                RelationFormationTrigger.FamilyBond => PersonalRelationType.Family,
                RelationFormationTrigger.Teaching => PersonalRelationType.None, // Will be set explicitly (Mentor/Student)
                RelationFormationTrigger.Combat => relationScore > 0f 
                    ? PersonalRelationType.Comrade 
                    : PersonalRelationType.Enemy,
                _ => PersonalRelationType.None
            };
        }

        /// <summary>
        /// Process a formation event and create/update personal relation.
        /// </summary>
        [BurstCompile]
        public static void ProcessFormationEvent(
            ref DynamicBuffer<PersonalRelation> personalRelations,
            in Entity target,
            RelationFormationEventType eventType,
            float relationScore,
            uint currentTick)
        {
            PersonalRelationType relationType = eventType switch
            {
                RelationFormationEventType.SharedExperience => relationScore > 50f 
                    ? PersonalRelationType.Friend 
                    : PersonalRelationType.Comrade,
                RelationFormationEventType.Betrayal => PersonalRelationType.Enemy,
                RelationFormationEventType.FamilyEvent => PersonalRelationType.Family,
                RelationFormationEventType.Teaching => PersonalRelationType.None, // Mentor/Student set explicitly
                RelationFormationEventType.Learning => PersonalRelationType.None, // Mentor/Student set explicitly
                RelationFormationEventType.Combat => relationScore > 0f 
                    ? PersonalRelationType.Comrade 
                    : PersonalRelationType.Enemy,
                RelationFormationEventType.Trade => relationScore > 30f 
                    ? PersonalRelationType.Friend 
                    : PersonalRelationType.None,
                _ => PersonalRelationType.None
            };

            if (relationType != PersonalRelationType.None)
            {
                float strength = math.clamp(relationScore, -100f, 100f);
                float trust = math.max(0f, relationScore / 100f); // Trust scales with positive relations
                
                FormPersonalRelation(ref personalRelations, target, relationType, strength, trust, currentTick);
            }
        }

        /// <summary>
        /// Find index of personal relation with target entity.
        /// </summary>
        [BurstCompile]
        private static int FindPersonalRelationIndex(
            in DynamicBuffer<PersonalRelation> relations,
            in Entity target)
        {
            for (int i = 0; i < relations.Length; i++)
            {
                if (relations[i].TargetEntity == target)
                    return i;
            }
            return -1;
        }
    }
}


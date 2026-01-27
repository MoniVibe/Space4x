using Unity.Entities;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Helper functions for promoting Villagers to SimIndividuals.
    /// </summary>
    public static class PromotionHelpers
    {
        /// <summary>
        /// Promote a Villager entity to SimIndividual by adding SimIndividual components.
        /// This is called by a system that processes PromotionPending entities.
        /// </summary>
        public static void PromoteToSimIndividual(EntityManager em, Entity villagerEntity)
        {
            // Add SimIndividual marker
            if (!em.HasComponent<SimIndividualTag>(villagerEntity))
            {
                em.AddComponent<SimIndividualTag>(villagerEntity);
            }

            // Add IndividualId if not present
            if (!em.HasComponent<IndividualId>(villagerEntity))
            {
                // Generate a unique ID (in practice, this would come from a registry)
                em.AddComponentData(villagerEntity, new IndividualId { Value = villagerEntity.Index });
            }

            // Add core SimIndividual components if not present
            if (!em.HasComponent<AlignmentTriplet>(villagerEntity))
            {
                em.AddComponentData(villagerEntity, AlignmentTriplet.FromFloats(0f, 0f, 0f));
            }

            if (!em.HasComponent<IndividualStats>(villagerEntity))
            {
                // Initialize from existing VillagerAttributes if available
                em.AddComponentData(villagerEntity, IndividualStats.FromValues(5f, 5f, 5f, 5f, 5f, 5f, 5f));
            }

            if (!em.HasComponent<MoraleState>(villagerEntity))
            {
                em.AddComponentData(villagerEntity, MoraleState.FromValues(0f, 0f));
            }

            if (!em.HasComponent<InitiativeState>(villagerEntity))
            {
                em.AddComponent<InitiativeState>(villagerEntity);
                em.SetComponentData(villagerEntity, InitiativeState.FromValues(0.05f, 0.2f));
            }

            if (!em.HasComponent<PersonalityAxes>(villagerEntity))
            {
                em.AddComponent<PersonalityAxes>(villagerEntity);
                em.SetComponentData(villagerEntity, PersonalityAxes.FromValues(0f, 0f, 0f, 0f, 0f));
            }

            if (!em.HasComponent<BehaviorTuning>(villagerEntity))
            {
                em.AddComponent<BehaviorTuning>(villagerEntity);
                em.SetComponentData(villagerEntity, BehaviorTuning.Neutral());
            }

            // Remove PromotionPending tag
            if (em.HasComponent<PromotionPending>(villagerEntity))
            {
                em.RemoveComponent<PromotionPending>(villagerEntity);
            }
        }
    }
}


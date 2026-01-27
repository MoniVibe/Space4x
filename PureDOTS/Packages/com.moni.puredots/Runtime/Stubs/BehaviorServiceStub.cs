using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Behavior
{
    /// <summary>
    /// Lightweight helpers for wiring behavior profiles and needs ahead of richer planners.
    /// </summary>
    public static class BehaviorService
    {
        public static void ApplyProfile(EntityManager entityManager, Entity entity, int profileId, float modifier = 0f)
        {
            if (entityManager.Exists(entity))
            {
                if (!entityManager.HasComponent<BehaviorProfileId>(entity))
                    entityManager.AddComponentData(entity, new BehaviorProfileId { Profile = profileId });
                else
                    entityManager.SetComponentData(entity, new BehaviorProfileId { Profile = profileId });

                if (!entityManager.HasComponent<BehaviorModifier>(entity))
                    entityManager.AddComponentData(entity, new BehaviorModifier { Value = modifier });
                else
                    entityManager.SetComponentData(entity, new BehaviorModifier { Value = modifier });
            }
        }

        public static void RegisterNeed(EntityManager entityManager, Entity entity, byte needType, float initialSatisfaction = 1f)
        {
            if (!entityManager.Exists(entity))
                return;

            if (!entityManager.HasComponent<NeedCategory>(entity))
            {
                entityManager.AddComponentData(entity, new NeedCategory { Type = needType });
            }
            else
            {
                entityManager.SetComponentData(entity, new NeedCategory { Type = needType });
            }

            if (!entityManager.HasComponent<NeedSatisfaction>(entity))
            {
                entityManager.AddComponentData(entity, new NeedSatisfaction { Value = math.saturate(initialSatisfaction) });
            }
            else
            {
                var sat = entityManager.GetComponentData<NeedSatisfaction>(entity);
                sat.Value = math.saturate(initialSatisfaction);
                entityManager.SetComponentData(entity, sat);
            }

            if (!entityManager.HasBuffer<NeedRequestElement>(entity))
            {
                entityManager.AddBuffer<NeedRequestElement>(entity);
            }
        }

        public static void ReportSatisfaction(EntityManager entityManager, Entity entity, float delta)
        {
            if (!entityManager.Exists(entity) || !entityManager.HasComponent<NeedSatisfaction>(entity))
                return;

            var sat = entityManager.GetComponentData<NeedSatisfaction>(entity);
            sat.Value = math.saturate(sat.Value + delta);
            entityManager.SetComponentData(entity, sat);
        }
    }
}

using PureDOTS.Runtime.Groups;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring.Groups
{
    /// <summary>
    /// Authoring component for fleet groups.
    /// Bakes GroupObjective and GroupMetrics for fleets.
    /// </summary>
    public class FleetGroupAuthoring : MonoBehaviour
    {
        [Tooltip("Initial objective type")]
        public GroupObjectiveType InitialObjective = GroupObjectiveType.PatrolRoute;

        [Tooltip("Initial objective priority")]
        [Range(0, 255)]
        public byte InitialPriority = 50;

        [Tooltip("Resource budget (fuel)")]
        public float MaxFuel = 10000f;

        [Tooltip("Resource budget (ammunition)")]
        public float MaxAmmunition = 5000f;
    }

    /// <summary>
    /// Baker for FleetGroupAuthoring.
    /// </summary>
    public class FleetGroupBaker : Baker<FleetGroupAuthoring>
    {
        public override void Bake(FleetGroupAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add GroupObjective
            AddComponent(entity, new GroupObjective
            {
                ObjectiveType = authoring.InitialObjective,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                Priority = authoring.InitialPriority,
                SetTick = 0,
                ExpirationTick = 0,
                IsActive = 1
            });

            // Add GroupMetrics
            AddComponent<GroupMetrics>(entity);

            // Add GroupResourceBudget
            var budget = new GroupResourceBudget { IsEnforced = 1 };
            budget.MaxResource1 = authoring.MaxFuel; // Fuel/Energy
            budget.MaxResource2 = authoring.MaxAmmunition; // Ammunition
            AddComponent(entity, budget);
        }
    }
}


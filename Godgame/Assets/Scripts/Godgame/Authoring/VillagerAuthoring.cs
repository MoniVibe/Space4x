using Godgame.Registry;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Godgame.Authoring
{
    /// <summary>
    /// Authoring component for individual villager registry entries.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillagerAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Villager";
        [SerializeField] private int villagerId = 1;
        [SerializeField] private int factionId = 0;

        [Header("Availability & Status")]
        [SerializeField] private bool isAvailable = true;
        [SerializeField] private bool isReserved = false;
        [Range(0f, 100f)]
        [SerializeField] private float healthPercent = 100f;
        [Range(0f, 100f)]
        [SerializeField] private float moralePercent = 100f;
        [Range(0f, 100f)]
        [SerializeField] private float energyPercent = 100f;

        [Header("Job & Discipline")]
        [SerializeField] private VillagerJob.JobType jobType = VillagerJob.JobType.Gatherer;
        [SerializeField] private VillagerJob.JobPhase jobPhase = VillagerJob.JobPhase.Idle;
        [SerializeField] private VillagerDisciplineType discipline = VillagerDisciplineType.Forester;
        [Range(0, 10)]
        [SerializeField] private byte disciplineLevel = 1;
        [SerializeField] private bool isCombatReady = false;

        [Header("AI State")]
        [SerializeField] private VillagerAIState.State aiState = VillagerAIState.State.Idle;
        [SerializeField] private VillagerAIState.Goal aiGoal = VillagerAIState.Goal.Rest;
        [SerializeField] private uint activeTicketId = 0;
        [SerializeField] private ushort currentResourceTypeIndex = 0;
        [Range(0f, 2f)]
        [SerializeField] private float productivity = 1f;

        private sealed class Baker : Unity.Entities.Baker<VillagerAuthoring>
        {
            public override void Bake(VillagerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var name = string.IsNullOrWhiteSpace(authoring.displayName)
                    ? $"Villager-{authoring.villagerId}"
                    : authoring.displayName;

                AddComponent<SpatialIndexedTag>(entity);
                AddComponent(entity, new GodgameVillager
                {
                    DisplayName = new FixedString64Bytes(name),
                    VillagerId = authoring.villagerId,
                    FactionId = authoring.factionId,
                    IsAvailable = authoring.isAvailable ? (byte)1 : (byte)0,
                    IsReserved = authoring.isReserved ? (byte)1 : (byte)0,
                    HealthPercent = math.clamp(authoring.healthPercent, 0f, 100f),
                    MoralePercent = math.clamp(authoring.moralePercent, 0f, 100f),
                    EnergyPercent = math.clamp(authoring.energyPercent, 0f, 100f),
                    JobType = authoring.jobType,
                    JobPhase = authoring.jobPhase,
                    Discipline = authoring.discipline,
                    DisciplineLevel = authoring.disciplineLevel,
                    IsCombatReady = authoring.isCombatReady ? (byte)1 : (byte)0,
                    AIState = authoring.aiState,
                    AIGoal = authoring.aiGoal,
                    CurrentTarget = Entity.Null,
                    ActiveTicketId = authoring.activeTicketId,
                    CurrentResourceTypeIndex = authoring.currentResourceTypeIndex,
                    Productivity = math.max(0f, authoring.productivity)
                });
            }
        }
    }
}

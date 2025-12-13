using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Authoring hook for crew growth policies. Defaults to fully disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XCrewGrowthAuthoring : MonoBehaviour
    {
        [Header("Breeding (disabled by default)")]
        public bool breedingEnabled = false;
        [Tooltip("Crew per tick when breeding is enabled.")]
        public float breedingRatePerTick = 0f;
        public bool doctrineAllowsBreeding = false;

        [Header("Cloning (disabled by default)")]
        public bool cloningEnabled = false;
        [Tooltip("Crew per tick when cloning is enabled.")]
        public float cloningRatePerTick = 0f;
        [Tooltip("Resource cost per clone (for future integration).")]
        public float cloningResourceCost = 0f;
        public bool doctrineAllowsCloning = false;

        [Header("Initial Crew State")]
        public float initialCrew = 0f;
        public float crewCapacity = 0f;

        public class Baker : Unity.Entities.Baker<Space4XCrewGrowthAuthoring>
        {
            public override void Bake(Space4XCrewGrowthAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                var settings = new CrewGrowthSettings
                {
                    BreedingEnabled = (byte)(authoring.breedingEnabled ? 1 : 0),
                    CloningEnabled = (byte)(authoring.cloningEnabled ? 1 : 0),
                    BreedingRatePerTick = authoring.breedingRatePerTick,
                    CloningRatePerTick = authoring.cloningRatePerTick,
                    CloningResourceCost = authoring.cloningResourceCost,
                    DoctrineAllowsBreeding = (byte)(authoring.doctrineAllowsBreeding ? 1 : 0),
                    DoctrineAllowsCloning = (byte)(authoring.doctrineAllowsCloning ? 1 : 0),
                    LastConfiguredTick = 0
                };

                var validated = CrewGrowthSettingsUtility.Sanitize(settings);
                if (validated.HadError)
                {
                    Debug.LogError("[Space4XCrewGrowthAuthoring] Invalid rates detected; breeding/cloning toggles disabled until fixed.");
                }

                AddComponent(entity, validated.Settings);

                if (authoring.crewCapacity > 0f || authoring.initialCrew > 0f)
                {
                    AddComponent(entity, new CrewGrowthState
                    {
                        Capacity = math.max(0f, authoring.crewCapacity),
                        CurrentCrew = math.clamp(authoring.initialCrew, 0f, math.max(0f, authoring.crewCapacity))
                    });
                }
            }
        }
    }
}

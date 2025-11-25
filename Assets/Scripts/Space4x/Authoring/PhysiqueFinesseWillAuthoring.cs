using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for shared stat contract (Physique, Finesse, Will) with inclination modifiers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Physique Finesse Will")]
    public sealed class PhysiqueFinesseWillAuthoring : MonoBehaviour
    {
        [Header("Base Stats")]
        [Range(0f, 100f)]
        [Tooltip("Physical capability (overcharged shots, fire-rate boosts, cooling efficiency, hull plating, boarding strength)")]
        public float physique = 50f;

        [Range(0f, 100f)]
        [Tooltip("Precision capability (accuracy, focus, maneuver precision, tactical awareness, targeting)")]
        public float finesse = 50f;

        [Range(0f, 100f)]
        [Tooltip("Mental capability (psionic abilities, energy pool regen, aura potency, education/insight)")]
        public float will = 50f;

        [Header("Inclination Modifiers (1-10, scale XP gain and skill costs)")]
        [Range(1, 10)]
        [Tooltip("Physique inclination modifier")]
        public int physiqueInclination = 5;

        [Range(1, 10)]
        [Tooltip("Finesse inclination modifier")]
        public int finesseInclination = 5;

        [Range(1, 10)]
        [Tooltip("Will inclination modifier")]
        public int willInclination = 5;

        [Header("General XP Pool")]
        [Range(0f, 1000f)]
        [Tooltip("General XP pool for cross-discipline abilities")]
        public float generalXP = 0f;

        public sealed class Baker : Unity.Entities.Baker<PhysiqueFinesseWillAuthoring>
        {
            public override void Bake(PhysiqueFinesseWillAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.PhysiqueFinesseWill
                {
                    Physique = (half)math.clamp(authoring.physique, 0f, 100f),
                    Finesse = (half)math.clamp(authoring.finesse, 0f, 100f),
                    Will = (half)math.clamp(authoring.will, 0f, 100f),
                    PhysiqueInclination = (byte)math.clamp(authoring.physiqueInclination, 1, 10),
                    FinesseInclination = (byte)math.clamp(authoring.finesseInclination, 1, 10),
                    WillInclination = (byte)math.clamp(authoring.willInclination, 1, 10),
                    GeneralXP = math.max(0f, authoring.generalXP)
                });
            }
        }
    }
}


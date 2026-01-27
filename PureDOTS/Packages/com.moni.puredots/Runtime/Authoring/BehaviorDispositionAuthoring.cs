using PureDOTS.Runtime.Profile;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for BehaviorDisposition weights.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PureDOTS/Behavior/Behavior Disposition")]
    public sealed class BehaviorDispositionAuthoring : MonoBehaviour
    {
        [Header("Disposition Weights (0..1)")]
        [Range(0f, 1f)] public float compliance = 0.5f;
        [Range(0f, 1f)] public float caution = 0.5f;
        [Range(0f, 1f)] public float formationAdherence = 0.5f;
        [Range(0f, 1f)] public float riskTolerance = 0.5f;
        [Range(0f, 1f)] public float aggression = 0.5f;
        [Range(0f, 1f)] public float patience = 0.5f;
    }

    public sealed class BehaviorDispositionBaker : Baker<BehaviorDispositionAuthoring>
    {
        public override void Bake(BehaviorDispositionAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, BehaviorDisposition.FromValues(
                authoring.compliance,
                authoring.caution,
                authoring.formationAdherence,
                authoring.riskTolerance,
                authoring.aggression,
                authoring.patience));
        }
    }
}

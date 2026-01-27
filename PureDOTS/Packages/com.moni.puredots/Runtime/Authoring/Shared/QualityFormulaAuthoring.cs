using PureDOTS.Runtime.Shared;
using PureDOTS.Systems.Shared;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Authoring.Shared
{
    /// <summary>
    /// Authoring ScriptableObject for quality formula configuration.
    /// Used by both Godgame and Space4X for deterministic quality calculation.
    /// </summary>
    public class QualityFormulaAuthoring : MonoBehaviour
    {
        [Header("Quality Weights")]
        [Range(0f, 1f)]
        [Tooltip("Weight for material purity contribution")]
        public float WMaterial = 0.4f;

        [Range(0f, 1f)]
        [Tooltip("Weight for crafter/crew skill contribution")]
        public float WSkill = 0.2f;

        [Range(0f, 1f)]
        [Tooltip("Weight for workstation/forge rating contribution")]
        public float WStation = 0.1f;

        [Range(0f, 1f)]
        [Tooltip("Weight for recipe difficulty influence")]
        public float WRecipe = 0.3f;

        [Header("Formula Settings")]
        [Tooltip("Baseline offset added to weighted sum")]
        public float Bias = 0f;

        [Tooltip("Minimum clamp value for Score01")]
        public float ClampMin = 0f;

        [Tooltip("Maximum clamp value for Score01")]
        public float ClampMax = 1f;

        [Header("Tier Cutoffs")]
        [Tooltip("Tier cutoff thresholds (0-1), sorted ascending. Example: [0.20, 0.45, 0.70, 0.90] for Common/Uncommon/Rare/Epic/Legendary")]
        public List<float> TierCutoffs01 = new List<float> { 0.20f, 0.45f, 0.70f, 0.90f };
    }

    /// <summary>
    /// Baker for QualityFormulaAuthoring.
    /// Creates singleton QualityFormulaBlobRef with baked blob asset.
    /// </summary>
    public sealed class QualityFormulaBaker : Baker<QualityFormulaAuthoring>
    {
        public override void Bake(QualityFormulaAuthoring authoring)
        {
            using var bb = new BlobBuilder(Allocator.Temp);
            ref var root = ref bb.ConstructRoot<QualityFormulaBlob>();

            root.WMaterial = authoring.WMaterial;
            root.WSkill = authoring.WSkill;
            root.WStation = authoring.WStation;
            root.WRecipe = authoring.WRecipe;
            root.Bias = authoring.Bias;
            root.ClampMin = authoring.ClampMin;
            root.ClampMax = authoring.ClampMax;

            // Bake tier cutoffs
            var cutoffs = bb.Allocate(ref root.TierCutoffs01, authoring.TierCutoffs01.Count);
            for (int i = 0; i < authoring.TierCutoffs01.Count; i++)
            {
                cutoffs[i] = authoring.TierCutoffs01[i];
            }

            var blob = bb.CreateBlobAssetReference<QualityFormulaBlob>(Allocator.Persistent);
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new QualityFormulaBlobRef { Blob = blob });
        }
    }
}


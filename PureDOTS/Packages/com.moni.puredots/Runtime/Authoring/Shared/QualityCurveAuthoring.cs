using PureDOTS.Runtime.Shared;
using PureDOTS.Systems.Shared;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Authoring.Shared
{
    /// <summary>
    /// Authoring ScriptableObject for quality curve configuration.
    /// Maps quality Score01 to stat multipliers via curves.
    /// Used by both Godgame and Space4X.
    /// </summary>
    public class QualityCurveAuthoring : MonoBehaviour
    {
        [Header("Stat Curves")]
        [Tooltip("Damage multiplier curve (e.g., 0.95 at score 0, 1.10 at score 1)")]
        public AnimationCurve DamageCurve = AnimationCurve.Linear(0f, 0.95f, 1f, 1.10f);

        [Tooltip("Durability multiplier curve (e.g., 0.8 at score 0, 1.5 at score 1)")]
        public AnimationCurve DurabilityCurve = AnimationCurve.Linear(0f, 0.8f, 1f, 1.5f);

        [Tooltip("Heat multiplier curve (e.g., 1.05 at score 0, 0.90 at score 1 - lower is better)")]
        public AnimationCurve HeatCurve = AnimationCurve.Linear(0f, 1.05f, 1f, 0.90f);

        [Tooltip("Reliability multiplier curve (e.g., 0.9 at score 0, 1.3 at score 1)")]
        public AnimationCurve ReliabilityCurve = AnimationCurve.Linear(0f, 0.9f, 1f, 1.3f);

        [Header("Curve Sampling")]
        [Tooltip("Number of knots to sample from AnimationCurve (8-16 recommended)")]
        [Range(8, 16)]
        public int KnotCount = 12;
    }

    /// <summary>
    /// Baker for QualityCurveAuthoring.
    /// Converts AnimationCurves to Curve1D blobs with sampled knots.
    /// Creates singleton QualityCurveBlobRef with baked blob asset.
    /// </summary>
    public sealed class QualityCurveBaker : Baker<QualityCurveAuthoring>
    {
        public override void Bake(QualityCurveAuthoring authoring)
        {
            using var bb = new BlobBuilder(Allocator.Temp);
            ref var root = ref bb.ConstructRoot<QualityCurveBlob>();

            // Sample AnimationCurves into Curve1D knots
            SampleCurve(bb, ref root.Damage, authoring.DamageCurve, authoring.KnotCount);
            SampleCurve(bb, ref root.Durability, authoring.DurabilityCurve, authoring.KnotCount);
            SampleCurve(bb, ref root.Heat, authoring.HeatCurve, authoring.KnotCount);
            SampleCurve(bb, ref root.Reliability, authoring.ReliabilityCurve, authoring.KnotCount);

            var blob = bb.CreateBlobAssetReference<QualityCurveBlob>(Allocator.Persistent);
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new QualityCurveBlobRef { Blob = blob });
        }

        private void SampleCurve(BlobBuilder bb, ref Curve1D curve, AnimationCurve animCurve, int knotCount)
        {
            var knots = bb.Allocate(ref curve.Knots, knotCount);
            for (int i = 0; i < knotCount; i++)
            {
                float t = knotCount > 1 ? i / (float)(knotCount - 1) : 0f;
                knots[i] = animCurve.Evaluate(t);
            }
        }
    }
}


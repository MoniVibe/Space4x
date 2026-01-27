using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Shared
{
    /// <summary>
    /// Blob structure defining the formula for calculating quality Score01 from inputs.
    /// Used by both Godgame and Space4X for deterministic quality calculation.
    /// </summary>
    public struct QualityFormulaBlob
    {
        /// <summary>
        /// Weight for material purity contribution (0-1).
        /// </summary>
        public float WMaterial;

        /// <summary>
        /// Weight for crafter/crew skill contribution (0-1).
        /// </summary>
        public float WSkill;

        /// <summary>
        /// Weight for workstation/forge rating contribution (0-1).
        /// </summary>
        public float WStation;

        /// <summary>
        /// Weight for recipe difficulty influence (0-1).
        /// </summary>
        public float WRecipe;

        /// <summary>
        /// Baseline offset added to weighted sum.
        /// </summary>
        public float Bias;

        /// <summary>
        /// Minimum clamp value for Score01.
        /// </summary>
        public float ClampMin;

        /// <summary>
        /// Maximum clamp value for Score01.
        /// </summary>
        public float ClampMax;

        /// <summary>
        /// Tier cutoff thresholds (0-1), sorted ascending.
        /// Example: [0.20, 0.45, 0.70, 0.90] for Common/Uncommon/Rare/Epic/Legendary
        /// </summary>
        public BlobArray<float> TierCutoffs01;
    }

    /// <summary>
    /// Compact Burst-friendly curve structure using linear interpolation between knots.
    /// Typically 8-16 knots provide sufficient precision.
    /// </summary>
    public struct Curve1D
    {
        /// <summary>
        /// Knot values for linear interpolation. Uniformly spaced from t=0 to t=1.
        /// </summary>
        public BlobArray<float> Knots;
    }

    /// <summary>
    /// Blob structure mapping quality Score01 to stat multipliers via curves.
    /// Each stat has its own Curve1D that defines how quality affects it.
    /// </summary>
    public struct QualityCurveBlob
    {
        /// <summary>
        /// Damage multiplier curve (e.g., 0.95 at score 0, 1.10 at score 1).
        /// </summary>
        public Curve1D Damage;

        /// <summary>
        /// Durability multiplier curve (e.g., 0.8 at score 0, 1.5 at score 1).
        /// </summary>
        public Curve1D Durability;

        /// <summary>
        /// Heat multiplier curve (e.g., 1.05 at score 0, 0.90 at score 1 - lower is better).
        /// </summary>
        public Curve1D Heat;

        /// <summary>
        /// Reliability multiplier curve (e.g., 0.9 at score 0, 1.3 at score 1).
        /// </summary>
        public Curve1D Reliability;
    }
}


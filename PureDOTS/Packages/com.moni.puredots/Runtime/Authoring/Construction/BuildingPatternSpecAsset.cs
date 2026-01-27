#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Construction;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Construction
{
    /// <summary>
    /// ScriptableObject for authoring individual building pattern specifications.
    /// Defines building blueprints that groups can construct.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingPatternSpec", menuName = "PureDOTS/Construction/Building Pattern Spec", order = 1)]
    public class BuildingPatternSpecAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique pattern ID. Must be unique.")]
        public int PatternId;

        [Tooltip("Label for this building pattern (e.g., \"House\", \"Storehouse\", \"Temple\").")]
        public string Label = "New Building Pattern";

        [Header("Category")]
        [Tooltip("Primary category for this building.")]
        public BuildCategory Category = BuildCategory.Housing;

        [Header("Costs & Time")]
        [Tooltip("Generic \"build effort\" cost.")]
        [Min(0f)]
        public float BaseCost = 100f;

        [Tooltip("Base build time (seconds at 1 builder, baseline).")]
        [Min(0f)]
        public float BaseBuildTime = 60f;

        [Header("Tier & Unlock")]
        [Tooltip("Tech/advancement level (0..N).")]
        [Range(0, 255)]
        public byte Tier = 0;

        [Tooltip("Minimum population to unlock.")]
        [Min(0f)]
        public float MinPopulation = 0f;

        [Tooltip("Minimum food per capita to unlock.")]
        [Min(0f)]
        public float MinFoodPerCapita = 0f;

        [Tooltip("Required advancement level.")]
        [Range(0, 255)]
        public byte RequiresAdvancementLevel = 0;

        [Header("Behavior")]
        [Tooltip("Whether auto-build is eligible (villagers/groups allowed to auto-request).")]
        public bool IsAutoBuildEligible = true;

        [Tooltip("Pattern utility score (for scoring during selection).")]
        [Range(0f, 10f)]
        public float PatternUtility = 1f;

        /// <summary>
        /// Converts this asset to a BuildingPatternSpec for blob building.
        /// </summary>
        public BuildingPatternSpec ToSpec()
        {
            return new BuildingPatternSpec
            {
                PatternId = PatternId,
                Category = Category,
                BaseCost = BaseCost,
                BaseBuildTime = BaseBuildTime,
                Tier = Tier,
                MinPopulation = MinPopulation,
                MinFoodPerCapita = MinFoodPerCapita,
                RequiresAdvancementLevel = RequiresAdvancementLevel,
                IsAutoBuildEligible = IsAutoBuildEligible ? (byte)1 : (byte)0,
                PatternUtility = PatternUtility,
                Label = new FixedString64Bytes(Label)
            };
        }
    }
}
#endif

























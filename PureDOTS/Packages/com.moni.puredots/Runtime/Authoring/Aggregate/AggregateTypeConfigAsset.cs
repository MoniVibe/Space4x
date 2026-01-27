#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Aggregate;
using UnityEngine;
using System.Collections.Generic;

namespace PureDOTS.Authoring.Aggregate
{
    /// <summary>
    /// ScriptableObject for authoring individual aggregate type configurations.
    /// Defines how traits are aggregated into ambient conditions for a specific aggregate type.
    /// </summary>
    [CreateAssetMenu(fileName = "AggregateTypeConfig", menuName = "PureDOTS/Aggregate/Aggregate Type Config", order = 1)]
    public class AggregateTypeConfigAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Type ID matching AggregateIdentity.TypeId. Must be unique.")]
        public ushort TypeId;

        [Header("Aggregation Rules")]
        [Tooltip("Rules defining how source traits contribute to target ambient metrics.")]
        public List<AggregationRuleData> Rules = new List<AggregationRuleData>();

        [Header("Cascade Settings")]
        [Tooltip("Threshold for triggering cascade effects (delta magnitude).")]
        [Range(0f, 100f)]
        public float CompositionChangeThreshold = 10f;

        /// <summary>
        /// Converts this asset to an AggregateAggregationRule array for blob building.
        /// </summary>
        public AggregateAggregationRule[] ToAggregationRules()
        {
            var rules = new AggregateAggregationRule[Rules.Count];
            for (int i = 0; i < Rules.Count; i++)
            {
                rules[i] = new AggregateAggregationRule
                {
                    SourceTrait = (byte)Rules[i].SourceTrait,
                    TargetMetric = (byte)Rules[i].TargetMetric,
                    Weight = Rules[i].Weight
                };
            }
            return rules;
        }
    }

    /// <summary>
    /// Serializable data for an aggregation rule.
    /// </summary>
    [System.Serializable]
    public class AggregationRuleData
    {
        [Tooltip("Source trait that contributes to the target metric.")]
        public AggregateSourceTrait SourceTrait;

        [Tooltip("Target ambient metric that receives the contribution.")]
        public AggregateTargetMetric TargetMetric;

        [Tooltip("Weight of the contribution (multiplier).")]
        [Range(0f, 10f)]
        public float Weight = 1f;
    }
}
#endif

























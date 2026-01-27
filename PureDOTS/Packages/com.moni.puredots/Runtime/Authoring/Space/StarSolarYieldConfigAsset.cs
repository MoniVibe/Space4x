#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Space;
using UnityEngine;

namespace PureDOTS.Authoring.Space
{
    /// <summary>
    /// ScriptableObject for configuring solar yield calculation.
    /// </summary>
    [CreateAssetMenu(fileName = "StarSolarYieldConfig", menuName = "PureDOTS/Space/Star Solar Yield Config", order = 100)]
    public sealed class StarSolarYieldConfigAsset : ScriptableObject
    {
        [Header("Calculation Strategy")]
        [SerializeField, Tooltip("Strategy for calculating solar yield from luminosity.")]
        private SolarYieldStrategy strategy = SolarYieldStrategy.Normalize;

        [Header("Normalization Parameters")]
        [SerializeField, Tooltip("Maximum luminosity value for normalization.")]
        private float maxLuminosity = 1000000f;

        [SerializeField, Tooltip("Minimum luminosity value for normalization.")]
        private float minLuminosity = 0.0001f;

        [Header("Logarithmic Parameters")]
        [SerializeField, Tooltip("Logarithm base for logarithmic strategy (default: 10 for log10).")]
        private float logBase = 10f;

        [Header("Custom Parameters")]
        [SerializeField, Tooltip("Custom multiplier for custom strategy.")]
        private float customMultiplier = 1f;

        [SerializeField, Tooltip("Custom exponent for custom strategy.")]
        private float customExponent = 1f;

        public SolarYieldStrategy Strategy => strategy;
        public float MaxLuminosity => maxLuminosity;
        public float MinLuminosity => minLuminosity;
        public float LogBase => logBase;
        public float CustomMultiplier => customMultiplier;
        public float CustomExponent => customExponent;

        /// <summary>
        /// Converts to ECS component.
        /// </summary>
        public StarSolarYieldConfig ToComponent() => new StarSolarYieldConfig
        {
            Strategy = strategy,
            MaxLuminosity = maxLuminosity,
            MinLuminosity = minLuminosity,
            LogBase = logBase,
            CustomMultiplier = customMultiplier,
            CustomExponent = customExponent
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxLuminosity = Mathf.Max(0.0001f, maxLuminosity);
            minLuminosity = Mathf.Max(0f, Mathf.Min(minLuminosity, maxLuminosity));
            logBase = Mathf.Max(2f, logBase);
            customMultiplier = Mathf.Max(0f, customMultiplier);
            customExponent = Mathf.Max(0.1f, customExponent);
        }
#endif
    }
}
#endif

























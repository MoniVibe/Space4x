#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Environment;
using UnityEngine;

namespace PureDOTS.Authoring.Environment
{
    /// <summary>
    /// ScriptableObject for configuring climate oscillation and seasonal behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "ClimateConfig", menuName = "PureDOTS/Environment/Climate Config", order = 200)]
    public sealed class ClimateConfigAsset : ScriptableObject
    {
        [Header("Base Values")]
        [SerializeField, Tooltip("Base temperature (center of oscillation).")]
        private float baseTemperature = 20f;

        [SerializeField, Tooltip("Base humidity (center of oscillation, 0-1).")]
        [Range(0f, 1f)]
        private float baseHumidity = 0.5f;

        [Header("Oscillation")]
        [SerializeField, Tooltip("Temperature oscillation amplitude.")]
        private float temperatureOscillation = 10f;

        [SerializeField, Tooltip("Humidity oscillation amplitude (0-1).")]
        [Range(0f, 1f)]
        private float humidityOscillation = 0.3f;

        [SerializeField, Tooltip("Temperature oscillation period in ticks.")]
        private uint temperaturePeriod = 1000u;

        [SerializeField, Tooltip("Humidity oscillation period in ticks.")]
        private uint humidityPeriod = 800u;

        [Header("Seasons")]
        [SerializeField, Tooltip("Whether seasons are enabled.")]
        private bool seasonsEnabled = false;

        [SerializeField, Tooltip("Length of each season in ticks (if seasons enabled).")]
        private uint seasonLengthTicks = 250u;

        public float BaseTemperature => baseTemperature;
        public float BaseHumidity => baseHumidity;
        public float TemperatureOscillation => temperatureOscillation;
        public float HumidityOscillation => humidityOscillation;
        public uint TemperaturePeriod => temperaturePeriod;
        public uint HumidityPeriod => humidityPeriod;
        public bool SeasonsEnabled => seasonsEnabled;
        public uint SeasonLengthTicks => seasonLengthTicks;

        /// <summary>
        /// Converts to ECS component.
        /// Note: ClimateConfig component doesn't exist - climate is managed via ClimateState singleton.
        /// This method is kept for potential future use.
        /// </summary>
        public object ToComponent()
        {
            // ClimateConfig component doesn't exist - climate is managed via ClimateState singleton
            // Return null as placeholder - actual climate config should be set via ClimateState directly
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            baseHumidity = Mathf.Clamp01(baseHumidity);
            humidityOscillation = Mathf.Clamp01(humidityOscillation);
            temperaturePeriod = (uint)Mathf.Max(1, (int)temperaturePeriod);
            humidityPeriod = (uint)Mathf.Max(1, (int)humidityPeriod);
            seasonLengthTicks = (uint)Mathf.Max(1, (int)seasonLengthTicks);
        }
#endif
    }
}
#endif




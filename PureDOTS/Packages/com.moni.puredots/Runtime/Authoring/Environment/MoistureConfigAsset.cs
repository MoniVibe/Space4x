#if UNITY_EDITOR || UNITY_STANDALONE
using PureDOTS.Runtime.Environment;
using UnityEngine;

namespace PureDOTS.Authoring.Environment
{
    /// <summary>
    /// ScriptableObject for configuring moisture grid behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "MoistureConfig", menuName = "PureDOTS/Environment/Moisture Config", order = 201)]
    public sealed class MoistureConfigAsset : ScriptableObject
    {
        [Header("Rates")]
        [SerializeField, Tooltip("Base evaporation rate (moisture loss per tick).")]
        private float baseEvaporationRate = 0.001f;

        [SerializeField, Tooltip("Base absorption rate (moisture gain per tick from rain).")]
        private float baseAbsorptionRate = 0.01f;

        [SerializeField, Tooltip("Drainage factor (how fast moisture flows/drains).")]
        private float drainageFactor = 0.0005f;

        [Header("Multipliers")]
        [SerializeField, Tooltip("Temperature influence on evaporation (multiplier).")]
        private float temperatureEvaporationMultiplier = 0.1f;

        [SerializeField, Tooltip("Wind influence on evaporation (multiplier).")]
        private float windEvaporationMultiplier = 0.05f;

        [Header("Update")]
        [SerializeField, Tooltip("Update frequency (update every N ticks).")]
        private uint updateFrequency = 1u;

        public float BaseEvaporationRate => baseEvaporationRate;
        public float BaseAbsorptionRate => baseAbsorptionRate;
        public float DrainageFactor => drainageFactor;
        public float TemperatureEvaporationMultiplier => temperatureEvaporationMultiplier;
        public float WindEvaporationMultiplier => windEvaporationMultiplier;
        public uint UpdateFrequency => updateFrequency;

        /// <summary>
        /// Converts to ECS component.
        /// </summary>
        public MoistureConfig ToComponent() => new MoistureConfig
        {
            BaseEvaporationRate = baseEvaporationRate,
            BaseAbsorptionRate = baseAbsorptionRate,
            DrainageFactor = drainageFactor,
            TemperatureEvaporationMultiplier = temperatureEvaporationMultiplier,
            WindEvaporationMultiplier = windEvaporationMultiplier,
            UpdateFrequency = updateFrequency
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            baseEvaporationRate = Mathf.Max(0f, baseEvaporationRate);
            baseAbsorptionRate = Mathf.Max(0f, baseAbsorptionRate);
            drainageFactor = Mathf.Max(0f, drainageFactor);
            temperatureEvaporationMultiplier = Mathf.Max(0f, temperatureEvaporationMultiplier);
            windEvaporationMultiplier = Mathf.Max(0f, windEvaporationMultiplier);
            updateFrequency = (uint)Mathf.Max(1, (int)updateFrequency);
        }
#endif
    }
}
#endif

























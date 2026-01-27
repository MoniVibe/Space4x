using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Environment;

namespace PureDOTS.Authoring
{
    [CreateAssetMenu(fileName = "ClimateProfile", menuName = "PureDOTS/Environment/Climate Profile", order = 7)]
    public sealed class ClimateProfile : ScriptableObject
    {
        [Header("Seasonal Temperatures (°C)")]
        [SerializeField] float _springTemperature = 16f;
        [SerializeField] float _summerTemperature = 24f;
        [SerializeField] float _autumnTemperature = 14f;
        [SerializeField] float _winterTemperature = 4f;

        [Header("Temperature Variance")]
        [SerializeField, Min(0f)] float _dayNightSwing = 6f;
        [SerializeField, Range(0f, 1f)] float _seasonalSmoothing = 0.2f;

        [Header("Wind Defaults")]
        [SerializeField] Vector2 _baseWindDirection = new Vector2(0.7f, 0.5f);
        [SerializeField, Min(0f)] float _baseWindStrength = 8f;
        [SerializeField, Range(0f, 1f)] float _windVariationAmplitude = 0.15f;
        [SerializeField, Min(0f)] float _windVariationFrequency = 0.35f;

        [Header("Humidity & Clouds")]
        [SerializeField, Range(0f, 100f)] float _atmosphericMoistureBase = 55f;
        [SerializeField, Range(0f, 100f)] float _atmosphericMoistureVariation = 20f;
        [SerializeField, Range(0f, 100f)] float _cloudCoverBase = 25f;
        [SerializeField, Range(0f, 100f)] float _cloudCoverVariation = 20f;

        [Header("Time Scale")]
        [SerializeField, Min(0.01f)] float _hoursPerSecond = 0.5f;
        [SerializeField, Min(1f)] float _daysPerSeason = 30f;

        [Header("Moisture")]
        [SerializeField, Min(0f)] float _evaporationBaseRate = 0.5f;

        public ClimateProfileData ToComponent()
        {
            float2 baseWind = math.normalizesafe((float2)_baseWindDirection, new float2(0f, 1f));

            return new ClimateProfileData
            {
                SeasonalTemperatures = new float4(_springTemperature, _summerTemperature, _autumnTemperature, _winterTemperature),
                DayNightTemperatureSwing = _dayNightSwing,
                SeasonalTransitionSmoothing = _seasonalSmoothing,
                BaseWindDirection = baseWind,
                BaseWindStrength = _baseWindStrength,
                WindVariationAmplitude = _windVariationAmplitude,
                WindVariationFrequency = _windVariationFrequency,
                AtmosphericMoistureBase = _atmosphericMoistureBase,
                AtmosphericMoistureVariation = _atmosphericMoistureVariation,
                CloudCoverBase = _cloudCoverBase,
                CloudCoverVariation = _cloudCoverVariation,
                HoursPerSecond = _hoursPerSecond,
                DaysPerSeason = _daysPerSeason,
                EvaporationBaseRate = _evaporationBaseRate
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _springTemperature = ClampTemperature(_springTemperature);
            _summerTemperature = ClampTemperature(_summerTemperature);
            _autumnTemperature = ClampTemperature(_autumnTemperature);
            _winterTemperature = ClampTemperature(_winterTemperature);

            _dayNightSwing = Mathf.Max(0f, _dayNightSwing);
            _seasonalSmoothing = Mathf.Clamp01(_seasonalSmoothing);
            _baseWindStrength = Mathf.Max(0f, _baseWindStrength);
            _windVariationAmplitude = Mathf.Clamp01(_windVariationAmplitude);
            _windVariationFrequency = Mathf.Max(0f, _windVariationFrequency);
            _atmosphericMoistureBase = Mathf.Clamp(_atmosphericMoistureBase, 0f, 100f);
            _atmosphericMoistureVariation = Mathf.Clamp(_atmosphericMoistureVariation, 0f, 100f);
            _cloudCoverBase = Mathf.Clamp(_cloudCoverBase, 0f, 100f);
            _cloudCoverVariation = Mathf.Clamp(_cloudCoverVariation, 0f, 100f);
            _hoursPerSecond = Mathf.Max(0.01f, _hoursPerSecond);
            _daysPerSeason = Mathf.Max(1f, _daysPerSeason);
            _evaporationBaseRate = Mathf.Max(0f, _evaporationBaseRate);
        }

        static float ClampTemperature(float value)
        {
            return Mathf.Clamp(value, -80f, 80f);
        }
#endif
    }

    [DisallowMultipleComponent]
    public sealed class ClimateProfileAuthoring : MonoBehaviour
    {
        public ClimateProfile profile;
    }

    public sealed class ClimateProfileBaker : Baker<ClimateProfileAuthoring>
    {
        public override void Bake(ClimateProfileAuthoring authoring)
        {
            if (authoring.profile == null)
            {
                Debug.LogWarning("ClimateProfileAuthoring is missing a ClimateProfile asset.", authoring);
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, authoring.profile.ToComponent());
        }
    }
}


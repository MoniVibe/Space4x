using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Environment
{
    /// <summary>
    /// Authored climate profile parameters driving global environment behaviour.
    /// Values are sampled by the climate update systems each tick.
    /// </summary>
    public struct ClimateProfileData : IComponentData
    {
        public float4 SeasonalTemperatures;       // Spring, Summer, Autumn, Winter
        public float DayNightTemperatureSwing;    // ± degrees applied over a 24h cycle
        public float SeasonalTransitionSmoothing; // 0-1 blend factor for cross-season transitions
        public float2 BaseWindDirection;          // Normalised XZ direction
        public float BaseWindStrength;            // m/s baseline
        public float WindVariationAmplitude;      // Fractional strength variation (0.1 = 10%)
        public float WindVariationFrequency;      // Oscillation speed in radians per simulated day
        public float AtmosphericMoistureBase;     // 0-100 humidity baseline
        public float AtmosphericMoistureVariation;// Max additional humidity from seasonal drift
        public float CloudCoverBase;              // 0-100 baseline cloud cover
        public float CloudCoverVariation;         // Additional cloud cover from humidity swing
        public float HoursPerSecond;              // In-game hours progressed per simulated second
        public float DaysPerSeason;               // Length of a season in in-game days
        public float EvaporationBaseRate;         // Baseline evaporation factor
    }

    /// <summary>
    /// Provides sensible fallback values when no authored climate profile is present.
    /// </summary>
    public static class ClimateProfileDefaults
    {
        public static ClimateProfileData Create(in EnvironmentGridConfigData gridConfig)
        {
            var baseWind = gridConfig.DefaultWindDirection;
            if (math.lengthsq(baseWind) < 1e-6f)
            {
                baseWind = new float2(0f, 1f);
            }

            baseWind = math.normalizesafe(baseWind, new float2(0f, 1f));

            var seasonalSwing = math.max(0f, gridConfig.SeasonalSwing);
            var baseTemp = gridConfig.BaseSeasonTemperature;

            var spring = baseTemp + seasonalSwing * 0.25f;
            var summer = baseTemp + seasonalSwing * 0.5f;
            var autumn = baseTemp;
            var winter = baseTemp - seasonalSwing * 0.5f;

            return new ClimateProfileData
            {
                SeasonalTemperatures = new float4(spring, summer, autumn, winter),
                DayNightTemperatureSwing = math.max(0f, gridConfig.TimeOfDaySwing),
                SeasonalTransitionSmoothing = 0.2f,
                BaseWindDirection = baseWind,
                BaseWindStrength = math.max(0f, gridConfig.DefaultWindStrength),
                WindVariationAmplitude = 0.15f,
                WindVariationFrequency = 0.35f,
                AtmosphericMoistureBase = 55f,
                AtmosphericMoistureVariation = 20f,
                CloudCoverBase = 25f,
                CloudCoverVariation = 20f,
                HoursPerSecond = 0.5f,
                DaysPerSeason = 30f,
                EvaporationBaseRate = 0.5f
            };
        }
    }
}


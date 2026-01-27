using PureDOTS.Environment;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Biome preference vector describing ideal environmental conditions for a species/culture.
    /// </summary>
    public struct BiomePreference
    {
        public float IdealTemperature;   // Normalized: -1 (cold) to +1 (hot)
        public float IdealMoisture;      // 0..1 (0 = dry, 1 = wet)
        public float IdealFertility;     // 0..1 (0 = barren, 1 = rich)
        public float IdealWaterLevel;    // 0..1 (0 = land, 1 = water)
        public float IdealRuggedness;    // 0..1 (0 = flat, 1 = mountainous)
        public float Tolerance;          // How forgiving (higher = more tolerant, 0..1)

        public static BiomePreference Lerp(in BiomePreference a, in BiomePreference b, float t)
        {
            return new BiomePreference
            {
                IdealTemperature = math.lerp(a.IdealTemperature, b.IdealTemperature, t),
                IdealMoisture = math.lerp(a.IdealMoisture, b.IdealMoisture, t),
                IdealFertility = math.lerp(a.IdealFertility, b.IdealFertility, t),
                IdealWaterLevel = math.lerp(a.IdealWaterLevel, b.IdealWaterLevel, t),
                IdealRuggedness = math.lerp(a.IdealRuggedness, b.IdealRuggedness, t),
                Tolerance = math.lerp(a.Tolerance, b.Tolerance, t)
            };
        }
    }

    /// <summary>
    /// Calculates comfort score for a species/culture in a given climate.
    /// Returns 0..1 where 1 is perfect comfort, 0 is intolerable.
    /// </summary>
    public static class SpeciesComfortMath
    {
        public static float Comfort(in ClimateVector climate, in BiomePreference preference)
        {
            var dT = math.abs(climate.Temperature - preference.IdealTemperature);
            var dM = math.abs(climate.Moisture - preference.IdealMoisture);
            var dF = math.abs(climate.Fertility - preference.IdealFertility);
            var dW = math.abs(climate.WaterLevel - preference.IdealWaterLevel);
            var dR = math.abs(climate.Ruggedness - preference.IdealRuggedness);

            var dist = dT + dM + dF + dW + dR;
            var maxDist = 5f; // Maximum possible distance (all dimensions at max difference)
            var normalizedDist = math.clamp(dist / maxDist, 0f, 1f);

            // Tolerance acts as a multiplier: higher tolerance = less penalty for distance
            var toleranceFactor = 1f - preference.Tolerance;
            var effectiveDist = normalizedDist * toleranceFactor;

            return math.saturate(1f - effectiveDist);
        }
    }

    /// <summary>
    /// Environment profile for a species or culture, linking to RaceId/CultureId.
    /// </summary>
    public struct SpeciesEnvironmentProfile : IComponentData
    {
        public BiomePreference IdealBiome;
        public float PreferredGravity;      // Optional: normalized gravity preference
        public float PollutionTolerance;     // 0..1, how much pollution they can handle
    }

    /// <summary>
    /// Current comfort score for an entity in its environment.
    /// Updated by SpeciesComfortSystem.
    /// </summary>
    public struct EnvironmentComfort : IComponentData
    {
        public float ComfortScore;  // 0..1
        public uint LastUpdateTick;
    }
}


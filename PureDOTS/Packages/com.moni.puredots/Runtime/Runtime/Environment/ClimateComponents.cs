using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Environment
{
    /// <summary>
    /// Unified climate vector describing local environmental conditions per cell.
    /// Temperature is normalized (-1..+1), other values are 0..1.
    /// </summary>
    public struct ClimateVector
    {
        public float Temperature;   // Normalized: -1 (cold) to +1 (hot)
        public float Moisture;       // 0..1 (0 = dry, 1 = saturated)
        public float Fertility;      // 0..1 (0 = barren, 1 = rich)
        public float WaterLevel;     // 0..1 (0 = dry land, 1 = ocean)
        public float Ruggedness;     // 0..1 (0 = flat, 1 = mountainous)

        public static ClimateVector Lerp(in ClimateVector a, in ClimateVector b, float t)
        {
            return new ClimateVector
            {
                Temperature = math.lerp(a.Temperature, b.Temperature, t),
                Moisture = math.lerp(a.Moisture, b.Moisture, t),
                Fertility = math.lerp(a.Fertility, b.Fertility, t),
                WaterLevel = math.lerp(a.WaterLevel, b.WaterLevel, t),
                Ruggedness = math.lerp(a.Ruggedness, b.Ruggedness, t)
            };
        }

        public readonly float Distance(in ClimateVector other)
        {
            var dT = math.abs(Temperature - other.Temperature);
            var dM = math.abs(Moisture - other.Moisture);
            var dF = math.abs(Fertility - other.Fertility);
            var dW = math.abs(WaterLevel - other.WaterLevel);
            var dR = math.abs(Ruggedness - other.Ruggedness);
            return dT + dM + dF + dW + dR;
        }
    }

    /// <summary>
    /// Runtime per-cell climate data stored in dynamic buffer for deterministic mutation.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ClimateGridRuntimeCell : IBufferElementData
    {
        public ClimateVector Climate;
    }

    /// <summary>
    /// Kind of climate control source (natural phenomena, god intervention, or structures).
    /// </summary>
    public enum ClimateControlKind : byte
    {
        Natural = 0,      // Volcano, glacier, ocean currents
        GodMiracle = 1,   // Direct god interaction
        Structure = 2     // Terraforming altar, weather machine, biodeck module
    }

    /// <summary>
    /// Climate control source that gradually pulls local climate toward a target vector.
    /// Used by miracles, terraforming structures, and biodeck modules.
    /// </summary>
    public struct ClimateControlSource : IComponentData
    {
        public ClimateControlKind Kind;
        public float3 Center;
        public float Radius;
        public ClimateVector TargetClimate;
        public float Strength;  // 0..1, how fast it pushes toward target
    }
}

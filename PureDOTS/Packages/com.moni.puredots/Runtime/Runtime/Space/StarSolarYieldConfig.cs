using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Space
{
    /// <summary>
    /// Strategy for calculating solar yield from luminosity.
    /// </summary>
    public enum SolarYieldStrategy : byte
    {
        /// <summary>Simple normalization: yield = (luminosity - min) / (max - min).</summary>
        Normalize = 0,
        /// <summary>Logarithmic scale: yield = log(luminosity) / log(maxLuminosity).</summary>
        Logarithmic = 1,
        /// <summary>Custom calculation (uses custom parameters).</summary>
        Custom = 2
    }

    /// <summary>
    /// Configuration for solar yield calculation system.
    /// Singleton component that defines how luminosity maps to solar yield.
    /// </summary>
    public struct StarSolarYieldConfig : IComponentData
    {
        /// <summary>Calculation strategy to use.</summary>
        public SolarYieldStrategy Strategy;

        /// <summary>Maximum luminosity value for normalization.</summary>
        public float MaxLuminosity;

        /// <summary>Minimum luminosity value for normalization.</summary>
        public float MinLuminosity;

        /// <summary>Logarithm base for logarithmic strategy (default: 10 for log10).</summary>
        public float LogBase;

        /// <summary>Custom multiplier for custom strategy.</summary>
        public float CustomMultiplier;

        /// <summary>Custom exponent for custom strategy.</summary>
        public float CustomExponent;

        /// <summary>
        /// Default configuration with sensible values.
        /// </summary>
        public static StarSolarYieldConfig Default => new StarSolarYieldConfig
        {
            Strategy = SolarYieldStrategy.Normalize,
            MaxLuminosity = 1000000f, // Very bright star (1 million times Sun)
            MinLuminosity = 0.0001f,  // Very dim star (0.01% of Sun)
            LogBase = 10f,            // Base-10 logarithm
            CustomMultiplier = 1f,
            CustomExponent = 1f
        };
    }
}

























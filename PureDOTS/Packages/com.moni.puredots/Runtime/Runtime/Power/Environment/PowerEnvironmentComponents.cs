using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Power.Environment
{
    /// <summary>
    /// Star energy profile for solar power calculations.
    /// </summary>
    public struct StarEnergyProfile : IComponentData
    {
        public float Luminosity;           // relative to Sol
        public float SolarConstantAt1AU;   // W/m^2
        public byte StarClass;             // OBAFGKM etc
    }

    /// <summary>
    /// Local sun exposure for solar panels (ships, orbital, ground).
    /// </summary>
    public struct LocalSunExposure : IComponentData
    {
        public Entity Star;
        public float DistanceAU;
        public float ExposureFactor;       // clouds, latitude, time-of-day (0..1)
    }

    /// <summary>
    /// Wind cell data for wind turbine power calculations.
    /// </summary>
    public struct WindCell : IComponentData
    {
        public float Intensity;    // normalized wind speed
        public float3 Direction;
    }

    /// <summary>
    /// Terrain modifier for wind intensity.
    /// </summary>
    public struct TerrainWindModifier : IComponentData
    {
        public float BaseModifier; // 0.5 valley, 1.0 plains, 1.5 ridge
    }
}


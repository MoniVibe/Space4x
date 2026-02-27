using Unity.Entities;

namespace Space4X.Runtime
{
    /// <summary>
    /// Singleton binding for power routing state to the current flagship.
    /// </summary>
    public struct ShipPowerRoutingBinding : IComponentData
    {
        public Entity Ship;

        public static ShipPowerRoutingBinding Default => new ShipPowerRoutingBinding
        {
            Ship = Entity.Null
        };
    }

    /// <summary>
    /// Player-configured power routing profile (percent allocation by domain).
    /// </summary>
    public struct ShipPowerRoutingProfile : IComponentData
    {
        public float EnginesPercent;
        public float WeaponsPercent;
        public float ShieldsPercent;
        public float ReactorPercent;
        public float SensorsPercent;

        public static ShipPowerRoutingProfile Default => new ShipPowerRoutingProfile
        {
            EnginesPercent = 100f,
            WeaponsPercent = 100f,
            ShieldsPercent = 100f,
            ReactorPercent = 100f,
            SensorsPercent = 100f
        };
    }

    /// <summary>
    /// Runtime-resolved routing factors after power budget allocation.
    /// </summary>
    public struct ShipPowerRoutingRuntime : IComponentData
    {
        public float EnginesFactor;
        public float WeaponsFactor;
        public float ShieldsFactor;
        public float ReactorFactor;
        public float SensorsFactor;
        public float ReactorSignatureFactor;
        public float JamRiskPerTick;
        public float AllocationTotalPercent;
        public float OverclockPercent;
        public float UnderclockPercent;

        public static ShipPowerRoutingRuntime Default => new ShipPowerRoutingRuntime
        {
            EnginesFactor = 1f,
            WeaponsFactor = 1f,
            ShieldsFactor = 1f,
            ReactorFactor = 1f,
            SensorsFactor = 1f,
            ReactorSignatureFactor = 1f,
            JamRiskPerTick = 0f,
            AllocationTotalPercent = 500f,
            OverclockPercent = 0f,
            UnderclockPercent = 0f
        };
    }

    /// <summary>
    /// Baseline sensor/signature values used to apply non-destructive routing modifiers.
    /// Stored on the routing singleton to avoid adding components to heavyweight ship archetypes.
    /// </summary>
    public struct ShipPowerRoutingSensorBaseline : IComponentData
    {
        public Entity Ship;
        public byte Initialized;
        public float Range;
        public float Acuity;
        public float EmSignature;
        public float GraviticSignature;

        public static ShipPowerRoutingSensorBaseline Default => new ShipPowerRoutingSensorBaseline
        {
            Ship = Entity.Null,
            Initialized = 0,
            Range = 1f,
            Acuity = 1f,
            EmSignature = 1f,
            GraviticSignature = 1f
        };
    }
}

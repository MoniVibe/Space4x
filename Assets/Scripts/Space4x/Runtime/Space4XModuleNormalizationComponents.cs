using Unity.Entities;

namespace Space4X.Runtime
{
    public enum ShipManeuverMode : byte
    {
        Anchor = 0,
        Transit = 1,
        Maneuver = 2
    }

    public struct ManeuverMode : IComponentData
    {
        public ShipManeuverMode Mode;
    }

    public struct OfficerProfile : IComponentData
    {
        public float ExpectedManeuverHorizonSeconds;
        public float RiskTolerance;
    }
}

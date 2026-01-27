using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    public enum CombatManeuver : byte
    {
        None = 0,
        Strafe = 1,
        Kite = 2,
        JTurn = 3,
        Dive = 4,
        Disengage = 5
    }

    public struct PilotExperience : IComponentData
    {
        public float Experience;
    }

    public struct VesselManeuverProfile : IComponentData
    {
        public float StrafeThreshold;
        public float KiteThreshold;
        public float JTurnThreshold;
    }
}

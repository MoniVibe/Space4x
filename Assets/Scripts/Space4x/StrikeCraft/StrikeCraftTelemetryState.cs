using Space4X.Registry;
using Unity.Entities;

namespace Space4X.StrikeCraft
{
    /// <summary>
    /// Per-strike craft tracker used to detect role/profile changes and phase transitions for telemetry.
    /// </summary>
    public struct StrikeCraftTelemetryState : IComponentData
    {
        public AttackRunPhase LastPhase;
        public WeaponDeliveryType LastDeliveryType;
        public uint LastPhaseTick;
        public uint ProfileHash;
        public byte BehaviorLogged;
        public byte AttackRunActive;
        public byte LastWingDirectiveMode;
        public uint LastWingDirectiveTick;
    }
}

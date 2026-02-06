using Unity.Entities;

namespace Space4X.Registry
{
    public enum ModuleLimbId : ushort
    {
        Unknown = 0,
        Heatsink = 1,
        CoolantManifold = 2,
        Barrel = 3,
        Lens = 4,
        FocusCoil = 5,
        SensorArray = 6,
        FireControl = 7,
        ProjectorEmitter = 8,
        GuidanceCore = 9,
        ActuatorMotor = 10,
        StructuralFrame = 11,
        Capacitor = 12,
        PowerCoupler = 13
    }

    /// <summary>
    /// Per-limb runtime state; Integrity is 0-1.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ModuleLimbState : IBufferElementData
    {
        public ModuleLimbId LimbId;
        public ModuleLimbFamily Family;
        public float Integrity;
        public float Exposure;
    }

    /// <summary>
    /// Damage event directed at a module limb. If LimbId is Unknown, choose a limb in the family.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct ModuleLimbDamageEvent : IBufferElementData
    {
        public ModuleLimbFamily Family;
        public ModuleLimbId LimbId;
        public float Damage;
        public uint Tick;
    }
}

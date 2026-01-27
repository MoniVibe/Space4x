using Unity.Entities;

namespace PureDOTS.Runtime.Identity
{
    public struct EnergyPool : IComponentData
    {
        public float Current;
        public float Max;
        public float RegenPerSecond;
    }

    public struct HeatState : IComponentData
    {
        public float Temperature;
        public float SafeMin;
        public float SafeMax;
        public float CoolRatePerSecond;
    }

    public struct IntegrityState : IComponentData
    {
        public float Current;
        public float Max;
        public float RegenPerSecond;
    }
}




using Unity.Entities;

namespace Space4X.Runtime
{
    public enum Space4XReactorType : byte
    {
        FusionMicro = 0,
        FusionStandard = 1,
        FusionHeavy = 2,
        AntimatterCapital = 3
    }

    public enum RestartMode : byte
    {
        Hot = 0,
        Cold = 1
    }

    public enum ShipPowerFocusMode : byte
    {
        Balanced = 0,
        Attack = 1,
        Defense = 2,
        Mobility = 3,
        Stealth = 4,
        Emergency = 5
    }

    public enum ShipPowerConsumerType : byte
    {
        Mobility = 0,
        Weapons = 1,
        Shields = 2,
        Sensors = 3,
        Stealth = 4,
        LifeSupport = 5
    }

    public struct ShipReactorSpec : IComponentData
    {
        public Space4XReactorType Type;
        public float OutputMW;
        public float Efficiency;
        public float IdleDrawMW;
        public float HotRestartSeconds;
        public float ColdRestartSeconds;
    }

    public struct RestartState : IComponentData
    {
        public float Warmth; // 0-1
        public float RestartTimer; // seconds
        public RestartMode Mode;
    }

    public struct ShipPowerFocus : IComponentData
    {
        public ShipPowerFocusMode Mode;
    }

    public struct ShipPowerFocusCommand : IComponentData
    {
        public ShipPowerFocusMode Mode;
    }

    public struct ShipPowerConsumer : IBufferElementData
    {
        public ShipPowerConsumerType Type;
        public Entity Consumer;
    }

    public struct PowerAllocationTarget : IComponentData
    {
        public float Value; // 0-250
    }

    public struct ShipCapacitorBankTag : IComponentData
    {
    }
}

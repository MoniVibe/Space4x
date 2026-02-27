using Unity.Entities;

namespace Space4X.Runtime
{
    public enum Space4XHeatKernelSourceKind : byte
    {
        Generic = 0,
        Fleetcrawl = 1
    }

    /// <summary>
    /// Global tuning values for the generic Space4X thermal kernel.
    /// </summary>
    public struct Space4XHeatKernelConfig : IComponentData
    {
        public float DefaultHeatCapacity;
        public float DefaultDissipationPerSecond;
        public float BaselineHeatPerSecond;
        public float PowerLoadHeatPerSecond;
        public float ConsumerHeatPerSecond;
        public float SpeedHeatPerSecond;
        public float ThermalFloorFromPower;
        public float ThermalFloorFromSpeed;
        public float ResponseLerp;
        public float GenericBlendWhenFleetcrawl;
        public float MaxHeatRatio;

        public static Space4XHeatKernelConfig Default => new Space4XHeatKernelConfig
        {
            DefaultHeatCapacity = 120f,
            DefaultDissipationPerSecond = 8f,
            BaselineHeatPerSecond = 0.5f,
            PowerLoadHeatPerSecond = 35f,
            ConsumerHeatPerSecond = 18f,
            SpeedHeatPerSecond = 14f,
            ThermalFloorFromPower = 0.35f,
            ThermalFloorFromSpeed = 0.2f,
            ResponseLerp = 0.45f,
            GenericBlendWhenFleetcrawl = 0.25f,
            MaxHeatRatio = 1.25f
        };
    }

    /// <summary>
    /// Runtime state for per-entity thermal simulation.
    /// </summary>
    public struct Space4XHeatKernelState : IComponentData
    {
        public float CurrentHeat;
        public float HeatCapacity;
        public float DissipationPerSecond;
    }

    /// <summary>
    /// Unified thermal output consumed by signatures, UI, and diagnostics.
    /// </summary>
    public struct Space4XHeatKernelOutput : IComponentData
    {
        public float Thermal01;
        public float CurrentHeat;
        public float HeatCapacity;
        public float GeneratedPerSecond;
        public float DissipationPerSecond;
        public float PowerLoad01;
        public float ConsumerHeat01;
        public float Speed;
        public Space4XHeatKernelSourceKind SourceKind;
        public byte IsSaturated;
    }

    [InternalBufferCapacity(8)]
    public struct Space4XHeatKernelActionEvent : IBufferElementData
    {
        public float HeatPerSecond;
        public float Scale;
    }
}

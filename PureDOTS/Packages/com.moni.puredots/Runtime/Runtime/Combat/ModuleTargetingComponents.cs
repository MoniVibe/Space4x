using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Module target - selected module as target.
    /// </summary>
    public struct ModuleTarget : IComponentData
    {
        public Entity TargetModule;
        public Entity TargetShip;
        public uint TargetSelectedTick;
    }

    /// <summary>
    /// Module hit detection result.
    /// </summary>
    public struct ModuleHitDetection : IComponentData
    {
        public Entity HitModule;
        public float3 HitPosition;
        public float HitConfidence;
    }

    /// <summary>
    /// Module position data for hit detection.
    /// Stubbed initially - can be replaced with geometric data later.
    /// </summary>
    public struct ModulePosition : IComponentData
    {
        public float3 LocalPosition;
        public float Radius;
    }

    /// <summary>
    /// Module target priority - higher priority modules targeted first.
    /// </summary>
    public struct ModuleTargetPriority : IComponentData
    {
        public byte Priority; // 0-255, higher = more critical
    }
}


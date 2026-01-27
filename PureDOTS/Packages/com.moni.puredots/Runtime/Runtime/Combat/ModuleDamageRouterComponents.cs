using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Module damage router - routes damage to specific modules.
    /// </summary>
    public struct ModuleDamageRouter : IComponentData
    {
        public Entity TargetModule;
        public float DamageAmount;
        public uint DamageTick;
    }

    /// <summary>
    /// Module hit result - result of module hit detection.
    /// </summary>
    public struct ModuleHitResult : IComponentData
    {
        public Entity HitModule;
        public float3 HitPosition;
        public float DamageAmount;
        public uint HitTick;
    }

    /// <summary>
    /// Hull damage fallback - damage applied to hull when no module hit.
    /// </summary>
    public struct HullDamageFallback : IComponentData
    {
        public float DamageAmount;
        public uint DamageTick;
    }
}


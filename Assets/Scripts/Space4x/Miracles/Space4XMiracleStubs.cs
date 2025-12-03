using Unity.Entities;
using Unity.Mathematics;

namespace Space4x.Miracles
{
    /// <summary>
    /// TODO: Replace these stubs with actual PureDOTS.Runtime.Miracles types once the new miracle API is finalized.
    /// These are minimal compilation stubs to allow Space4x to build.
    /// </summary>

    /// <summary>
    /// Central registry component tracking all miracles in the system.
    /// </summary>
    [System.Obsolete("Use PureDOTS.Runtime.Registry.MiracleRegistry instead")]
    public struct MiracleRegistry : IComponentData
    {
        public int TotalMiracles;
        public int ActiveMiracles;
        public float TotalEnergyCost;
        public float TotalCooldownSeconds;
    }

    /// <summary>
    /// Buffer element representing a single miracle entry in the registry.
    /// </summary>
    [System.Obsolete("Use PureDOTS.Runtime.Registry.MiracleRegistryEntry instead")]
    public struct MiracleRegistryEntry : IBufferElementData
    {
        public Entity Source;
        public Entity Target;
        public Entity DefinitionEntity;
        public float ChargePercent;
        public uint LastCastTick;
        public MiracleLifecycleState Lifecycle;
        public MiracleRegistryFlags Flags;
    }

    /// <summary>
    /// Flags for miracle registry entries.
    /// </summary>
    [System.Obsolete("Use PureDOTS.Runtime.Registry.MiracleRegistryFlags instead")]
    [System.Flags]
    public enum MiracleRegistryFlags
    {
        None = 0,
        Active = 1 << 0,
        CoolingDown = 1 << 1,
    }

    /// <summary>
    /// Lifecycle state of a miracle.
    /// </summary>
    public enum MiracleLifecycleState : byte
    {
        Idle = 0,
        Active = 1,
        Cooldown = 2,
        // Legacy aliases for compatibility
        Ready = Idle,
        CoolingDown = Cooldown,
    }

    /// <summary>
    /// Target information for a miracle.
    /// </summary>
    public struct MiracleTarget : IComponentData
    {
        public float3 TargetPosition;
        public Entity TargetEntity;
    }

    /// <summary>
    /// Caster information for a miracle.
    /// </summary>
    [System.Obsolete("Use PureDOTS.Runtime.Components.MiracleCaster instead")]
    public struct MiracleCaster : IComponentData
    {
        public Entity CasterEntity;
    }

    /// <summary>
    /// Stub system for miracle registry management.
    /// TODO: Replace with actual PureDOTS.Runtime.Miracles.MiracleRegistrySystem once available.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))]
    public partial struct MiracleRegistrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Stub - no implementation
        }

        public void OnDestroy(ref SystemState state)
        {
            // Stub - no implementation
        }

        public void OnUpdate(ref SystemState state)
        {
            // Stub - no implementation
        }
    }
}


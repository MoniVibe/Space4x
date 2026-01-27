using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Ships
{
    /// <summary>
    /// Hull state - tracks hull hit points.
    /// </summary>
    public struct HullState : IComponentData
    {
        public float HP;
        public float MaxHP;
    }

    /// <summary>
    /// Shield arc state - tracks shield HP per arc (8 arcs).
    /// </summary>
    public struct ShieldArcState : IComponentData
    {
        public float HP0, HP1, HP2, HP3, HP4, HP5, HP6, HP7; // Per-arc HP

        public float GetHP(Facing8 facing)
        {
            return (byte)facing switch
            {
                0 => HP0, 1 => HP1, 2 => HP2, 3 => HP3,
                4 => HP4, 5 => HP5, 6 => HP6, 7 => HP7,
                _ => 0f
            };
        }

        public void SetHP(Facing8 facing, float value)
        {
            switch ((byte)facing)
            {
                case 0: HP0 = value; break;
                case 1: HP1 = value; break;
                case 2: HP2 = value; break;
                case 3: HP3 = value; break;
                case 4: HP4 = value; break;
                case 5: HP5 = value; break;
                case 6: HP6 = value; break;
                case 7: HP7 = value; break;
            }
        }
    }

    /// <summary>
    /// Armor degradation state - tracks armor ablation per arc (optional ablative armor).
    /// </summary>
    public struct ArmorDegradeState : IComponentData
    {
        public float Degrade0, Degrade1, Degrade2, Degrade3, Degrade4, Degrade5, Degrade6, Degrade7; // Per-arc degradation

        public float GetDegrade(Facing8 facing)
        {
            return (byte)facing switch
            {
                0 => Degrade0, 1 => Degrade1, 2 => Degrade2, 3 => Degrade3,
                4 => Degrade4, 5 => Degrade5, 6 => Degrade6, 7 => Degrade7,
                _ => 0f
            };
        }

        public void SetDegrade(Facing8 facing, float value)
        {
            switch ((byte)facing)
            {
                case 0: Degrade0 = value; break;
                case 1: Degrade1 = value; break;
                case 2: Degrade2 = value; break;
                case 3: Degrade3 = value; break;
                case 4: Degrade4 = value; break;
                case 5: Degrade5 = value; break;
                case 6: Degrade6 = value; break;
                case 7: Degrade7 = value; break;
            }
        }
    }

    /// <summary>
    /// Module runtime state - tracks module HP and status.
    /// </summary>
    public struct ModuleRuntimeState : IComponentData
    {
        public float HP;
        public float MaxHP;
        public byte Destroyed; // 0 = intact, 1 = destroyed
        public byte Disabled; // 0 = enabled, 1 = disabled
    }

    /// <summary>
    /// Buffer element version of <see cref="ModuleRuntimeState"/> for per-module data.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ModuleRuntimeStateElement : IBufferElementData
    {
        public float HP;
        public float MaxHP;
        public byte Destroyed; // 0 = intact, 1 = destroyed
        public byte Disabled; // 0 = enabled, 1 = disabled

        public static implicit operator ModuleRuntimeState(ModuleRuntimeStateElement element) => new ModuleRuntimeState
        {
            HP = element.HP,
            MaxHP = element.MaxHP,
            Destroyed = element.Destroyed,
            Disabled = element.Disabled
        };

        public static implicit operator ModuleRuntimeStateElement(ModuleRuntimeState state) => new ModuleRuntimeStateElement
        {
            HP = state.HP,
            MaxHP = state.MaxHP,
            Destroyed = state.Destroyed,
            Disabled = state.Disabled
        };
    }

    /// <summary>
    /// Module reference - maps to a module slot index in ShipLayoutBlob.
    /// </summary>
    public struct ModuleRef : IComponentData
    {
        public ushort Index; // Index into ShipLayoutBlob.Modules array
    }

    /// <summary>
    /// Crew state - tracks crew status.
    /// </summary>
    public struct CrewState : IComponentData
    {
        public int Alive; // Number of alive crew
        public int InPods; // Number of crew in escape pods
        public int Captured; // Number of captured crew
    }

    /// <summary>
    /// Derelict state - tracks ship derelict progression.
    /// </summary>
    public struct DerelictState : IComponentData
    {
        public byte Stage; // 0 = active, 1 = disabled, 2 = derelict, 3 = wreck
    }

    /// <summary>
    /// Tech level - repair gate for modules.
    /// </summary>
    public struct TechLevel : IComponentData
    {
        public byte Value; // Tech tier (0-255)
    }

    /// <summary>
    /// Life boat configuration - defines escape pod parameters.
    /// </summary>
    public struct LifeBoatConfig : IComponentData
    {
        public byte Count; // Number of pods
        public byte Seats; // Seats per pod
        public byte AutoEject; // 0/1 flag for automatic ejection
    }

    /// <summary>
    /// Hit event - directional hit information from projectile/beam systems.
    /// </summary>
    public struct HitEvent : IBufferElementData
    {
        public Entity TargetShip; // Ship entity being hit
        public float3 WorldPos; // World-space hit position
        public float3 WorldNormal; // World-space hit normal
        public DamageKind Kind; // Type of damage
        public float Damage; // Damage amount
        public uint Seed; // Deterministic RNG seed for crit/aim
    }

    /// <summary>
    /// Module damage event - emitted when a module takes damage.
    /// Used by critical effects system.
    /// </summary>
    public struct ModuleDamageEvent : IBufferElementData
    {
        public Entity ShipEntity; // Ship containing the module
        public ushort ModuleIndex; // Index into ShipLayoutBlob.Modules
        public float Damage; // Damage amount applied
        public byte WasDestroyed; // 1 if module was destroyed this tick
        public uint Tick; // Tick when damage occurred
    }
}


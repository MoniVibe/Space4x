// [TRI-STUB] Stub module operational states extension
using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Module operational state extensions.
    /// Extends ModuleState enum with combat-specific states.
    /// </summary>
    public enum ModuleCombatState : byte
    {
        ModuleDestroyed = 100,
        ModuleDamaged = 101,
        ModuleOffline = 102,
        ModuleRepairing = 103
    }
}


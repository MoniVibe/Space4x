using System;
using Unity.Entities;

namespace Space4X.Registry
{
    [Flags]
    public enum EntityDispositionFlags : ushort
    {
        None = 0,
        Civilian = 1 << 0,
        Trader = 1 << 1,
        Combatant = 1 << 2,
        Hostile = 1 << 3,
        Military = 1 << 4,
        Mining = 1 << 5,
        Hauler = 1 << 6,
        Support = 1 << 7
    }

    public struct EntityDisposition : IComponentData
    {
        public EntityDispositionFlags Flags;
    }

    public static class EntityDispositionUtility
    {
        public static bool IsCivilian(EntityDispositionFlags flags)
        {
            return (flags & (EntityDispositionFlags.Civilian |
                             EntityDispositionFlags.Trader |
                             EntityDispositionFlags.Mining |
                             EntityDispositionFlags.Hauler)) != 0;
        }

        public static bool IsCombatant(EntityDispositionFlags flags)
        {
            return (flags & (EntityDispositionFlags.Combatant | EntityDispositionFlags.Military)) != 0;
        }

        public static bool IsHostile(EntityDispositionFlags flags)
        {
            return (flags & EntityDispositionFlags.Hostile) != 0;
        }
    }
}

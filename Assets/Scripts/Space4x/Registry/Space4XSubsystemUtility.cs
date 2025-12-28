using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public static class Space4XSubsystemUtility
    {
        public const float EngineDisabledScale = 0.35f;

        public static bool TryGetSubsystem(DynamicBuffer<SubsystemHealth> subsystems, SubsystemType type, out SubsystemHealth health, out int index)
        {
            for (int i = 0; i < subsystems.Length; i++)
            {
                var entry = subsystems[i];
                if (entry.Type == type)
                {
                    health = entry;
                    index = i;
                    return true;
                }
            }

            health = default;
            index = -1;
            return false;
        }

        public static bool IsSubsystemDisabled(DynamicBuffer<SubsystemHealth> subsystems, SubsystemType type)
        {
            if (TryGetSubsystem(subsystems, type, out var health, out _))
            {
                if ((health.Flags & SubsystemFlags.Destroyed) != 0)
                {
                    return true;
                }

                return health.Current <= 0f;
            }

            return false;
        }

        public static bool IsSubsystemDisabled(DynamicBuffer<SubsystemHealth> subsystems, DynamicBuffer<SubsystemDisabled> disabled, SubsystemType type)
        {
            if (IsSubsystemDisabled(subsystems, type))
            {
                return true;
            }

            for (int i = 0; i < disabled.Length; i++)
            {
                if (disabled[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        public static float ResolveEngineScale(DynamicBuffer<SubsystemHealth> subsystems, DynamicBuffer<SubsystemDisabled> disabled)
        {
            return IsSubsystemDisabled(subsystems, disabled, SubsystemType.Engines) ? EngineDisabledScale : 1f;
        }

        public static bool IsWeaponMountDisabled(Entity entity, int mountIndex, DynamicBuffer<SubsystemHealth> subsystems, DynamicBuffer<SubsystemDisabled> disabled)
        {
            if (!IsSubsystemDisabled(subsystems, disabled, SubsystemType.Weapons))
            {
                return false;
            }

            return ShouldDisableMount(entity, mountIndex);
        }

        public static bool ShouldDisableMount(Entity entity, int mountIndex)
        {
            var hash = (uint)math.hash(new uint2((uint)entity.Index, (uint)mountIndex));
            return (hash & 1u) == 0u;
        }
    }
}

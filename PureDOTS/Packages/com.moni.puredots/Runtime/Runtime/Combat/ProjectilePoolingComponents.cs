using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    public struct ProjectileActive : IComponentData, IEnableableComponent
    {
    }

    public struct ProjectileRecycleTag : IComponentData, IEnableableComponent
    {
    }

    public struct ProjectilePoolConfig : IComponentData
    {
        public Entity Prefab;
        public int Capacity;
    }

    public struct ProjectilePoolState : IComponentData
    {
        public int Capacity;
        public int Available;
        public int Active;
        public int Dropped;
        public byte Initialized;
    }

    public struct ProjectilePoolEntry : IBufferElementData
    {
        public Entity Projectile;
    }
}

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    public struct Space4XEffectInstance : IComponentData
    {
        public FixedString64Bytes EffectId;
        public Entity Source;
        public float3 Position;
        public float3 Direction;
        public float Intensity;
        public float Lifetime;
    }

    public enum Space4XEffectBindingMode : byte
    {
        None = 0,
        VfxGraph = 1,
        Particle = 2
    }

    public enum Space4XEffectFollowMode : byte
    {
        World = 0,
        FollowSource = 1,
        FollowSourceWithOffset = 2
    }

    public sealed class Space4XEffectCatalog : IComponentData
    {
        public Space4XEffectCatalogEntry[] Entries;
    }

    public sealed class Space4XEffectCatalogEntry
    {
        public FixedString64Bytes EffectId;
        public UnityEngine.GameObject Prefab;
        public Space4XEffectBindingMode BindingMode;
        public Space4XEffectFollowMode FollowMode;
        public float3 FollowOffset;
        public float LifetimeOverride;
        public string IntensityProperty;
        public string DirectionProperty;
    }

    public sealed class Space4XEffectPresenter : IComponentData, ICleanupComponentData
    {
        public UnityEngine.GameObject Instance;
        public UnityEngine.VFX.VisualEffect Vfx;
        public UnityEngine.ParticleSystem Particle;
        public float BaseEmissionRate;
        public float BaseStartSize;
        public float BaseStartSpeed;
        public float BaseScale;
        public Space4XEffectBindingMode BindingMode;
        public string IntensityProperty;
        public string DirectionProperty;
    }
}

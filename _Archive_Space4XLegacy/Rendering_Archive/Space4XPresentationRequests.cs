using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Shared request buffer for presentation effects. Simulation writes to this in fixed-step.
    /// </summary>
    public struct Space4XEffectRequestStream : IComponentData { }

    /// <summary>
    /// Ephemeral presentation request describing a logical effect to play.
    /// </summary>
    public struct PlayEffectRequest : IBufferElementData
    {
        public FixedString64Bytes EffectId;
        public Entity AttachTo;
        public float Lifetime;
    }
}

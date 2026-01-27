using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Deception
{
    /// <summary>
    /// True vs cover identity. Cover is what most observers perceive until exposure.
    /// </summary>
    public struct DisguiseIdentity : IComponentData
    {
        public ushort TrueFactionId;
        public ushort CoverFactionId;

        /// <summary>0..1. Higher = harder to expose.</summary>
        public float DisguiseQuality01;

        /// <summary>0..1. Higher = better liar (reduces detection odds).</summary>
        public float LieSkill01;

        public byte IsActive;
    }

    /// <summary>
    /// Observer tuning for suspicion/exposure behavior.
    /// </summary>
    public struct DeceptionObserverConfig : IComponentData
    {
        public byte Enabled;
        public float SuspicionGainOnDetectedLie;
        public float SuspicionDecayPerTick;
        public float ExposeThresholdBase01;
        public byte MaxTracked;

        public static DeceptionObserverConfig Default => new DeceptionObserverConfig
        {
            Enabled = 1,
            SuspicionGainOnDetectedLie = 0.25f,
            SuspicionDecayPerTick = 0.0025f,
            ExposeThresholdBase01 = 0.7f,
            MaxTracked = 6
        };
    }

    public enum LieOutcomeHint : byte
    {
        None = 0,
        PlayAlong = 1,
        Curious = 2,
        Investigate = 3,
        PrivateCallout = 4,
        PublicCallout = 5,
        Detain = 6
    }

    /// <summary>
    /// Optional policy override; authored or derived from AIBehaviorProfile later.
    /// </summary>
    public struct DeceptionResponsePolicy : IComponentData
    {
        public LieOutcomeHint OnLieDetected;
        public LieOutcomeHint OnIdentityExposed;
        public float PublicCalloutThreshold01;
        public float PrivateCalloutThreshold01;

        public static DeceptionResponsePolicy Default => new DeceptionResponsePolicy
        {
            OnLieDetected = LieOutcomeHint.Curious,
            OnIdentityExposed = LieOutcomeHint.Investigate,
            PublicCalloutThreshold01 = 0.92f,
            PrivateCalloutThreshold01 = 0.78f
        };
    }

    /// <summary>
    /// Per-observer bounded suspicion memory toward specific targets.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct DisguiseDiscovery : IBufferElementData
    {
        public Entity TargetEntity;
        public float Suspicion01;
        public byte IsExposed;
        public uint LastUpdateTick;
    }

    public static class DeceptionPayloads
    {
        public static FixedString32Bytes HintToPayload(LieOutcomeHint hint)
        {
            return hint switch
            {
                LieOutcomeHint.PlayAlong => "lie.playalong",
                LieOutcomeHint.Curious => "lie.curious",
                LieOutcomeHint.Investigate => "lie.investigate",
                LieOutcomeHint.PrivateCallout => "lie.private",
                LieOutcomeHint.PublicCallout => "lie.public",
                LieOutcomeHint.Detain => "lie.detain",
                _ => "lie.unknown"
            };
        }
    }
}






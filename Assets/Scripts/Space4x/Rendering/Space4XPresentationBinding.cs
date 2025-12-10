using System.Runtime.CompilerServices;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    /// <summary>
    /// Per-entity presentation descriptor binding that tells the presentation queue which prefab to spawn.
    /// </summary>
    public struct Space4XPresentationBinding : IComponentData
    {
        /// <summary>
        /// Optional category hints for editor/debug tools.
        /// </summary>
        public enum EntityCategory : byte
        {
            Hull = 0,
            Module = 1,
            Station = 2,
            Resource = 3,
            Product = 4,
            Aggregate = 5,
            Effect = 6,
            Individual = 7
        }

        public Hash128 Descriptor;
        public float3 PositionOffset;
        public quaternion RotationOffset;
        public float ScaleMultiplier;
        public float4 Tint;
        public uint VariantSeed;
        public PresentationSpawnFlags Flags;

        public static Space4XPresentationBinding Create(Hash128 descriptor)
        {
            return new Space4XPresentationBinding
            {
                Descriptor = descriptor,
                PositionOffset = float3.zero,
                RotationOffset = quaternion.identity,
                ScaleMultiplier = 1f,
                Tint = float4.zero,
                VariantSeed = 0u,
                Flags = PresentationSpawnFlags.AllowPooling
            };
        }
    }

    /// <summary>
    /// Tag to request a refresh of the currently spawned visual. The assignment system recycles and respawns next frame.
    /// </summary>
    public struct Space4XPresentationDirtyTag : IComponentData { }

    public static class Space4XPresentationFlagUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PresentationSpawnFlags WithOverrides(
            bool overrideTint,
            bool overrideScale,
            bool overrideTransform)
        {
            var flags = PresentationSpawnFlags.AllowPooling;
            if (overrideTint)
            {
                flags |= PresentationSpawnFlags.OverrideTint;
            }

            if (overrideScale)
            {
                flags |= PresentationSpawnFlags.OverrideScale;
            }

            if (overrideTransform)
            {
                flags |= PresentationSpawnFlags.OverrideTransform;
            }

            return flags;
        }
    }
}


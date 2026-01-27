using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;

namespace PureDOTS.Runtime.Components
{
    public enum PresentationKind : byte
    {
        None = 0,
        Mesh = 1,
        Particle = 2,
        Vfx = 3,
        Audio = 4,
        Sfx = 5,
        Ui = 6
    }

    public struct Presentable : IComponentData
    {
    }

    public struct PresentationBindingBlob
    {
        public BlobArray<PresentationEffectBinding> Effects;
        public BlobArray<PresentationCompanionBinding> Companions;
    }

    public struct PresentationEffectBinding
    {
        public int EffectId;
        public PresentationKind Kind;
        public PresentationStyleBlock Style;
        public PresentationLifetimePolicy Lifetime;
        public PresentationAttachRule AttachRule;
        public float DurationSeconds;
    }

    public struct PresentationCompanionBinding
    {
        public int CompanionId;
        public PresentationKind Kind;
        public PresentationStyleBlock Style;
        public PresentationAttachRule AttachRule;
    }

    public struct PresentationBindingReference : IComponentData
    {
        public BlobAssetReference<PresentationBindingBlob> Binding;
    }

    public struct PresentationRequestHub : IComponentData
    {
    }

    public struct PlayEffectRequest : IBufferElementData
    {
        public int EffectId;
        public Entity Target;
        public float3 Position;
        public quaternion Rotation;
        public float DurationSeconds;
        public PresentationStyleOverride StyleOverride;
        public PresentationLifetimePolicy LifetimePolicy;
        public PresentationAttachRule AttachRule;
    }

    public struct SpawnCompanionRequest : IBufferElementData
    {
        public int CompanionId;
        public Entity Target;
        public float3 Position;
        public quaternion Rotation;
        public PresentationStyleOverride StyleOverride;
        public PresentationAttachRule AttachRule;
        public float3 Offset;
        public float FollowLerp;
    }

    public struct DespawnCompanionRequest : IBufferElementData
    {
        public Entity Target;
    }

    public struct PresentationCleanupTag : IComponentData
    {
        public int Handle;
        public PresentationKind Kind;
        public float SecondsRemaining;
        public PresentationLifetimePolicy Lifetime;
        public PresentationAttachRule AttachRule;
        public Entity Target;
    }

    public struct CompanionPresentation : IComponentData
    {
        public int CompanionId;
        public int Handle;
        public PresentationKind Kind;
        public PresentationStyleBlock Style;
        public PresentationAttachRule AttachRule;
        public float3 Offset;
        public float FollowLerp;
    }

    public struct PresentationEffect : IComponentData
    {
        public int EffectId;
        public int Handle;
        public PresentationKind Kind;
        public PresentationStyleBlock Style;
        public PresentationLifetimePolicy Lifetime;
        public PresentationAttachRule AttachRule;
        public Entity Target;
    }

    public struct PresentationRequestFailures : IComponentData
    {
        public int MissingBridge;
        public int MissingBindings;
        public int FailedPlayback;
        public int SuccessfulSpawns;
        public int SuccessfulEffects;
    }

    [Flags]
    public enum PresentationSpawnFlags : byte
    {
        None = 0,
        AllowPooling = 1 << 0,
        ForceAnimateOnSpawn = 1 << 1,
        OverrideTint = 1 << 2,
        OverrideScale = 1 << 3,
        OverrideTransform = 1 << 4
    }

    public enum PresentationLifetimePolicy : byte
    {
        Timed = 0,
        UntilRecycle = 1,
        Manual = 2
    }

    public enum PresentationAttachRule : byte
    {
        World = 0,
        FollowTarget = 1,
        AttachToTarget = 2
    }
    
    public struct PresentationStyleBlock
    {
        public FixedString64Bytes Style;
        public byte PaletteIndex;
        public float Size;
        public float Speed;
    }

    public struct PresentationStyleOverride
    {
        public FixedString64Bytes Style;
        public sbyte PaletteIndex;
        public float Size;
        public float Speed;

        public bool HasPalette => PaletteIndex >= 0;
        public bool HasSize => Size > 0f;
        public bool HasSpeed => Speed > 0f;

        public static PresentationStyleOverride FromStyle(in FixedString64Bytes style)
        {
            return new PresentationStyleOverride
            {
                Style = style,
                PaletteIndex = -1,
                Size = 0f,
                Speed = 0f
            };
        }
    }

    public struct PresentationDescriptor
    {
        public Unity.Entities.Hash128 KeyHash;
        public Entity Prefab;
        public float3 DefaultOffset;
        public float DefaultScale;
        public float4 DefaultTint;
        public PresentationSpawnFlags DefaultFlags;
    }

    public struct PresentationRegistryBlob
    {
        public BlobArray<PresentationDescriptor> Descriptors;
    }

    public struct PresentationRegistryReference : IComponentData
    {
        public BlobAssetReference<PresentationRegistryBlob> Registry;
    }

    public struct PresentationCommandQueue : IComponentData
    {
    }

    /// <summary>
    /// Command that forces all presentation visuals to be rebuilt from their handles.
    /// </summary>
    public struct PresentationReloadCommand : IComponentData
    {
        public int RequestId;
    }

    /// <summary>
    /// Aggregated stats for presentation pooling; updated each frame.
    /// </summary>
    public struct PresentationPoolStats : IComponentData
    {
        public uint ActiveVisuals;
        public uint SpawnedThisFrame;
        public uint RecycledThisFrame;
        public uint TotalSpawned;
        public uint TotalRecycled;
    }

    public struct PresentationSpawnRequest : IBufferElementData
    {
        public Entity Target;
        public Unity.Entities.Hash128 DescriptorHash;
        public float3 Position;
        public quaternion Rotation;
        public float ScaleMultiplier;
        public float4 Tint;
        public uint VariantSeed;
        public PresentationSpawnFlags Flags;
    }

    public struct PresentationRecycleRequest : IBufferElementData
    {
        public Entity Target;
    }

    public struct PresentationHandle : IComponentData
    {
        public Entity Visual;
        public Unity.Entities.Hash128 DescriptorHash;
        public uint VariantSeed;
    }

    /// <summary>
    /// Configuration for syncing presentation companions to their targets.
    /// Defaults snap visuals directly to the target transform.
    /// </summary>
    public struct PresentationHandleSyncConfig : IComponentData
    {
        public float PositionLerp;
        public float RotationLerp;
        public float ScaleLerp;
        public float3 VisualOffset;

        public static PresentationHandleSyncConfig Default => new PresentationHandleSyncConfig
        {
            PositionLerp = 1f,
            RotationLerp = 1f,
            ScaleLerp = 1f,
            VisualOffset = float3.zero
        };
    }

    public static class PresentationBindingUtility
    {
        public static bool TryGetEffectBinding(ref PresentationBindingReference bindingRef, int effectId, out PresentationEffectBinding binding)
        {
            binding = default;
            if (!bindingRef.Binding.IsCreated)
            {
                return false;
            }

            ref var blob = ref bindingRef.Binding.Value;
            ref var effects = ref blob.Effects;
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].EffectId == effectId)
                {
                    binding = effects[i];
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetCompanionBinding(ref PresentationBindingReference bindingRef, int companionId, out PresentationCompanionBinding binding)
        {
            binding = default;
            if (!bindingRef.Binding.IsCreated)
            {
                return false;
            }

            ref var blob = ref bindingRef.Binding.Value;
            ref var companions = ref blob.Companions;
            for (int i = 0; i < companions.Length; i++)
            {
                if (companions[i].CompanionId == companionId)
                {
                    binding = companions[i];
                    return true;
                }
            }

            return false;
        }

        public static PresentationStyleBlock ResolveStyle(in PresentationStyleBlock bindingStyle, in PresentationStyleOverride overrideStyle)
        {
            var resolved = bindingStyle;
            if (!overrideStyle.Style.IsEmpty)
            {
                resolved.Style = overrideStyle.Style;
            }

            if (overrideStyle.HasPalette)
            {
                resolved.PaletteIndex = (byte)overrideStyle.PaletteIndex;
            }

            if (overrideStyle.HasSize)
            {
                resolved.Size = overrideStyle.Size;
            }

            if (overrideStyle.HasSpeed)
            {
                resolved.Speed = overrideStyle.Speed;
            }

            return resolved;
        }
    }

    public static class PresentationKeyUtility
    {
        private const int MaxKeyLength = 48;

        /// <summary>
        /// Builds a deterministic Entities.Hash128 from a presentation key string. Burst jobs shouldn't
        /// compile this method because it calls managed hash utilities, so discard for Burst.
        /// </summary>
        [BurstDiscard]
        public static bool TryParseKey(string key, out Unity.Entities.Hash128 hash, out string sanitizedKey)
        {
            sanitizedKey = string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                hash = default;
                return false;
            }

            string lower = key.Trim().ToLowerInvariant();
            if (lower.Length > MaxKeyLength)
            {
                lower = lower.Substring(0, MaxKeyLength);
            }

            var engineHash = UnityEngine.Hash128.Compute(lower);
            hash = new Unity.Entities.Hash128(engineHash.ToString());
            if (!hash.IsValid)
            {
                return false;
            }

            sanitizedKey = lower;
            return true;
        }
    }

    public static class PresentationRegistryUtility
    {
        public static bool TryGetDescriptor(ref PresentationRegistryReference registryRef, Unity.Entities.Hash128 key, out PresentationDescriptor descriptor)
        {
            descriptor = default;

            if (!registryRef.Registry.IsCreated)
            {
                return false;
            }

            ref var blob = ref registryRef.Registry.Value;
            ref var descriptors = ref blob.Descriptors;
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].KeyHash == key)
                {
                    descriptor = descriptors[i];
                    return true;
                }
            }

            return false;
        }
    }
}


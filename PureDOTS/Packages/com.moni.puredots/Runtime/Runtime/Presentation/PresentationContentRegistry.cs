using System;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    [Flags]
    public enum PresentationContentFlags : byte
    {
        None = 0,
        HasRenderBinding = 1 << 0,
        HasDescriptor = 1 << 1,
        HasSceneReference = 1 << 2,
        HasBaseScale = 1 << 3,
        HasBaseTint = 1 << 4
    }

    public struct PresentationContentBinding
    {
        public RegistryId Id;
        public ushort RenderSemanticKey;
        public ushort RenderArchetypeId;
        public Unity.Entities.Hash128 DescriptorHash;
        public Unity.Entities.Hash128 SceneGuid;
        public float BaseScale;
        public float4 BaseTint;
        public PresentationContentFlags Flags;
    }

    public struct PresentationContentRegistryBlob
    {
        public BlobArray<PresentationContentBinding> Bindings;
    }

    public struct PresentationContentRegistryReference : IComponentData
    {
        public BlobAssetReference<PresentationContentRegistryBlob> Registry;
    }

    /// <summary>
    /// Resolved presentation binding for a sim entity with RegistryIdentity.
    /// Stored so presentation systems can consume without re-querying the registry blob.
    /// </summary>
    public struct PresentationContentResolved : IComponentData
    {
        public RegistryId Id;
        public ushort RenderSemanticKey;
        public ushort RenderArchetypeId;
        public Unity.Entities.Hash128 DescriptorHash;
        public Unity.Entities.Hash128 SceneGuid;
        public float BaseScale;
        public float4 BaseTint;
        public PresentationContentFlags Flags;
    }

    public static class PresentationContentRegistryUtility
    {
        public static bool TryGetBinding(
            ref PresentationContentRegistryReference registryRef,
            RegistryId id,
            out PresentationContentBinding binding)
        {
            binding = default;

            if (!registryRef.Registry.IsCreated || !id.IsValid)
            {
                return false;
            }

            ref var blob = ref registryRef.Registry.Value;
            ref var entries = ref blob.Bindings;
            int lo = 0;
            int hi = entries.Length - 1;

            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                var candidate = entries[mid].Id;
                int cmp = id.CompareTo(candidate);
                if (cmp == 0)
                {
                    binding = entries[mid];
                    return true;
                }
                if (cmp < 0)
                {
                    hi = mid - 1;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            return false;
        }
    }
}

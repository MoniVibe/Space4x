using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [CreateAssetMenu(menuName = "PureDOTS/Presentation/Content Registry", fileName = "PresentationContentRegistry")]
    public sealed class PresentationContentRegistryAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Header("Identity")]
            [Tooltip("Stable registry id (lowercase).")]
            public string registryId;

            [Header("Render Binding")]
            public ushort renderSemanticKey;
            public ushort renderArchetypeId;

            [Header("Presentation Descriptor")]
            [Tooltip("Presentation descriptor key resolved by PresentationRegistry.")]
            public string descriptorKey;

            [Header("Streaming")]
            [Tooltip("Scene GUID (string) for streamed interiors/modules, optional.")]
            public string sceneGuid;

            [Header("Overrides")]
            public bool setBaseScale;
            public float baseScale = 1f;
            public bool setBaseTint;
            public Color baseTint = Color.white;
        }

        public List<Entry> entries = new();
    }

    [DisallowMultipleComponent]
    public sealed class PresentationContentRegistryAuthoring : MonoBehaviour
    {
        public PresentationContentRegistryAsset registry;
    }

    public sealed class PresentationContentRegistryBaker : Baker<PresentationContentRegistryAuthoring>
    {
        private struct BuildEntry
        {
            public RegistryId Id;
            public PresentationContentBinding Binding;
        }

        public override void Bake(PresentationContentRegistryAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);

            if (authoring.registry == null || authoring.registry.entries == null || authoring.registry.entries.Count == 0)
            {
                AddComponent(entity, new PresentationContentRegistryReference
                {
                    Registry = BlobAssetReference<PresentationContentRegistryBlob>.Null
                });
                return;
            }

            var definitions = authoring.registry.entries;
            var buildList = new List<BuildEntry>(definitions.Count);
            var seen = new HashSet<RegistryId>();

            for (int i = 0; i < definitions.Count; i++)
            {
                var entry = definitions[i];
                if (entry == null)
                {
                    continue;
                }

                if (!RegistryId.TryParse(entry.registryId, out var registryId, out _))
                {
                    Debug.LogWarning($"PresentationContentRegistry '{authoring.registry.name}' entry {i} has invalid registry id '{entry.registryId}'.", authoring);
                    continue;
                }

                if (!seen.Add(registryId))
                {
                    Debug.LogWarning($"Duplicate registry id '{entry.registryId}' in PresentationContentRegistry '{authoring.registry.name}'.", authoring);
                    continue;
                }

                var binding = new PresentationContentBinding
                {
                    Id = registryId,
                    RenderSemanticKey = entry.renderSemanticKey,
                    RenderArchetypeId = entry.renderArchetypeId,
                    DescriptorHash = default,
                    SceneGuid = default,
                    BaseScale = entry.setBaseScale ? math.max(0.01f, entry.baseScale) : 1f,
                    BaseTint = entry.setBaseTint
                        ? new float4(entry.baseTint.r, entry.baseTint.g, entry.baseTint.b, entry.baseTint.a)
                        : new float4(1f, 1f, 1f, 1f),
                    Flags = PresentationContentFlags.None
                };

                if (binding.RenderArchetypeId == 0 && binding.RenderSemanticKey != 0)
                {
                    binding.RenderArchetypeId = binding.RenderSemanticKey;
                }

                if (binding.RenderSemanticKey != 0 || binding.RenderArchetypeId != 0)
                {
                    binding.Flags |= PresentationContentFlags.HasRenderBinding;
                }

                if (!string.IsNullOrWhiteSpace(entry.descriptorKey) &&
                    PresentationKeyUtility.TryParseKey(entry.descriptorKey, out var descriptorHash, out _))
                {
                    binding.DescriptorHash = descriptorHash;
                    binding.Flags |= PresentationContentFlags.HasDescriptor;
                }

                if (!string.IsNullOrWhiteSpace(entry.sceneGuid))
                {
                    try
                    {
                        binding.SceneGuid = new Unity.Entities.Hash128(entry.sceneGuid.Trim());
                        if (binding.SceneGuid.IsValid)
                        {
                            binding.Flags |= PresentationContentFlags.HasSceneReference;
                        }
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning($"PresentationContentRegistry '{authoring.registry.name}' entry '{entry.registryId}' has invalid scene guid.", authoring);
                    }
                }

                if (entry.setBaseScale)
                {
                    binding.Flags |= PresentationContentFlags.HasBaseScale;
                }
                if (entry.setBaseTint)
                {
                    binding.Flags |= PresentationContentFlags.HasBaseTint;
                }

                buildList.Add(new BuildEntry { Id = registryId, Binding = binding });
            }

            if (buildList.Count == 0)
            {
                AddComponent(entity, new PresentationContentRegistryReference
                {
                    Registry = BlobAssetReference<PresentationContentRegistryBlob>.Null
                });
                return;
            }

            buildList.Sort((a, b) => a.Id.CompareTo(b.Id));

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PresentationContentRegistryBlob>();
            var blobArray = builder.Allocate(ref root.Bindings, buildList.Count);
            for (int i = 0; i < buildList.Count; i++)
            {
                blobArray[i] = buildList[i].Binding;
            }

            var blob = builder.CreateBlobAssetReference<PresentationContentRegistryBlob>(Allocator.Persistent);
            builder.Dispose();
            AddBlobAsset(ref blob, out _);

            AddComponent(entity, new PresentationContentRegistryReference
            {
                Registry = blob
            });
        }
    }
}

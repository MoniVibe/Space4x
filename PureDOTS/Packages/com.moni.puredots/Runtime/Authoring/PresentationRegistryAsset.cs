using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [CreateAssetMenu(menuName = "PureDOTS/Presentation/Registry", fileName = "PresentationRegistry")]
    public sealed class PresentationRegistryAsset : ScriptableObject
    {
        [Tooltip("Ordered descriptor definitions that will be baked into a blob asset.")]
        public List<PresentationDescriptorDefinition> descriptors = new();
    }

    [Serializable]
    public sealed class PresentationDescriptorDefinition
    {
        [Header("Identification")]
        [Tooltip("Lowercase identifier referenced by spawners (e.g., villager.basic).")]
        public string descriptorKey;

        [Header("Prefab & Defaults")]
        [Tooltip("Prefab converted to an Entity for presentation. Should contain renderer + PlaceholderVisual components.")]
        public GameObject prefab;
        public Vector3 defaultOffset;
        [Min(0.01f)] public float defaultScale = 1f;
        public Color defaultTint = Color.white;
        [Tooltip("Baseline spawn flags applied when a descriptor is resolved.")]
        public PresentationSpawnFlags defaultFlags = PresentationSpawnFlags.AllowPooling;
    }

    [DisallowMultipleComponent]
    public sealed class PresentationRegistryAuthoring : MonoBehaviour
    {
        public PresentationRegistryAsset registry;
    }

    public sealed class PresentationRegistryAuthoringBaker : Baker<PresentationRegistryAuthoring>
    {
        private struct DescriptorBuildData
        {
            public Unity.Entities.Hash128 Hash;
            public PresentationDescriptor Descriptor;
        }

        public override void Bake(PresentationRegistryAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);

            if (authoring.registry == null || authoring.registry.descriptors == null || authoring.registry.descriptors.Count == 0)
            {
                AddComponent(entity, new PresentationRegistryReference
                {
                    Registry = BlobAssetReference<PresentationRegistryBlob>.Null
                });
                return;
            }

            var definitions = authoring.registry.descriptors;
            var buildList = new List<DescriptorBuildData>(definitions.Count);
            var seenHashes = new HashSet<Unity.Entities.Hash128>();

            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (!PresentationKeyUtility.TryParseKey(definition.descriptorKey, out var hash, out _))
                {
                    Debug.LogWarning($"PresentationDescriptorDefinition '{authoring.registry.name}' entry {i} has an invalid key.", authoring);
                    continue;
                }

                if (definition.prefab == null)
                {
                    Debug.LogWarning($"PresentationDescriptor '{definition.descriptorKey}' is missing a prefab reference.", authoring);
                    continue;
                }

                if (!seenHashes.Add(hash))
                {
                    Debug.LogWarning($"Duplicate presentation descriptor key '{definition.descriptorKey}' detected. Skipping duplicate.", authoring);
                    continue;
                }

                var prefabEntity = GetEntity(definition.prefab, TransformUsageFlags.Renderable | TransformUsageFlags.Dynamic);
                if (prefabEntity == Entity.Null)
                {
                    Debug.LogWarning($"Failed to convert prefab for descriptor '{definition.descriptorKey}'.", authoring);
                    continue;
                }

                var descriptor = new PresentationDescriptor
                {
                    KeyHash = hash,
                    Prefab = prefabEntity,
                    DefaultOffset = new float3(definition.defaultOffset.x, definition.defaultOffset.y, definition.defaultOffset.z),
                    DefaultScale = math.max(0.01f, definition.defaultScale),
                    DefaultTint = new float4(definition.defaultTint.r, definition.defaultTint.g, definition.defaultTint.b, definition.defaultTint.a),
                    DefaultFlags = definition.defaultFlags
                };

                buildList.Add(new DescriptorBuildData
                {
                    Hash = hash,
                    Descriptor = descriptor
                });
            }

            if (buildList.Count == 0)
            {
                AddComponent(entity, new PresentationRegistryReference
                {
                    Registry = BlobAssetReference<PresentationRegistryBlob>.Null
                });
                return;
            }

            buildList.Sort((a, b) => a.Hash.CompareTo(b.Hash));

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PresentationRegistryBlob>();
            var descriptorArray = builder.Allocate(ref root.Descriptors, buildList.Count);

            for (int i = 0; i < buildList.Count; i++)
            {
                descriptorArray[i] = buildList[i].Descriptor;
            }

            var blob = builder.CreateBlobAssetReference<PresentationRegistryBlob>(Allocator.Persistent);
            builder.Dispose();

            AddBlobAsset(ref blob, out _);

            AddComponent(entity, new PresentationRegistryReference
            {
                Registry = blob
            });
        }
    }
}


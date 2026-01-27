using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
#if UNITY_EDITOR
    [CreateAssetMenu(menuName = "PureDOTS/Registry/Registry Catalog", fileName = "RegistryCatalog")]
    public sealed class RegistryCatalogAsset : ScriptableObject
    {
        [Serializable]
        public sealed class RegistryEntryDefinition
        {
            [Tooltip("Stable ID for the registry (lowercase). Leave blank to derive from the display name.")]
            public string registryId;

            [Tooltip("Human-readable label surfaced to debug overlays.")]
            public string displayName;

            [Tooltip("Optional archetype hint for downstream systems. Uses ushort to mirror RegistryMetadata.ArchetypeId.")]
            public ushort archetypeId;

            [Header("Continuity")]
            [Tooltip("Schema version used for residency/category grouping.")]
            public uint continuityVersion = 1;
            public RegistryResidency residency = RegistryResidency.Runtime;
            public RegistryCategory category = RegistryCategory.Gameplay;

            [Header("Telemetry & Presentation")]
            [Tooltip("Telemetry key shared across registries (defaults to registryId).")]
            public string telemetryKey;

            [Tooltip("Hybrid prefab GUID used by presentation bridges when ECS mesh data is absent.")]
            public string hybridPrefabGuid;
        }

        public List<RegistryEntryDefinition> entries = new();

        private void OnValidate()
        {
            var seen = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                var id = string.IsNullOrWhiteSpace(entry.registryId)
                    ? entry.displayName ?? string.Empty
                    : entry.registryId;

                if (!seen.Add(id.Trim().ToLowerInvariant()))
                {
                    Debug.LogWarning($"Duplicate registry id detected in catalog '{name}': {id}", this);
                }
            }
        }
    }
#endif

    [DisallowMultipleComponent]
    public sealed class RegistryCatalogAuthoring : MonoBehaviour
    {
#if UNITY_EDITOR
        public RegistryCatalogAsset catalog;
#endif
    }

    public sealed class RegistryCatalogBaker : Baker<RegistryCatalogAuthoring>
    {
        public override void Bake(RegistryCatalogAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
#if UNITY_EDITOR
            if (authoring.catalog == null || authoring.catalog.entries == null || authoring.catalog.entries.Count == 0)
            {
                AddComponent(entity, new RegistryDefinitionCatalog
                {
                    Catalog = BlobAssetReference<RegistryDefinitionBlob>.Null
                });
                return;
            }

            var definitions = authoring.catalog.entries;
            var buildList = new List<RegistryDefinition>(definitions.Count);

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                var displayName = new FixedString64Bytes(definition.displayName ?? string.Empty);
                var idSource = string.IsNullOrWhiteSpace(definition.registryId)
                    ? definition.displayName ?? string.Empty
                    : definition.registryId;

                if (!RegistryId.TryParse(idSource, out var id, out _))
                {
                    Debug.LogWarning($"RegistryCatalog '{authoring.catalog.name}' entry {i} has an invalid id '{idSource}'. Skipping entry.", authoring);
                    continue;
                }

                var telemetry = RegistryTelemetryKey.FromString(definition.telemetryKey, id);
                var continuity = new RegistryContinuityMeta
                {
                    SchemaVersion = definition.continuityVersion,
                    Residency = definition.residency,
                    Category = definition.category
                }.WithDefaultsIfUnset();

                Unity.Entities.Hash128 hybridGuid = default;
                if (!string.IsNullOrWhiteSpace(definition.hybridPrefabGuid))
                {
                    try
                    {
                        hybridGuid = new Unity.Entities.Hash128(definition.hybridPrefabGuid.Trim());
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning($"RegistryCatalog '{authoring.catalog.name}' entry {idSource} has an invalid Hybrid Prefab GUID.", authoring);
                    }
                }

                buildList.Add(new RegistryDefinition
                {
                    Id = id,
                    DisplayName = displayName,
                    ArchetypeId = definition.archetypeId,
                    TelemetryKey = telemetry,
                    Continuity = continuity,
                    HybridPrefabGuid = hybridGuid
                });
            }

            if (buildList.Count == 0)
            {
                AddComponent(entity, new RegistryDefinitionCatalog
                {
                    Catalog = BlobAssetReference<RegistryDefinitionBlob>.Null
                });
                return;
            }

            buildList.Sort((a, b) => a.Id.CompareTo(b.Id));

            var tempArray = new NativeArray<RegistryDefinition>(buildList.Count, Allocator.Temp);
            for (var i = 0; i < buildList.Count; i++)
            {
                tempArray[i] = buildList[i];
            }

            var summary = RegistryContinuityValidator.Validate(tempArray);
            if (!summary.IsValid)
            {
                Debug.LogError($"RegistryCatalog '{authoring.catalog.name}' failed continuity validation (duplicates={summary.DuplicateIdCount}, invalidIds={summary.InvalidIdCount}, versionMismatch={summary.VersionMismatchCount}, residencyMismatch={summary.ResidencyMismatchCount}).", authoring);
            }
            tempArray.Dispose();

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<RegistryDefinitionBlob>();
            var blobArray = builder.Allocate(ref root.Definitions, buildList.Count);
            for (var i = 0; i < buildList.Count; i++)
            {
                blobArray[i] = buildList[i];
            }

            var blob = builder.CreateBlobAssetReference<RegistryDefinitionBlob>(Allocator.Persistent);
            builder.Dispose();
            AddBlobAsset(ref blob, out _);

            AddComponent(entity, new RegistryDefinitionCatalog
            {
                Catalog = blob
            });
#else
            AddComponent(entity, new RegistryDefinitionCatalog
            {
                Catalog = BlobAssetReference<RegistryDefinitionBlob>.Null
            });
#endif
        }
    }
}

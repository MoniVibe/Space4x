using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Alignment;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
#if UNITY_EDITOR
    [CreateAssetMenu(menuName = "PureDOTS/Alignment/DoctrineCatalog", fileName = "DoctrineCatalog")]
    public sealed class DoctrineAuthoring : ScriptableObject
    {
        [Serializable]
        public sealed class DoctrineDefinitionEntry
        {
            public string doctrineId;
            public AffiliationKind kind = AffiliationKind.Faction;
            [Range(-1f, 1f)] public float authorityAffinity;
            [Range(-1f, 1f)] public float militaryAffinity;
            [Range(-1f, 1f)] public float economicAffinity;
            [Range(-1f, 1f)] public float toleranceAffinity;
            [Range(-1f, 1f)] public float expansionAffinity;
            [Range(0f, 2f)] public float fanaticismCap = 1f;
        }

        public List<DoctrineDefinitionEntry> entries = new();

        private void OnValidate()
        {
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                entry.doctrineId = SanitizeId(entry.doctrineId);
                entry.fanaticismCap = Mathf.Clamp(entry.fanaticismCap, 0f, 2f);
                entry.authorityAffinity = Mathf.Clamp(entry.authorityAffinity, -1f, 1f);
                entry.militaryAffinity = Mathf.Clamp(entry.militaryAffinity, -1f, 1f);
                entry.economicAffinity = Mathf.Clamp(entry.economicAffinity, -1f, 1f);
                entry.toleranceAffinity = Mathf.Clamp(entry.toleranceAffinity, -1f, 1f);
                entry.expansionAffinity = Mathf.Clamp(entry.expansionAffinity, -1f, 1f);

                if (string.IsNullOrEmpty(entry.doctrineId))
                {
                    Debug.LogWarning($"Doctrine entry at index {i} has an empty id and will be ignored during baking.", this);
                    continue;
                }

                if (!seenIds.Add(entry.doctrineId))
                {
                    Debug.LogWarning($"Duplicate doctrine id \"{entry.doctrineId}\" detected; later duplicates will be skipped when building the catalog.", this);
                }
            }
        }

        private static string SanitizeId(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToLowerInvariant();
        }
    }
#endif

    [DisallowMultipleComponent]
    public sealed class DoctrineCatalogAuthoring : MonoBehaviour
    {
#if UNITY_EDITOR
        public DoctrineAuthoring catalog;
#endif
    }

    public sealed class DoctrineCatalogBaker : Baker<DoctrineCatalogAuthoring>
    {
        public override void Bake(DoctrineCatalogAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
#if UNITY_EDITOR
            if (authoring.catalog == null || authoring.catalog.entries == null || authoring.catalog.entries.Count == 0)
            {
                AddComponent(entity, new DoctrineCatalog { Catalog = BlobAssetReference<DoctrineCatalogBlob>.Null });
                return;
            }

            var definitions = authoring.catalog.entries;
            var buildList = new List<DoctrineDefinition>(definitions.Count);
            var seenIds = new HashSet<DoctrineId>();

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                var doctrineId = DoctrineId.FromString(definition.doctrineId);
                if (doctrineId.Value.Length == 0)
                {
                    Debug.LogWarning($"Doctrine entry {i} has an empty id.", authoring);
                    continue;
                }

                if (!seenIds.Add(doctrineId))
                {
                    Debug.LogWarning($"Doctrine id \"{definition.doctrineId}\" already exists; skipping duplicate at index {i}.", authoring);
                    continue;
                }

                buildList.Add(new DoctrineDefinition
                {
                    Id = doctrineId,
                    Kind = definition.kind,
                    AuthorityAffinity = definition.authorityAffinity,
                    MilitaryAffinity = definition.militaryAffinity,
                    EconomicAffinity = definition.economicAffinity,
                    ToleranceAffinity = definition.toleranceAffinity,
                    ExpansionAffinity = definition.expansionAffinity,
                    FanaticismCap = definition.fanaticismCap
                });
            }

            if (buildList.Count == 0)
            {
                AddComponent(entity, new DoctrineCatalog { Catalog = BlobAssetReference<DoctrineCatalogBlob>.Null });
                return;
            }

            buildList.Sort((a, b) => string.Compare(a.Id.Value.ToString(), b.Id.Value.ToString(), StringComparison.Ordinal));

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<DoctrineCatalogBlob>();
            var blobArray = builder.Allocate(ref root.Definitions, buildList.Count);
            for (var i = 0; i < buildList.Count; i++)
            {
                blobArray[i] = buildList[i];
            }

            var blob = builder.CreateBlobAssetReference<DoctrineCatalogBlob>(Allocator.Persistent);
            builder.Dispose();
            AddBlobAsset(ref blob, out _);

            AddComponent(entity, new DoctrineCatalog { Catalog = blob });
#else
            AddComponent(entity, new DoctrineCatalog { Catalog = BlobAssetReference<DoctrineCatalogBlob>.Null });
#endif
        }
    }
}

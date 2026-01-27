using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Resource;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Entities.Hybrid;
#endif

namespace PureDOTS.Authoring.Resource
{
    [CreateAssetMenu(fileName = "DepositCatalog", menuName = "PureDOTS/Space4X/Deposit Catalog")]
    public sealed class DepositCatalogAsset : ScriptableObject
    {
        public List<DepositCatalogEntry> Entries = new();
    }

    [Serializable]
    public struct DepositCatalogEntry
    {
        public string DepositId;
        public string ResourceId;
        public float Richness;
        public float DepletionPerWork;
        public float Hardness;
    }

    /// <summary>
    /// Authoring component for deposit catalog.
    /// </summary>
    public sealed class DepositCatalogAuthoring : MonoBehaviour
    {
        public DepositCatalogAsset Catalog;
    }

#if UNITY_EDITOR
    public sealed class DepositCatalogBaker : Baker<DepositCatalogAuthoring>
    {
        public override void Bake(DepositCatalogAuthoring authoring)
        {
            if (authoring.Catalog == null || authoring.Catalog.Entries == null || authoring.Catalog.Entries.Count == 0)
            {
                Debug.LogWarning("[DepositCatalogBaker] No catalog assigned or empty entries; skipping blob creation.");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<DepositCatalogBlob>();
            var array = builder.Allocate(ref root.Deposits, authoring.Catalog.Entries.Count);

            for (int i = 0; i < authoring.Catalog.Entries.Count; i++)
            {
                var entry = authoring.Catalog.Entries[i];
                var id = string.IsNullOrWhiteSpace(entry.DepositId) ? $"deposit.{i}" : entry.DepositId.Trim().ToLowerInvariant();
                var resourceId = string.IsNullOrWhiteSpace(entry.ResourceId) ? default : new FixedString32Bytes(entry.ResourceId.Trim().ToLowerInvariant());

                array[i] = new DepositSpec
                {
                    Id = new FixedString32Bytes(id),
                    ResourceId = resourceId,
                    Richness = entry.Richness,
                    DepletionPerWork = entry.DepletionPerWork,
                    Hardness = entry.Hardness
                };
            }

            var blob = builder.CreateBlobAssetReference<DepositCatalogBlob>(Allocator.Persistent);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new DepositCatalog { Catalog = blob });
        }
    }
#endif
}


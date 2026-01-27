using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Crew;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Entities.Hybrid;
#endif

namespace PureDOTS.Authoring.Crew
{
    [CreateAssetMenu(fileName = "CrewCatalog", menuName = "PureDOTS/Space4X/Crew Catalog")]
    public sealed class CrewCatalogAsset : ScriptableObject
    {
        public List<CrewCatalogEntry> Entries = new();
    }

    [Serializable]
    public struct CrewCatalogEntry
    {
        public string CrewSpecId;
        public CrewRole Role;
        public float XpPerAction;
        public float FatiguePerHour;
        public float RepairMultPerLvl;
        public float AccuracyMultPerLvl;
    }

    /// <summary>
    /// Authoring component for crew catalog.
    /// </summary>
    public sealed class CrewCatalogAuthoring : MonoBehaviour
    {
        public CrewCatalogAsset Catalog;
    }

#if UNITY_EDITOR
    public sealed class CrewCatalogBaker : Baker<CrewCatalogAuthoring>
    {
        public override void Bake(CrewCatalogAuthoring authoring)
        {
            if (authoring.Catalog == null || authoring.Catalog.Entries == null || authoring.Catalog.Entries.Count == 0)
            {
                Debug.LogWarning("[CrewCatalogBaker] No catalog assigned or empty entries; skipping blob creation.");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CrewCatalogBlob>();
            var array = builder.Allocate(ref root.CrewSpecs, authoring.Catalog.Entries.Count);

            for (int i = 0; i < authoring.Catalog.Entries.Count; i++)
            {
                var entry = authoring.Catalog.Entries[i];
                var id = string.IsNullOrWhiteSpace(entry.CrewSpecId) ? $"crew.{i}" : entry.CrewSpecId.Trim().ToLowerInvariant();

                array[i] = new CrewSpec
                {
                    Id = new FixedString32Bytes(id),
                    Role = (byte)entry.Role,
                    XpPerAction = entry.XpPerAction,
                    FatiguePerHour = entry.FatiguePerHour,
                    RepairMultPerLvl = entry.RepairMultPerLvl,
                    AccuracyMultPerLvl = entry.AccuracyMultPerLvl
                };
            }

            var blob = builder.CreateBlobAssetReference<CrewCatalogBlob>(Allocator.Persistent);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new CrewCatalog { Catalog = blob });
        }
    }
#endif
}


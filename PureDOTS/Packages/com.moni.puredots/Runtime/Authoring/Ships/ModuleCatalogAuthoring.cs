using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Ships;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Ships
{
    [CreateAssetMenu(fileName = "ModuleCatalog", menuName = "PureDOTS/Space4X/Module Catalog")]
    public sealed class ModuleCatalogAsset : ScriptableObject
    {
        public List<ModuleCatalogEntry> Entries = new();
    }

    [Serializable]
    public struct ModuleCatalogEntry
    {
        public string ModuleId;
        public ModuleFamily Family;
        public ModuleClass Class;
        public MountType RequiredMount;
        public MountSize RequiredSize;
        public float Mass;
        public float PowerRequired;
        public float OffenseRating;
        public float DefenseRating;
        public float UtilityRating;
        [Range(0, 200)] public byte EfficiencyPercent;
    }

    /// <summary>
    /// Bakes a module catalog asset into a blob that runtime systems can reference.
    /// </summary>
    public sealed class ModuleCatalogAuthoring : MonoBehaviour
    {
        public ModuleCatalogAsset Catalog;
    }

    public sealed class ModuleCatalogBaker : Baker<ModuleCatalogAuthoring>
    {
        public override void Bake(ModuleCatalogAuthoring authoring)
        {
            if (authoring.Catalog == null || authoring.Catalog.Entries == null || authoring.Catalog.Entries.Count == 0)
            {
                Debug.LogWarning("[ModuleCatalogBaker] No catalog assigned or empty entries; skipping blob creation.");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ModuleCatalogBlob>();
            var array = builder.Allocate(ref root.Definitions, authoring.Catalog.Entries.Count);

            for (int i = 0; i < authoring.Catalog.Entries.Count; i++)
            {
                var entry = authoring.Catalog.Entries[i];
                var id = string.IsNullOrWhiteSpace(entry.ModuleId) ? $"module.{i}" : entry.ModuleId.Trim().ToLowerInvariant();

                array[i] = new ModuleDefinition
                {
                    ModuleId = new FixedString64Bytes(id),
                    Family = entry.Family,
                    Class = entry.Class,
                    RequiredMount = entry.RequiredMount,
                    RequiredSize = entry.RequiredSize,
                    Mass = entry.Mass,
                    PowerRequired = entry.PowerRequired,
                    OffenseRating = entry.OffenseRating,
                    DefenseRating = entry.DefenseRating,
                    UtilityRating = entry.UtilityRating,
                    EfficiencyPercent = entry.EfficiencyPercent
                };
            }

            var blob = builder.CreateBlobAssetReference<ModuleCatalogBlob>(Allocator.Persistent);
            AddComponent(entity, new ModuleCatalog { Catalog = blob });
        }
    }
}

using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Combat;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Entities.Hybrid;
#endif

namespace PureDOTS.Authoring.Combat
{
    [CreateAssetMenu(fileName = "WeaponCatalog", menuName = "PureDOTS/Space4X/Weapon Catalog")]
    public sealed class WeaponCatalogAsset : ScriptableObject
    {
        public List<WeaponCatalogEntry> Entries = new();
    }

    [Serializable]
    public struct WeaponCatalogEntry
    {
        public string WeaponId;
        public WeaponClass Class;
        public float FireRate;
        public byte Burst;
        public float SpreadDeg;
        public float EnergyCost;
        public float HeatCost;
        public string ProjectileId;
    }

    /// <summary>
    /// Authoring component for weapon catalog.
    /// </summary>
    public sealed class WeaponCatalogAuthoring : MonoBehaviour
    {
        public WeaponCatalogAsset Catalog;
    }

#if UNITY_EDITOR
    public sealed class WeaponCatalogBaker : Baker<WeaponCatalogAuthoring>
    {
        public override void Bake(WeaponCatalogAuthoring authoring)
        {
            if (authoring.Catalog == null || authoring.Catalog.Entries == null || authoring.Catalog.Entries.Count == 0)
            {
                Debug.LogWarning("[WeaponCatalogBaker] No catalog assigned or empty entries; skipping blob creation.");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<WeaponCatalogBlob>();
            var array = builder.Allocate(ref root.Weapons, authoring.Catalog.Entries.Count);

            for (int i = 0; i < authoring.Catalog.Entries.Count; i++)
            {
                var entry = authoring.Catalog.Entries[i];
                var id = string.IsNullOrWhiteSpace(entry.WeaponId) ? $"weapon.{i}" : entry.WeaponId.Trim().ToLowerInvariant();
                var projectileId = string.IsNullOrWhiteSpace(entry.ProjectileId) ? default : new FixedString32Bytes(entry.ProjectileId.Trim().ToLowerInvariant());

                array[i] = new WeaponSpec
                {
                    Id = new FixedString64Bytes(id),
                    Class = (byte)entry.Class,
                    FireRate = entry.FireRate,
                    Burst = entry.Burst,
                    SpreadDeg = entry.SpreadDeg,
                    EnergyCost = entry.EnergyCost,
                    HeatCost = entry.HeatCost,
                    ProjectileId = projectileId
                };
            }

            var blob = builder.CreateBlobAssetReference<WeaponCatalogBlob>(Allocator.Persistent);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new WeaponCatalog { Catalog = blob });
        }
    }
#endif
}


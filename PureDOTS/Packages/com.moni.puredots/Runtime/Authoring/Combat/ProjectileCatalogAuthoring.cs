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
    [CreateAssetMenu(fileName = "ProjectileCatalog", menuName = "PureDOTS/Space4X/Projectile Catalog")]
    public sealed class ProjectileCatalogAsset : ScriptableObject
    {
        public List<ProjectileCatalogEntry> Entries = new();
    }

    [Serializable]
    public struct ProjectileCatalogEntry
    {
        public string ProjectileId;
        public ProjectileKind Kind;
        public float Speed;
        public float Lifetime;
        public float TurnRateDeg;
        public float SeekRadius;
        public float AoERadius;
        public float Pierce;
        public DamageModelEntry Damage;
    }

    [Serializable]
    public struct DamageModelEntry
    {
        public float BaseDamage;
        public float ShieldMultiplier;
        public float ArmorMultiplier;
        public float HullMultiplier;
    }

    /// <summary>
    /// Authoring component for projectile catalog.
    /// </summary>
    public sealed class ProjectileCatalogAuthoring : MonoBehaviour
    {
        public ProjectileCatalogAsset Catalog;
    }

#if UNITY_EDITOR
    public sealed class ProjectileCatalogBaker : Baker<ProjectileCatalogAuthoring>
    {
        public override void Bake(ProjectileCatalogAuthoring authoring)
        {
            if (authoring.Catalog == null || authoring.Catalog.Entries == null || authoring.Catalog.Entries.Count == 0)
            {
                Debug.LogWarning("[ProjectileCatalogBaker] No catalog assigned or empty entries; skipping blob creation.");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ProjectileCatalogBlob>();
            var array = builder.Allocate(ref root.Projectiles, authoring.Catalog.Entries.Count);

            for (int i = 0; i < authoring.Catalog.Entries.Count; i++)
            {
                var entry = authoring.Catalog.Entries[i];
                var id = string.IsNullOrWhiteSpace(entry.ProjectileId) ? $"projectile.{i}" : entry.ProjectileId.Trim().ToLowerInvariant();

                ref var spec = ref array[i];
                spec = new ProjectileSpec
                {
                    Id = new FixedString64Bytes(id),
                    Kind = (byte)entry.Kind,
                    Speed = entry.Speed,
                    Lifetime = entry.Lifetime,
                    TurnRateDeg = entry.TurnRateDeg,
                    SeekRadius = entry.SeekRadius,
                    AoERadius = entry.AoERadius,
                    Pierce = entry.Pierce,
                    Damage = new DamageModel
                    {
                        BaseDamage = entry.Damage.BaseDamage,
                        ShieldMultiplier = entry.Damage.ShieldMultiplier,
                        ArmorMultiplier = entry.Damage.ArmorMultiplier,
                        HullMultiplier = entry.Damage.HullMultiplier
                    }
                };
                ProjectileSpecSanitizer.Sanitize(ref spec);
            }

            var blob = builder.CreateBlobAssetReference<ProjectileCatalogBlob>(Allocator.Persistent);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new ProjectileCatalog { Catalog = blob });
        }
    }
#endif
}

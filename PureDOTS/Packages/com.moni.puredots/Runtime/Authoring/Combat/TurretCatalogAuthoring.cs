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
    [CreateAssetMenu(fileName = "TurretCatalog", menuName = "PureDOTS/Space4X/Turret Catalog")]
    public sealed class TurretCatalogAsset : ScriptableObject
    {
        public List<TurretCatalogEntry> Entries = new();
    }

    [Serializable]
    public struct TurretCatalogEntry
    {
        public string TurretId;
        public float TraverseDegPerS;
        public float ElevDegPerS;
        public float ArcYawDeg;
        public float ArcPitchDeg;
        public string MuzzleSocket;
    }

    /// <summary>
    /// Authoring component for turret catalog.
    /// </summary>
    public sealed class TurretCatalogAuthoring : MonoBehaviour
    {
        public TurretCatalogAsset Catalog;
    }

#if UNITY_EDITOR
    public sealed class TurretCatalogBaker : Baker<TurretCatalogAuthoring>
    {
        public override void Bake(TurretCatalogAuthoring authoring)
        {
            if (authoring.Catalog == null || authoring.Catalog.Entries == null || authoring.Catalog.Entries.Count == 0)
            {
                Debug.LogWarning("[TurretCatalogBaker] No catalog assigned or empty entries; skipping blob creation.");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TurretCatalogBlob>();
            var array = builder.Allocate(ref root.Turrets, authoring.Catalog.Entries.Count);

            for (int i = 0; i < authoring.Catalog.Entries.Count; i++)
            {
                var entry = authoring.Catalog.Entries[i];
                var id = string.IsNullOrWhiteSpace(entry.TurretId) ? $"turret.{i}" : entry.TurretId.Trim().ToLowerInvariant();
                var muzzleSocket = string.IsNullOrWhiteSpace(entry.MuzzleSocket) ? default : new FixedString32Bytes(entry.MuzzleSocket.Trim());

                array[i] = new TurretSpec
                {
                    Id = new FixedString32Bytes(id),
                    TraverseDegPerS = entry.TraverseDegPerS,
                    ElevDegPerS = entry.ElevDegPerS,
                    ArcYawDeg = entry.ArcYawDeg,
                    ArcPitchDeg = entry.ArcPitchDeg,
                    MuzzleSocket = muzzleSocket
                };
            }

            var blob = builder.CreateBlobAssetReference<TurretCatalogBlob>(Allocator.Persistent);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new TurretCatalog { Catalog = blob });
        }
    }
#endif
}


using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Weapon Catalog")]
    public sealed class WeaponCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class WeaponSpecData
        {
            public string id;
            public WeaponClass weaponClass;
            [Header("Firing")]
            [Tooltip("Shots per second")]
            [Min(0f)] public float fireRate = 1f;
            [Tooltip("Burst count (1..N)")]
            [Range(1, 10)] public byte burstCount = 1;
            [Tooltip("Cone spread in degrees")]
            [Min(0f)] public float spreadDeg = 0f;
            [Header("Costs")]
            [Min(0f)] public float energyCost = 0f;
            [Min(0f)] public float heatCost = 0f;
            [Header("Targeting")]
            [Tooltip("Lead bias (0..1, aiming hint)")]
            [Range(0f, 1f)] public float leadBias = 0.5f;
            [Header("Projectile")]
            [Tooltip("Projectile ID (references ProjectileCatalog)")]
            public string projectileId = string.Empty;
        }

        public List<WeaponSpecData> weapons = new List<WeaponSpecData>();

        public sealed class Baker : Unity.Entities.Baker<WeaponCatalogAuthoring>
        {
            public override void Bake(WeaponCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.weapons == null || authoring.weapons.Count == 0)
                {
                    UnityDebug.LogWarning("WeaponCatalogAuthoring has no weapons defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<WeaponCatalogBlob>();
                var weaponArray = builder.Allocate(ref catalogBlob.Weapons, authoring.weapons.Count);

                for (int i = 0; i < authoring.weapons.Count; i++)
                {
                    var weaponData = authoring.weapons[i];
                    weaponArray[i] = new WeaponSpec
                    {
                        Id = new FixedString64Bytes(weaponData.id ?? string.Empty),
                        Class = weaponData.weaponClass,
                        FireRate = math.max(0f, weaponData.fireRate),
                        BurstCount = (byte)math.clamp(weaponData.burstCount, 1, 10),
                        SpreadDeg = math.max(0f, weaponData.spreadDeg),
                        EnergyCost = math.max(0f, weaponData.energyCost),
                        HeatCost = math.max(0f, weaponData.heatCost),
                        LeadBias = math.clamp(weaponData.leadBias, 0f, 1f),
                        ProjectileId = new FixedString32Bytes(weaponData.projectileId ?? string.Empty)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<WeaponCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new WeaponCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}


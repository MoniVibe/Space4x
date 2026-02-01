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
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Module Catalogs/Weapon Module Catalog")]
    public sealed class Space4XWeaponModuleCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class WeaponModuleSpecData
        {
            public string moduleId;
            public string weaponId;
            [Range(0f, 360f)] public float fireArcDegrees = 180f;
            [Range(-180f, 180f)] public float fireArcOffsetDeg;
            [Range(0f, 1f)] public float accuracyBonus;
            [Range(0f, 1f)] public float trackingBonus;
        }

        public List<WeaponModuleSpecData> modules = new List<WeaponModuleSpecData>();

        public sealed class Baker : Unity.Entities.Baker<Space4XWeaponModuleCatalogAuthoring>
        {
            public override void Bake(Space4XWeaponModuleCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.modules == null || authoring.modules.Count == 0)
                {
                    UnityDebug.LogWarning("Space4XWeaponModuleCatalogAuthoring has no modules defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<WeaponModuleCatalogBlob>();
                var array = builder.Allocate(ref root.Modules, authoring.modules.Count);

                for (int i = 0; i < authoring.modules.Count; i++)
                {
                    var data = authoring.modules[i];
                    array[i] = new WeaponModuleSpec
                    {
                        ModuleId = new FixedString64Bytes(data.moduleId ?? string.Empty),
                        WeaponId = new FixedString64Bytes(data.weaponId ?? string.Empty),
                        FireArcDegrees = math.clamp(data.fireArcDegrees, 0f, 360f),
                        FireArcOffsetDeg = math.clamp(data.fireArcOffsetDeg, -180f, 180f),
                        AccuracyBonus = math.saturate(data.accuracyBonus),
                        TrackingBonus = math.saturate(data.trackingBonus)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<WeaponModuleCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new WeaponModuleCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

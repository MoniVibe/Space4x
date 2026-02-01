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
    [AddComponentMenu("Space4X/Module Catalogs/Shield Module Catalog")]
    public sealed class Space4XShieldModuleCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class ShieldModuleSpecData
        {
            public string moduleId;
            [Min(0f)] public float capacity;
            [Min(0f)] public float rechargePerSecond;
            [Min(0f)] public float regenDelaySeconds;
            [Range(0f, 360f)] public float arcDegrees = 360f;
            [Range(0f, 1f)] public float kineticResist;
            [Range(0f, 1f)] public float energyResist;
            [Range(0f, 1f)] public float explosiveResist;
        }

        public List<ShieldModuleSpecData> modules = new List<ShieldModuleSpecData>();

        public sealed class Baker : Unity.Entities.Baker<Space4XShieldModuleCatalogAuthoring>
        {
            public override void Bake(Space4XShieldModuleCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.modules == null || authoring.modules.Count == 0)
                {
                    UnityDebug.LogWarning("Space4XShieldModuleCatalogAuthoring has no modules defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<ShieldModuleCatalogBlob>();
                var array = builder.Allocate(ref root.Modules, authoring.modules.Count);

                for (int i = 0; i < authoring.modules.Count; i++)
                {
                    var data = authoring.modules[i];
                    array[i] = new ShieldModuleSpec
                    {
                        ModuleId = new FixedString64Bytes(data.moduleId ?? string.Empty),
                        Capacity = math.max(0f, data.capacity),
                        RechargePerSecond = math.max(0f, data.rechargePerSecond),
                        RegenDelaySeconds = math.max(0f, data.regenDelaySeconds),
                        ArcDegrees = math.clamp(data.arcDegrees, 0f, 360f),
                        KineticResist = math.saturate(data.kineticResist),
                        EnergyResist = math.saturate(data.energyResist),
                        ExplosiveResist = math.saturate(data.explosiveResist)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<ShieldModuleCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ShieldModuleCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

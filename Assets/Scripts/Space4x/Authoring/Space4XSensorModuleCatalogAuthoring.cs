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
    [AddComponentMenu("Space4X/Module Catalogs/Sensor Module Catalog")]
    public sealed class Space4XSensorModuleCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class SensorModuleSpecData
        {
            public string moduleId;
            [Min(0f)] public float range;
            [Min(0f)] public float refreshSeconds = 1f;
            [Min(0f)] public float resolution = 1f;
            [Range(0f, 1f)] public float jamResistance;
            [Range(0f, 1f)] public float passiveSignature = 0.5f;
        }

        public List<SensorModuleSpecData> modules = new List<SensorModuleSpecData>();

        public sealed class Baker : Unity.Entities.Baker<Space4XSensorModuleCatalogAuthoring>
        {
            public override void Bake(Space4XSensorModuleCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.modules == null || authoring.modules.Count == 0)
                {
                    UnityDebug.LogWarning("Space4XSensorModuleCatalogAuthoring has no modules defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<SensorModuleCatalogBlob>();
                var array = builder.Allocate(ref root.Modules, authoring.modules.Count);

                for (int i = 0; i < authoring.modules.Count; i++)
                {
                    var data = authoring.modules[i];
                    array[i] = new SensorModuleSpec
                    {
                        ModuleId = new FixedString64Bytes(data.moduleId ?? string.Empty),
                        Range = math.max(0f, data.range),
                        RefreshSeconds = math.max(0.01f, data.refreshSeconds),
                        Resolution = math.max(0f, data.resolution),
                        JamResistance = math.saturate(data.jamResistance),
                        PassiveSignature = math.saturate(data.passiveSignature)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<SensorModuleCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SensorModuleCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

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
    [AddComponentMenu("Space4X/Module Catalogs/Bridge Module Catalog")]
    public sealed class Space4XBridgeModuleCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class BridgeModuleSpecData
        {
            public string moduleId;
            [Range(0f, 1f)] public float techLevel;
        }

        public List<BridgeModuleSpecData> modules = new List<BridgeModuleSpecData>();

        public sealed class Baker : Unity.Entities.Baker<Space4XBridgeModuleCatalogAuthoring>
        {
            public override void Bake(Space4XBridgeModuleCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.modules == null || authoring.modules.Count == 0)
                {
                    UnityDebug.LogWarning("Space4XBridgeModuleCatalogAuthoring has no modules defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<BridgeModuleCatalogBlob>();
                var array = builder.Allocate(ref root.Modules, authoring.modules.Count);

                for (int i = 0; i < authoring.modules.Count; i++)
                {
                    var data = authoring.modules[i];
                    array[i] = new BridgeModuleSpec
                    {
                        ModuleId = new FixedString64Bytes(data.moduleId ?? string.Empty),
                        TechLevel = math.saturate(data.techLevel)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<BridgeModuleCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BridgeModuleCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

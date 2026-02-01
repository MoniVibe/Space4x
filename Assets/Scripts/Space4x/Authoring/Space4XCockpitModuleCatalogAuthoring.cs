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
    [AddComponentMenu("Space4X/Module Catalogs/Cockpit Module Catalog")]
    public sealed class Space4XCockpitModuleCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class CockpitModuleSpecData
        {
            public string moduleId;
            [Range(0f, 1f)] public float navigationCohesion;
        }

        public List<CockpitModuleSpecData> modules = new List<CockpitModuleSpecData>();

        public sealed class Baker : Unity.Entities.Baker<Space4XCockpitModuleCatalogAuthoring>
        {
            public override void Bake(Space4XCockpitModuleCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.modules == null || authoring.modules.Count == 0)
                {
                    UnityDebug.LogWarning("Space4XCockpitModuleCatalogAuthoring has no modules defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<CockpitModuleCatalogBlob>();
                var array = builder.Allocate(ref root.Modules, authoring.modules.Count);

                for (int i = 0; i < authoring.modules.Count; i++)
                {
                    var data = authoring.modules[i];
                    array[i] = new CockpitModuleSpec
                    {
                        ModuleId = new FixedString64Bytes(data.moduleId ?? string.Empty),
                        NavigationCohesion = math.saturate(data.navigationCohesion)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<CockpitModuleCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CockpitModuleCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

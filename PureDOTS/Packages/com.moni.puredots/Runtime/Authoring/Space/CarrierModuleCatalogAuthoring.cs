#if UNITY_EDITOR
using System;
using PureDOTS.Runtime.Space;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Space
{
    [DisallowMultipleComponent]
    public sealed class CarrierModuleCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct ModuleDefinition
        {
            public string moduleId;
            public ModuleFamily family;
            public ModuleClass moduleClass;
            public MountType requiredMount;
            public MountSize requiredSize;
            public float mass;
            public float powerDraw;
            public float powerGeneration;
        }

        public ModuleDefinition[] modules = Array.Empty<ModuleDefinition>();
    }

    public sealed class CarrierModuleCatalogBaker : Baker<CarrierModuleCatalogAuthoring>
    {
        public override void Bake(CarrierModuleCatalogAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);

            var builder = new BlobBuilder(Allocator.Temp);
            ref var catalog = ref builder.ConstructRoot<CarrierModuleCatalogBlob>();

            var sourceModules = authoring.modules ?? Array.Empty<CarrierModuleCatalogAuthoring.ModuleDefinition>();
            var blobModules = builder.Allocate(ref catalog.Modules, sourceModules.Length);

            for (int i = 0; i < sourceModules.Length; i++)
            {
                ref var target = ref blobModules[i];
                var source = sourceModules[i];

                builder.AllocateString(ref target.ModuleId, string.IsNullOrWhiteSpace(source.moduleId)
                    ? source.moduleClass.ToString()
                    : source.moduleId.Trim());
                target.Family = source.family;
                target.Class = source.moduleClass;
                target.RequiredMount = source.requiredMount;
                target.RequiredSize = source.requiredSize;
                target.Mass = math.max(0f, source.mass);
                target.PowerDraw = math.max(0f, source.powerDraw);
                target.PowerGeneration = math.max(0f, source.powerGeneration);
            }

            var blobAsset = builder.CreateBlobAssetReference<CarrierModuleCatalogBlob>(Allocator.Persistent);
            builder.Dispose();
            AddComponent(entity, new CarrierModuleCatalog { Catalog = blobAsset });
        }
    }
}
#endif

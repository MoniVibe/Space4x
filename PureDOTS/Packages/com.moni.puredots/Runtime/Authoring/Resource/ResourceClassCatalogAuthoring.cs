using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Resource;

namespace PureDOTS.Authoring.Resource
{
    [DisallowMultipleComponent]
    public sealed class ResourceClassCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string resourceTypeId;
            public ResourceClass resourceClass;
        }

        public Entry[] entries = Array.Empty<Entry>();
    }

    public sealed class ResourceClassCatalogBaker : Baker<ResourceClassCatalogAuthoring>
    {
        public override void Bake(ResourceClassCatalogAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent<ResourceClassCatalogTag>(entity);
            var buffer = AddBuffer<ResourceClassEntry>(entity);

            foreach (var entry in authoring.entries)
            {
                if (string.IsNullOrWhiteSpace(entry.resourceTypeId))
                {
                    continue;
                }

                buffer.Add(new ResourceClassEntry
                {
                    ResourceTypeId = new FixedString64Bytes(entry.resourceTypeId.Trim()),
                    Class = entry.resourceClass
                });
            }
        }
    }
}

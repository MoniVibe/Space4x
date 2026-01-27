#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Space;

namespace PureDOTS.Authoring.Space
{
    [DisallowMultipleComponent]
    public sealed class ResourceValueCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string resourceTypeId;
            public float baseValue;
        }

        public Entry[] entries = Array.Empty<Entry>();
    }

    public sealed class ResourceValueCatalogBaker : Baker<ResourceValueCatalogAuthoring>
    {
        public override void Bake(ResourceValueCatalogAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent<ResourceValueCatalogTag>(entity);
            var buffer = AddBuffer<ResourceValueEntry>(entity);
            foreach (var entry in authoring.entries)
            {
                if (string.IsNullOrWhiteSpace(entry.resourceTypeId))
                {
                    continue;
                }

                buffer.Add(new ResourceValueEntry
                {
                    ResourceTypeId = new FixedString64Bytes(entry.resourceTypeId.Trim()),
                    BaseValue = Mathf.Max(0f, entry.baseValue)
                });
            }
        }
    }
}
#endif

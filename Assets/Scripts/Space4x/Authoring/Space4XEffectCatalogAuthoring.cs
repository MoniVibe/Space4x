using System;
using Space4X.Presentation;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Effect Catalog")]
    public sealed class Space4XEffectCatalogAuthoring : MonoBehaviour
    {
        [SerializeField] private EffectEntry[] entries = Array.Empty<EffectEntry>();

        [Serializable]
        public struct EffectEntry
        {
            public string EffectId;
            public GameObject Prefab;
            public Space4XEffectBindingMode BindingMode;
            public Space4XEffectFollowMode FollowMode;
            public Vector3 FollowOffset;
            public float LifetimeOverride;
            public string IntensityProperty;
            public string DirectionProperty;
        }

        private sealed class Baker : Baker<Space4XEffectCatalogAuthoring>
        {
            public override void Bake(Space4XEffectCatalogAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var source = authoring.entries ?? Array.Empty<EffectEntry>();
                var catalogEntries = new Space4XEffectCatalogEntry[source.Length];
                var count = 0;

                for (int i = 0; i < source.Length; i++)
                {
                    var entry = source[i];
                    if (string.IsNullOrWhiteSpace(entry.EffectId) || entry.Prefab == null)
                    {
                        continue;
                    }

                    var intensityProp = string.IsNullOrWhiteSpace(entry.IntensityProperty) ? "_Intensity" : entry.IntensityProperty;
                    var directionProp = string.IsNullOrWhiteSpace(entry.DirectionProperty) ? "_Direction" : entry.DirectionProperty;

                    catalogEntries[count++] = new Space4XEffectCatalogEntry
                    {
                        EffectId = new FixedString64Bytes(entry.EffectId),
                        Prefab = entry.Prefab,
                        BindingMode = entry.BindingMode,
                        FollowMode = entry.FollowMode,
                        FollowOffset = entry.FollowOffset,
                        LifetimeOverride = entry.LifetimeOverride,
                        IntensityProperty = intensityProp,
                        DirectionProperty = directionProp
                    };
                }

                if (count == 0)
                {
                    return;
                }

                if (count != catalogEntries.Length)
                {
                    var trimmed = new Space4XEffectCatalogEntry[count];
                    Array.Copy(catalogEntries, trimmed, count);
                    catalogEntries = trimmed;
                }

                AddComponentObject(entity, new Space4XEffectCatalog
                {
                    Entries = catalogEntries
                });
            }
        }
    }
}

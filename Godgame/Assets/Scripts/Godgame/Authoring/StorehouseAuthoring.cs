using System;
using Godgame.Registry;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Godgame.Authoring
{
    /// <summary>
    /// Authoring component for individual storehouse registry entries.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StorehouseAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string label = "Storehouse";
        [SerializeField] private int storehouseId = 1001;

        [Header("Totals")]
        [SerializeField] private float totalCapacity = 500f;
        [SerializeField] private float totalStored = 0f;
        [SerializeField] private float totalReserved = 0f;
        [SerializeField] private ushort primaryResourceTypeIndex = 0;
        [SerializeField] private uint lastMutationTick = 0;

        [Header("Resource Summaries")]
        [SerializeField] private StorehouseResourceSummaryDefinition[] resourceSummaries =
        {
            new StorehouseResourceSummaryDefinition
            {
                ResourceTypeIndex = 0,
                Capacity = 500f,
                Stored = 0f,
                Reserved = 0f
            }
        };

        [Serializable]
        public struct StorehouseResourceSummaryDefinition
        {
            public ushort ResourceTypeIndex;
            public float Capacity;
            public float Stored;
            public float Reserved;
        }

        private sealed class Baker : Unity.Entities.Baker<StorehouseAuthoring>
        {
            public override void Bake(StorehouseAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var resolvedLabel = string.IsNullOrWhiteSpace(authoring.label)
                    ? $"Storehouse-{authoring.storehouseId}"
                    : authoring.label;

                AddComponent<SpatialIndexedTag>(entity);

                var summaries = default(FixedList32Bytes<GodgameStorehouseResourceSummary>);
                var definitions = authoring.resourceSummaries;
                if (definitions != null)
                {
                    for (var i = 0; i < definitions.Length; i++)
                    {
                        var definition = definitions[i];
                        summaries.Add(new GodgameStorehouseResourceSummary
                        {
                            ResourceTypeIndex = definition.ResourceTypeIndex,
                            Capacity = math.max(0f, definition.Capacity),
                            Stored = math.max(0f, definition.Stored),
                            Reserved = math.max(0f, definition.Reserved)
                        });
                    }
                }

                AddComponent(entity, new GodgameStorehouse
                {
                    Label = new FixedString64Bytes(resolvedLabel),
                    StorehouseId = authoring.storehouseId,
                    TotalCapacity = math.max(0f, authoring.totalCapacity),
                    TotalStored = math.max(0f, authoring.totalStored),
                    TotalReserved = math.max(0f, authoring.totalReserved),
                    PrimaryResourceTypeIndex = authoring.primaryResourceTypeIndex,
                    LastMutationTick = authoring.lastMutationTick,
                    ResourceSummaries = summaries
                });
            }
        }
    }
}

using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Identity;

namespace PureDOTS.Authoring.Identity
{
    [DisallowMultipleComponent]
    public sealed class EntityInventoryAuthoring : MonoBehaviour
    {
        [SerializeField] private ushort slots = 4;
        [SerializeField] private float massLimit = 100f;
        [SerializeField] private InventoryEntry[] initialContents = Array.Empty<InventoryEntry>();

        [Serializable]
        public struct InventoryEntry
        {
            public string SlotId;
            public GameObject Item;
            public float Quantity;
            public float Mass;
        }

        private sealed class Baker : Baker<EntityInventoryAuthoring>
        {
            public override void Bake(EntityInventoryAuthoring authoring)
            {
                if (authoring.slots == 0 && (authoring.initialContents == null || authoring.initialContents.Length == 0))
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new InventoryCapacity
                {
                    Slots = (ushort)math.max(1, authoring.slots),
                    MassLimit = math.max(0f, authoring.massLimit)
                });

                if (authoring.initialContents is not { Length: > 0 })
                {
                    AddBuffer<InventorySlot>(entity);
                    return;
                }

                var buffer = AddBuffer<InventorySlot>(entity);
                foreach (var entry in authoring.initialContents)
                {
                    buffer.Add(new InventorySlot
                    {
                        SlotId = new FixedString32Bytes(entry.SlotId ?? string.Empty),
                        Item = entry.Item != null ? GetEntity(entry.Item, TransformUsageFlags.Dynamic) : Entity.Null,
                        Quantity = entry.Quantity,
                        Mass = entry.Mass
                    });
                }
            }
        }
    }
}




using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Observability;

namespace PureDOTS.Authoring.Observability
{
    /// <summary>
    /// Adds a per-entity event log ring buffer for observability/debugging.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityEventLogAuthoring : MonoBehaviour
    {
        [Tooltip("Max entries retained per entity (ring buffer). Keep small (16-64).")]
        [Range(0, 256)]
        [SerializeField] private int capacity = 16;

        private sealed class Baker : Baker<EntityEventLogAuthoring>
        {
            public override void Bake(EntityEventLogAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var cap = (ushort)Mathf.Clamp(authoring.capacity, 0, ushort.MaxValue);
                if (cap == 0)
                {
                    return;
                }

                AddComponent(entity, new EntityEventLogState
                {
                    WriteIndex = 0,
                    Capacity = cap
                });
                AddBuffer<EntityEventLogEntry>(entity);
            }
        }
    }
}





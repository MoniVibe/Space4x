#if UNITY_EDITOR
using PureDOTS.Runtime.Formations;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Formations
{
    [DisallowMultipleComponent]
    public sealed class FormationAuthoring : MonoBehaviour
    {
        public FormationType formationType = FormationType.Line;
        [Range(1f, 100f)] public float slotSpacing = 5f;
        [Min(1)] public int slotCount = 5;
    }

    public sealed class FormationBaker : Baker<FormationAuthoring>
    {
        public override void Bake(FormationAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new FormationConfig
            {
                Type = authoring.formationType,
                SlotSpacing = authoring.slotSpacing,
                SlotCount = authoring.slotCount
            });

            var slots = AddBuffer<FormationSlot>(entity);
            for (int i = 0; i < authoring.slotCount; i++)
            {
                slots.Add(new FormationSlot
                {
                    LocalOffset = new float3(i * authoring.slotSpacing, 0f, 0f),
                    AssignedEntity = Entity.Null
                });
            }
        }
    }
}
#endif

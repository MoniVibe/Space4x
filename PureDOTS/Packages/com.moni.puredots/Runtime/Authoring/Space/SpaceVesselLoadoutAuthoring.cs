#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Space;

namespace PureDOTS.Authoring.Space
{
    [DisallowMultipleComponent]
    public sealed class SpaceVesselLoadoutAuthoring : MonoBehaviour
    {
        [Min(0f)] public float baseMassCapacity = 50f;
        [Range(0f, 100f)] public float overCapacityPercent = 10f;

        [Serializable]
        public struct EquipmentEntry
        {
            public SpaceEquipmentDefinitionAsset definition;
        }

        public EquipmentEntry[] defaultEquipment = Array.Empty<EquipmentEntry>();
    }

    public sealed class SpaceVesselLoadoutBaker : Baker<SpaceVesselLoadoutAuthoring>
    {
        public override void Bake(SpaceVesselLoadoutAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new SpaceVesselCapacity
            {
                BaseMassCapacity = math.max(0f, authoring.baseMassCapacity),
                OverCapacityPercent = math.clamp(authoring.overCapacityPercent, 0f, 100f),
                CurrentMass = 0f
            });

            var buffer = AddBuffer<SpaceVesselLoadoutEntry>(entity);
            if (authoring.defaultEquipment == null)
            {
                return;
            }

            foreach (var entry in authoring.defaultEquipment)
            {
                if (entry.definition == null)
                {
                    continue;
                }

                var equipmentId = string.IsNullOrWhiteSpace(entry.definition.equipmentId)
                    ? entry.definition.name
                    : entry.definition.equipmentId.Trim();

                if (string.IsNullOrEmpty(equipmentId))
                {
                    continue;
                }

                buffer.Add(new SpaceVesselLoadoutEntry
                {
                    EquipmentId = new FixedString64Bytes(equipmentId),
                    Type = entry.definition.equipmentType,
                    Mass = math.max(0f, entry.definition.mass)
                });
            }
        }
    }
}
#endif

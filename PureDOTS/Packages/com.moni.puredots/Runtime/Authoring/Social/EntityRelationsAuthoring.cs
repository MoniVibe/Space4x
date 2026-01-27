using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Social;

namespace PureDOTS.Authoring.Social
{
    /// <summary>
    /// Enables the relations module for an entity and optionally seeds initial ties.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityRelationsAuthoring : MonoBehaviour
    {
        [SerializeField] private bool autoEnableModule = true;
        [SerializeField] private RelationEntry[] initialRelations = Array.Empty<RelationEntry>();

        [Serializable]
        public struct RelationEntry
        {
            public GameObject Target;
            public RelationType Type;
            [Range(-100, 100)] public int Intensity;
        }

        private sealed class Baker : Baker<EntityRelationsAuthoring>
        {
            public override void Bake(EntityRelationsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                if (authoring.autoEnableModule)
                {
                    AddComponent<RelationsModuleTag>(entity);
                }

                if (authoring.initialRelations is not { Length: > 0 })
                {
                    return;
                }

                var buffer = AddBuffer<EntityRelation>(entity);
                foreach (var entry in authoring.initialRelations)
                {
                    if (entry.Target == null)
                    {
                        continue;
                    }

                    var targetEntity = GetEntity(entry.Target, TransformUsageFlags.Dynamic);
                    buffer.Add(new EntityRelation
                    {
                        OtherEntity = targetEntity,
                        Type = entry.Type,
                        Intensity = (sbyte)math.clamp(entry.Intensity, -100, 100),
                        InteractionCount = 0,
                        FirstMetTick = 0,
                        LastInteractionTick = 0,
                        Trust = 0,
                        Familiarity = 0,
                        Respect = 0,
                        Fear = 0
                    });
                }
            }
        }
    }
}


using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using PureDOTS.Runtime.Identity;

namespace PureDOTS.Authoring.Identity
{
    /// <summary>
    /// Authoring helper that initializes baseline blank-entity data (name, kind, capability tags).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityBasicsAuthoring : MonoBehaviour
    {
        [SerializeField] private string entityName;
        [SerializeField] private string entityKind;
        [SerializeField] private CapabilityEntry[] capabilities = Array.Empty<CapabilityEntry>();

        [Serializable]
        public struct CapabilityEntry
        {
            [Tooltip("Capability identifier, e.g. Sentient, IsStructure, CanCast")]
            public string Id;

            [Tooltip("Tier/intensity (0-255). Semantics defined per capability.")]
            [Range(0, 255)] public byte Tier;

            [Tooltip("Confidence/quality (0-255). Optional metadata for planners.")]
            [Range(0, 255)] public byte Confidence;
        }

        private sealed class Baker : Baker<EntityBasicsAuthoring>
        {
            public override void Bake(EntityBasicsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                if (!string.IsNullOrWhiteSpace(authoring.entityName))
                {
                    AddComponent(entity, new EntityName
                    {
                        Value = new FixedString64Bytes(authoring.entityName.Trim())
                    });
                }

                if (!string.IsNullOrWhiteSpace(authoring.entityKind))
                {
                    AddComponent(entity, new EntityKind
                    {
                        Value = new FixedString64Bytes(authoring.entityKind.Trim())
                    });
                }

                if (authoring.capabilities is not { Length: > 0 })
                {
                    return;
                }

                var capabilityBuffer = AddBuffer<CapabilityTag>(entity);
                for (var i = 0; i < authoring.capabilities.Length; i++)
                {
                    var entry = authoring.capabilities[i];
                    if (string.IsNullOrWhiteSpace(entry.Id))
                    {
                        continue;
                    }

                    capabilityBuffer.Add(new CapabilityTag
                    {
                        Id = new FixedString64Bytes(entry.Id.Trim()),
                        Tier = new CapabilityTier
                        {
                            Tier = entry.Tier,
                            Confidence = entry.Confidence
                        }
                    });
                }
            }
        }
    }
}




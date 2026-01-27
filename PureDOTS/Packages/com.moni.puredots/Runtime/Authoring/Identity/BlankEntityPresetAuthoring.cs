using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Identity;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Observability;
using PureDOTS.Runtime.Spatial;
using UHash128 = UnityEngine.Hash128;

namespace PureDOTS.Authoring.Identity
{
    [DisallowMultipleComponent]
    public sealed class BlankEntityPresetAuthoring : MonoBehaviour
    {
        [SerializeField] private BlankEntityPreset preset;

        private sealed class Baker : Baker<BlankEntityPresetAuthoring>
        {
            public override void Bake(BlankEntityPresetAuthoring authoring)
            {
                if (authoring.preset == null)
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var preset = authoring.preset;

                if (!string.IsNullOrWhiteSpace(preset.entityName))
                {
                    AddComponent(entity, new EntityName
                    {
                        Value = new FixedString64Bytes(preset.entityName.Trim())
                    });
                }

                if (!string.IsNullOrWhiteSpace(preset.entityKind))
                {
                    AddComponent(entity, new EntityKind
                    {
                        Value = new FixedString64Bytes(preset.entityKind.Trim())
                    });
                }

                if (!string.IsNullOrWhiteSpace(preset.stableKey))
                {
                    var hashHex = UHash128.Compute(preset.stableKey.Trim()).ToString();
                    var stableId = ParseHex128(hashHex);
                    AddComponent(entity, stableId);
                }

                if (preset.capabilityEntries is { Length: > 0 })
                {
                    var buffer = AddBuffer<CapabilityTag>(entity);
                    foreach (var cap in preset.capabilityEntries)
                    {
                        if (string.IsNullOrWhiteSpace(cap.Id))
                        {
                            continue;
                        }

                        buffer.Add(new CapabilityTag
                        {
                            Id = new FixedString64Bytes(cap.Id.Trim()),
                            Tier = new CapabilityTier
                            {
                                Tier = (byte)math.clamp(cap.Tier, 0, 255),
                                Confidence = (byte)math.clamp(cap.Confidence, 0, 255)
                            }
                        });
                    }
                }

                if (preset.enableNeeds)
                {
                    AddComponent<NeedsModuleTag>(entity);
                }

                if (preset.enableRelations)
                {
                    AddComponent<RelationsModuleTag>(entity);
                }

                if (preset.enableProfile)
                {
                    AddComponent<ProfileModuleTag>(entity);
                }

                if (preset.enableAgency)
                {
                    AddComponent<AgencyModuleTag>(entity);
                }

                if (preset.enableCommunication)
                {
                    AddComponent<CommunicationModuleTag>(entity);
                }

                if (preset.enableGroupKnowledge)
                {
                    AddComponent<GroupKnowledgeModuleTag>(entity);
                }

                if (preset.enableEventLog && preset.eventLogCapacity > 0)
                {
                    var cap = (ushort)math.clamp((int)preset.eventLogCapacity, 1, ushort.MaxValue);
                    AddComponent(entity, new EntityEventLogState
                    {
                        Capacity = cap,
                        WriteIndex = 0
                    });
                    AddBuffer<EntityEventLogEntry>(entity);
                }

                if (preset.enableIntentQueue && preset.intentCapacity > 0)
                {
                    var cap = (byte)math.max(1, preset.intentCapacity);
                    AddComponent(entity, new EntityIntentQueue
                    {
                        Capacity = cap,
                        PendingCount = 0
                    });
                    AddBuffer<EntityIntent>(entity);
                }

                if (preset.enableSpatialIndexing)
                {
                    AddComponent<SpatialIndexedTag>(entity);
                }
            }

            private static EntityStableId ParseHex128(string hex32)
            {
                if (string.IsNullOrEmpty(hex32) || hex32.Length < 32)
                {
                    return default;
                }

                var hi = ulong.Parse(hex32.Substring(0, 16), System.Globalization.NumberStyles.HexNumber);
                var lo = ulong.Parse(hex32.Substring(16, 16), System.Globalization.NumberStyles.HexNumber);
                return new EntityStableId
                {
                    Hi = hi,
                    Lo = lo
                };
            }
        }
    }
}


using System;
using System.Collections.Generic;
using PureDOTS.Presentation.Runtime;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class ResourceSourceAuthoring : MonoBehaviour
    {
        [Header("Resource")]
        public string resourceTypeId;
        [Min(0f)] public float initialUnits = 100f;

        [Header("Gathering")]
        [Min(0f)] public float gatherRatePerWorker = 2f;
        [Min(0)] public int maxSimultaneousWorkers = 3;
        [Tooltip("Approximate radius used for editor gizmos and default gather distance.")]
        [Min(0f)] public float debugGatherRadius = 3f;

        [Header("Lifecycle")]
        public bool infinite;
        public bool respawns;
        [Min(0f)] public float respawnSeconds = 60f;
        public bool handUprootAllowed;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var radius = Mathf.Max(0.1f, debugGatherRadius);
            Gizmos.color = new Color(0.93f, 0.77f, 0.36f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }

    public sealed class ResourceSourceBaker : Baker<ResourceSourceAuthoring>
    {
        private static FixedString64Bytes ToFixedString(string value)
        {
            FixedString64Bytes str = default;
            if (!string.IsNullOrWhiteSpace(value))
            {
                str = value.Trim();
            }

            return str;
        }

        public override void Bake(ResourceSourceAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            var resourceType = ToFixedString(authoring.resourceTypeId);
            if (!resourceType.IsEmpty)
            {
                AddComponent(entity, new ResourceTypeId { Value = resourceType });
            }

            byte flags = 0;
            if (authoring.infinite) flags |= ResourceSourceConfig.FlagInfinite;
            if (authoring.respawns) flags |= ResourceSourceConfig.FlagRespawns;
            if (authoring.handUprootAllowed) flags |= ResourceSourceConfig.FlagHandUprootAllowed;

            AddComponent(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = math.max(0f, authoring.gatherRatePerWorker),
                MaxSimultaneousWorkers = math.max(0, authoring.maxSimultaneousWorkers),
                RespawnSeconds = math.max(0f, authoring.respawnSeconds),
                Flags = flags
            });

            AddComponent(entity, new ResourceSourceState
            {
                UnitsRemaining = math.max(0f, authoring.initialUnits)
            });

            AddComponent(entity, new LastRecordedTick { Tick = 0 });

            AddComponent<RewindableTag>(entity);
            AddComponent(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.Default,
                OverrideStrideSeconds = 0f
            });
            AddBuffer<ResourceHistorySample>(entity);

            AddComponent(entity, PresentationRequest.Create(PresentationPrototype.ResourceNode));
        }
    }

    [Serializable]
    public struct StorehouseCapacityEntry
    {
        public string resourceTypeId;
        public float maxCapacity;
    }

    [DisallowMultipleComponent]
    public sealed class StorehouseAuthoring : MonoBehaviour
    {
        [Header("Capacity & Flow")]
        [Min(0f)] public float shredRate = 1f;
        [Min(0)] public int maxShredQueueSize = 8;
        [Min(0f)] public float inputRate = 10f;
        [Min(0f)] public float outputRate = 10f;

        [Header("Storage Types")]
        public List<StorehouseCapacityEntry> capacities = new();

#if UNITY_EDITOR
        private void OnValidate()
        {
            for (int i = capacities.Count - 1; i >= 0; i--)
            {
                var entry = capacities[i];
                if (string.IsNullOrWhiteSpace(entry.resourceTypeId) || entry.maxCapacity <= 0f)
                {
                    capacities.RemoveAt(i);
                    continue;
                }

                entry.resourceTypeId = entry.resourceTypeId.Trim();
                capacities[i] = entry;
            }
        }
#endif
    }

    public sealed class StorehouseBaker : Baker<StorehouseAuthoring>
    {
        private static FixedString64Bytes ToFixedString(string value)
        {
            FixedString64Bytes str = default;
            if (!string.IsNullOrWhiteSpace(value))
            {
                str = value.Trim();
            }

            return str;
        }

        public override void Bake(StorehouseAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            AddComponent(entity, new StorehouseConfig
            {
                ShredRate = math.max(0f, authoring.shredRate),
                MaxShredQueueSize = math.max(0, authoring.maxShredQueueSize),
                InputRate = math.max(0f, authoring.inputRate),
                OutputRate = math.max(0f, authoring.outputRate)
            });

            var capacityBuffer = AddBuffer<StorehouseCapacityElement>(entity);
            float totalCapacity = 0f;
            foreach (var entry in authoring.capacities)
            {
                if (entry.maxCapacity <= 0f)
                {
                    continue;
                }

                var resourceType = ToFixedString(entry.resourceTypeId);
                if (resourceType.IsEmpty)
                {
                    continue;
                }

                capacityBuffer.Add(new StorehouseCapacityElement
                {
                    ResourceTypeId = resourceType,
                    MaxCapacity = entry.maxCapacity
                });
                totalCapacity += entry.maxCapacity;
            }

            AddComponent(entity, new StorehouseInventory
            {
                TotalStored = 0f,
                TotalCapacity = totalCapacity,
                ItemTypeCount = capacityBuffer.Length,
                IsShredding = 0,
                LastUpdateTick = 0
            });

            AddBuffer<StorehouseInventoryItem>(entity);
            AddComponent<RewindableTag>(entity);
            AddComponent(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.Default,
                OverrideStrideSeconds = 0f
            });
            AddBuffer<StorehouseHistorySample>(entity);

            AddComponent(entity, PresentationRequest.Create(PresentationPrototype.Building));
        }
    }

    [DisallowMultipleComponent]
    public sealed class ResourceChunkAuthoring : MonoBehaviour
    {
        public string resourceTypeId;
        [Header("Chunk Values")]
        [Min(0f)] public float massPerUnit = 0.5f;
        [Min(0f)] public float minScale = 0.3f;
        [Min(0f)] public float maxScale = 1.2f;
        [Min(0f)] public float defaultUnits = 50f;
    }

    public sealed class ResourceChunkBaker : Baker<ResourceChunkAuthoring>
    {
        private static FixedString64Bytes ToFixedString(string value)
        {
            FixedString64Bytes str = default;
            if (!string.IsNullOrWhiteSpace(value))
            {
                str = value.Trim();
            }

            return str;
        }

        public override void Bake(ResourceChunkAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            var resourceType = ToFixedString(authoring.resourceTypeId);
            if (!resourceType.IsEmpty)
            {
                AddComponent(entity, new ResourceTypeId { Value = resourceType });
            }

            AddComponent(entity, new ResourceChunkConfig
            {
                MassPerUnit = math.max(0f, authoring.massPerUnit),
                MinScale = math.max(0f, authoring.minScale),
                MaxScale = math.max(authoring.minScale, authoring.maxScale),
                DefaultUnits = math.max(0f, authoring.defaultUnits)
            });

            AddComponent(entity, PresentationRequest.Create(PresentationPrototype.Chunk));
            AddComponent(entity, new HandInteractable
            {
                Type = HandInteractableType.ResourceChunk,
                Radius = math.max(0.5f, authoring.maxScale)
            });
        }
    }

    [Serializable]
    public struct ConstructionCostEntry
    {
        public string resourceTypeId;
        public float unitsRequired;
    }

    [DisallowMultipleComponent]
    public sealed class ConstructionSiteAuthoring : MonoBehaviour
    {
        public List<ConstructionCostEntry> cost = new();
        [Min(0f)] public float requiredProgress = 100f;
        [Min(0f)] public float currentProgress;
        public GameObject completionPrefab;
        public bool destroySiteOnComplete = true;
        public int siteIdOverride;
    }

    public sealed class ConstructionSiteBaker : Baker<ConstructionSiteAuthoring>
    {
        private static FixedString64Bytes ToFixedString(string value)
        {
            FixedString64Bytes str = default;
            if (!string.IsNullOrWhiteSpace(value))
            {
                str = value.Trim();
            }

            return str;
        }

        public override void Bake(ConstructionSiteAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            int siteId = authoring.siteIdOverride != 0 ? authoring.siteIdOverride : authoring.gameObject.GetInstanceID();

            AddComponent(entity, new ConstructionSiteId { Value = siteId });
            AddComponent(entity, new ConstructionSiteFlags { Value = 0 });
            AddComponent(entity, new ConstructionSiteProgress
            {
                RequiredProgress = math.max(0f, authoring.requiredProgress),
                CurrentProgress = math.clamp(authoring.currentProgress, 0f, authoring.requiredProgress)
            });

            var costBuffer = AddBuffer<ConstructionCostElement>(entity);
            foreach (var entry in authoring.cost)
            {
                if (entry.unitsRequired <= 0f)
                {
                    continue;
                }

                var resourceType = ToFixedString(entry.resourceTypeId);
                if (resourceType.IsEmpty)
                {
                    continue;
                }

                costBuffer.Add(new ConstructionCostElement
                {
                    ResourceTypeId = resourceType,
                    UnitsRequired = entry.unitsRequired
                });
            }

            AddBuffer<ConstructionDeliveredElement>(entity);
            AddBuffer<ConstructionDepositCommand>(entity);
            AddBuffer<ConstructionProgressCommand>(entity);

            Entity prefabEntity = authoring.completionPrefab != null
                ? GetEntity(authoring.completionPrefab, TransformUsageFlags.Dynamic)
                : Entity.Null;

            AddComponent(entity, new ConstructionCompletionPrefab
            {
                Prefab = prefabEntity,
                DestroySiteEntity = authoring.destroySiteOnComplete
            });

            AddComponent(entity, PresentationRequest.Create(PresentationPrototype.Building));
        }
    }
}
#endif

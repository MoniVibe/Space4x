using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Compliance;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using Unity.Entities.Hybrid;
#endif

namespace PureDOTS.Authoring.Compliance
{
    [CreateAssetMenu(fileName = "ComplianceRuleCatalog", menuName = "PureDOTS/Space4X/Compliance Rule Catalog")]
    public sealed class ComplianceRuleCatalogAsset : ScriptableObject
    {
        public List<ComplianceRuleCatalogEntry> Entries = new();
    }

    [Serializable]
    public struct ComplianceRuleCatalogEntry
    {
        public string RuleId;
        public ComplianceVerb Verb;
        public ComplianceTags TargetTags;
        public float Magnitude;
        public EnforcementLevel Enforcement;
    }

    /// <summary>
    /// Authoring component for compliance rule catalog.
    /// </summary>
    public sealed class ComplianceRuleCatalogAuthoring : MonoBehaviour
    {
        public ComplianceRuleCatalogAsset Catalog;
    }

#if UNITY_EDITOR
    public sealed class ComplianceRuleCatalogBaker : Baker<ComplianceRuleCatalogAuthoring>
    {
        public override void Bake(ComplianceRuleCatalogAuthoring authoring)
        {
            if (authoring.Catalog == null || authoring.Catalog.Entries == null || authoring.Catalog.Entries.Count == 0)
            {
                Debug.LogWarning("[ComplianceRuleCatalogBaker] No catalog assigned or empty entries; skipping blob creation.");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ComplianceRuleCatalogBlob>();
            var array = builder.Allocate(ref root.Rules, authoring.Catalog.Entries.Count);

            for (int i = 0; i < authoring.Catalog.Entries.Count; i++)
            {
                var entry = authoring.Catalog.Entries[i];
                var id = string.IsNullOrWhiteSpace(entry.RuleId) ? $"rule.{i}" : entry.RuleId.Trim().ToLowerInvariant();

                array[i] = new ComplianceRule
                {
                    Id = new FixedString32Bytes(id),
                    Verb = (byte)entry.Verb,
                    TargetTags = (uint)entry.TargetTags,
                    Magnitude = entry.Magnitude,
                    Enforcement = (byte)entry.Enforcement
                };
            }

            var blob = builder.CreateBlobAssetReference<ComplianceRuleCatalogBlob>(Allocator.Persistent);
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new ComplianceRuleCatalog { Catalog = blob });
        }
    }
#endif
}


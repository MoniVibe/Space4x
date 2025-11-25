using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for patronage webs (aggregate memberships).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Patronage Web")]
    public sealed class PatronageWebAuthoring : MonoBehaviour
    {
        [Serializable]
        public class PatronageEntry
        {
            [Tooltip("Aggregate type")]
            public AffiliationType aggregateType;
            [Tooltip("Aggregate ID")]
            public string aggregateId = string.Empty;
            [Tooltip("Role in aggregate")]
            public string role = string.Empty;
        }

        [Tooltip("Patronage memberships (individuals belong to one or more aggregates)")]
        public List<PatronageEntry> patronages = new List<PatronageEntry>();

        public sealed class Baker : Unity.Entities.Baker<PatronageWebAuthoring>
        {
            public override void Bake(PatronageWebAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<Registry.PatronageMembership>(entity);

                if (authoring.patronages != null)
                {
                    foreach (var entry in authoring.patronages)
                    {
                        if (!string.IsNullOrWhiteSpace(entry.aggregateId))
                        {
                            buffer.Add(new Registry.PatronageMembership
                            {
                                AggregateType = entry.aggregateType,
                                AggregateId = new FixedString64Bytes(entry.aggregateId),
                                Role = new FixedString64Bytes(entry.role ?? string.Empty)
                            });
                        }
                    }
                }
            }
        }
    }
}


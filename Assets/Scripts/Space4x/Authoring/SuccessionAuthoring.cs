using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for succession (heirs/proteges who inherit partial expertise).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Succession")]
    public sealed class SuccessionAuthoring : MonoBehaviour
    {
        [Serializable]
        public class SuccessorEntry
        {
            [Tooltip("Successor entity ID (heir or protege)")]
            public string successorId = string.Empty;
            [Tooltip("Inheritance percentage (0-1, e.g., 0.5 = 50% of expertise)")]
            [Range(0f, 1f)]
            public float inheritancePercentage = 0.5f;
            [Tooltip("Successor type (Heir or Protege)")]
            public SuccessorType type = SuccessorType.Heir;
        }

        public enum SuccessorType : byte
        {
            Heir = 0,
            Protege = 1
        }

        [Tooltip("Successors (heirs/proteges who inherit partial expertise)")]
        public List<SuccessorEntry> successors = new List<SuccessorEntry>();

        public sealed class Baker : Unity.Entities.Baker<SuccessionAuthoring>
        {
            public override void Bake(SuccessionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<Registry.Successor>(entity);

                if (authoring.successors != null)
                {
                    foreach (var entry in authoring.successors)
                    {
                        if (!string.IsNullOrWhiteSpace(entry.successorId))
                        {
                            buffer.Add(new Registry.Successor
                            {
                                SuccessorId = new FixedString64Bytes(entry.successorId),
                                InheritancePercentage = entry.inheritancePercentage,
                                Type = (Registry.SuccessorType)entry.type
                            });
                        }
                    }
                }
            }
        }
    }
}


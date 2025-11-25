using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for outlook entries (sparse buffer).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Outlook")]
    public sealed class OutlookAuthoring : MonoBehaviour
    {
        [Serializable]
        public class OutlookEntry
        {
            public OutlookId outlookId;
            [Tooltip("Weight in [-1, +1]. Only top three are surfaced for crews")]
            [Range(-1f, 1f)]
            public float weight = 0f;
        }

        [Tooltip("Outlook entries (multiple allowed, top three used for aggregation)")]
        public List<OutlookEntry> outlooks = new List<OutlookEntry>();

        public sealed class Baker : Unity.Entities.Baker<OutlookAuthoring>
        {
            public override void Bake(OutlookAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<Registry.OutlookEntry>(entity);

                if (authoring.outlooks != null)
                {
                    foreach (var entry in authoring.outlooks)
                    {
                        if (math.abs(entry.weight) > 0.01f) // Only add non-zero weights
                        {
                            buffer.Add(new Registry.OutlookEntry
                            {
                                OutlookId = entry.outlookId,
                                Weight = (half)math.clamp(entry.weight, -1f, 1f)
                            });
                        }
                    }
                }
            }
        }
    }
}


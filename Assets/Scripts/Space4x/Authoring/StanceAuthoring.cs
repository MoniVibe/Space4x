using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for stance entries (sparse buffer).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Stance")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Space4X.Authoring", null, "OutlookAuthoring")]
    public sealed class StanceAuthoring : MonoBehaviour
    {
        [Serializable]
        [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Space4X.Authoring", null, "OutlookEntry")]
        public class StanceEntryData
        {
            [FormerlySerializedAs("outlookId")]
            public StanceId StanceId;
            [Tooltip("Weight in [-1, +1]. Only top three are surfaced for crews")]
            [Range(-1f, 1f)]
            public float weight = 0f;
        }

        [FormerlySerializedAs("outlooks")]
        [Tooltip("Stance entries (multiple allowed, top three used for aggregation)")]
        public List<StanceEntryData> stances = new List<StanceEntryData>();

        public sealed class Baker : Unity.Entities.Baker<StanceAuthoring>
        {
            public override void Bake(StanceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<Registry.StanceEntry>(entity);

                if (authoring.stances != null)
                {
                    foreach (var entry in authoring.stances)
                    {
                        if (math.abs(entry.weight) > 0.01f) // Only add non-zero weights
                        {
                            buffer.Add(new Registry.StanceEntry
                            {
                                StanceId = entry.StanceId,
                                Weight = (half)math.clamp(entry.weight, -1f, 1f)
                            });
                        }
                    }
                }
            }
        }
    }
}



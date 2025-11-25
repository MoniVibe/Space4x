using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for aggregate entities (crews, fleets, colonies, factions).
    /// Tracks aggregated race/culture/outlook composition.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Aggregate Alignment")]
    public sealed class AggregateAlignmentAuthoring : MonoBehaviour
    {
        [Serializable]
        public class RacePresenceEntry
        {
            public ushort raceId;
            public int count = 0;
        }

        [Serializable]
        public class CulturePresenceEntry
        {
            public ushort cultureId;
            public int count = 0;
        }

        [Serializable]
        public class TopOutlookEntry
        {
            public OutlookId outlookId;
            [Range(-1f, 1f)]
            public float weight = 0f;
        }

        [Tooltip("Race composition (aggregated from members)")]
        public List<RacePresenceEntry> racePresence = new List<RacePresenceEntry>();

        [Tooltip("Culture composition (aggregated from members)")]
        public List<CulturePresenceEntry> culturePresence = new List<CulturePresenceEntry>();

        [Tooltip("Top outlooks (top three aggregated from members)")]
        public List<TopOutlookEntry> topOutlooks = new List<TopOutlookEntry>();

        public sealed class Baker : Unity.Entities.Baker<AggregateAlignmentAuthoring>
        {
            public override void Bake(AggregateAlignmentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add race presence buffer
                if (authoring.racePresence != null && authoring.racePresence.Count > 0)
                {
                    var raceBuffer = AddBuffer<RacePresence>(entity);
                    foreach (var entry in authoring.racePresence)
                    {
                        if (entry.count > 0)
                        {
                            raceBuffer.Add(new RacePresence
                            {
                                RaceId = entry.raceId,
                                Count = entry.count
                            });
                        }
                    }
                }

                // Add culture presence buffer
                if (authoring.culturePresence != null && authoring.culturePresence.Count > 0)
                {
                    var cultureBuffer = AddBuffer<CulturePresence>(entity);
                    foreach (var entry in authoring.culturePresence)
                    {
                        if (entry.count > 0)
                        {
                            cultureBuffer.Add(new CulturePresence
                            {
                                CultureId = entry.cultureId,
                                Count = entry.count
                            });
                        }
                    }
                }

                // Add top outlooks buffer
                if (authoring.topOutlooks != null && authoring.topOutlooks.Count > 0)
                {
                    var outlookBuffer = AddBuffer<TopOutlook>(entity);
                    int count = 0;
                    foreach (var entry in authoring.topOutlooks)
                    {
                        if (count >= 3) break; // Only top three
                        if (math.abs(entry.weight) > 0.01f)
                        {
                            outlookBuffer.Add(new TopOutlook
                            {
                                OutlookId = entry.outlookId,
                                Weight = (half)math.clamp(entry.weight, -1f, 1f)
                            });
                            count++;
                        }
                    }
                }
            }
        }
    }
}


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
        [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Space4X.Authoring", null, "TopOutlookEntry")]
        public class TopStanceEntry
        {
            public StanceId StanceId;
            [Range(-1f, 1f)]
            public float weight = 0f;
        }

        [Tooltip("Race composition (aggregated from members)")]
        public List<RacePresenceEntry> racePresence = new List<RacePresenceEntry>();

        [Tooltip("Culture composition (aggregated from members)")]
        public List<CulturePresenceEntry> culturePresence = new List<CulturePresenceEntry>();

        [FormerlySerializedAs("topOutlooks")]
        [Tooltip("Top stances (top three aggregated from members)")]
        public List<TopStanceEntry> topStances = new List<TopStanceEntry>();

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

                // Add top stances buffer
                if (authoring.topStances != null && authoring.topStances.Count > 0)
                {
                    var outlookBuffer = AddBuffer<TopStance>(entity);
                    int count = 0;
                    foreach (var entry in authoring.topStances)
                    {
                        if (count >= 3) break; // Only top three
                        if (math.abs(entry.weight) > 0.01f)
                        {
                            outlookBuffer.Add(new TopStance
                            {
                                StanceId = entry.StanceId,
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



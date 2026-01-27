using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.AI.WorldFacts
{
    /// <summary>
    /// Stable API surface for world facts.
    /// Must be locked before building action libraries.
    /// </summary>
    public static class WorldFactsAPI
    {
        /// <summary>
        /// Sets a world fact value.
        /// </summary>
        public static void SetFact(
            ref DynamicBuffer<WorldFact> facts,
            WorldFactKey key,
            float value,
            WorldFactProvenance provenance,
            uint currentTick)
        {
            for (int i = 0; i < facts.Length; i++)
            {
                if (facts[i].Key == key)
                {
                    var fact = facts[i];
                    fact.Value = value;
                    fact.Provenance = provenance;
                    fact.LastUpdatedTick = currentTick;
                    fact.IsValid = 1;
                    facts[i] = fact;
                    return;
                }
            }

            // Add new fact
            facts.Add(new WorldFact
            {
                Key = key,
                Value = value,
                Provenance = provenance,
                LastUpdatedTick = currentTick,
                IsValid = 1
            });
        }

        /// <summary>
        /// Gets a world fact value.
        /// Returns defaultValue if fact not found.
        /// </summary>
        public static float GetFact(
            in DynamicBuffer<WorldFact> facts,
            WorldFactKey key,
            float defaultValue = 0f)
        {
            for (int i = 0; i < facts.Length; i++)
            {
                if (facts[i].Key == key && facts[i].IsValid != 0)
                {
                    return facts[i].Value;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Checks if a fact exists and is valid.
        /// </summary>
        public static bool HasFact(in DynamicBuffer<WorldFact> facts, WorldFactKey key)
        {
            for (int i = 0; i < facts.Length; i++)
            {
                if (facts[i].Key == key && facts[i].IsValid != 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets fact with provenance information.
        /// </summary>
        public static bool TryGetFact(
            in DynamicBuffer<WorldFact> facts,
            WorldFactKey key,
            out float value,
            out WorldFactProvenance provenance,
            out uint lastUpdatedTick)
        {
            for (int i = 0; i < facts.Length; i++)
            {
                if (facts[i].Key == key && facts[i].IsValid != 0)
                {
                    value = facts[i].Value;
                    provenance = facts[i].Provenance;
                    lastUpdatedTick = facts[i].LastUpdatedTick;
                    return true;
                }
            }
            value = 0f;
            provenance = WorldFactProvenance.Registry;
            lastUpdatedTick = 0;
            return false;
        }

        /// <summary>
        /// Invalidates a fact (marks it as stale).
        /// </summary>
        public static void InvalidateFact(ref DynamicBuffer<WorldFact> facts, WorldFactKey key)
        {
            for (int i = 0; i < facts.Length; i++)
            {
                if (facts[i].Key == key)
                {
                    var fact = facts[i];
                    fact.IsValid = 0;
                    facts[i] = fact;
                    return;
                }
            }
        }

        /// <summary>
        /// Clears all facts (useful for reset).
        /// </summary>
        public static void ClearFacts(ref DynamicBuffer<WorldFact> facts)
        {
            facts.Clear();
        }
    }
}




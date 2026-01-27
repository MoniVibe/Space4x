using System;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Config
{
    /// <summary>
    /// Runtime blob data for villager archetype definitions.
    /// Contains base stats, needs decay rates, job weights, and alignment lean for each archetype.
    /// </summary>
    public struct VillagerArchetypeData
    {
        public FixedString64Bytes ArchetypeName;
        
        // Base stats (0-100 range)
        public byte BasePhysique;
        public byte BaseFinesse;
        public byte BaseWillpower;
        
        // Needs decay rates (per tick, normalized)
        public float HungerDecayRate;
        public float EnergyDecayRate;
        public float MoraleDecayRate;
        
        // Job preference weights (0-100, higher = more preferred)
        public byte GatherJobWeight;
        public byte BuildJobWeight;
        public byte CraftJobWeight;
        public byte CombatJobWeight;
        public byte TradeJobWeight;
        
        // Alignment lean (-100 to +100)
        public sbyte MoralAxisLean;
        public sbyte OrderAxisLean;
        public sbyte PurityAxisLean;
        
        // Loyalty baseline (0-100)
        public byte BaseLoyalty;
    }

    /// <summary>
    /// Blob asset containing all villager archetype definitions.
    /// </summary>
    public struct VillagerArchetypeCatalogBlob
    {
        public BlobArray<VillagerArchetypeData> Archetypes;
        
        public int FindArchetypeIndex(in FixedString64Bytes name)
        {
            for (int i = 0; i < Archetypes.Length; i++)
            {
                if (Archetypes[i].ArchetypeName.Equals(name))
                {
                    return i;
                }
            }
            return -1;
        }
        
        public bool TryGetArchetype(in FixedString64Bytes name, out VillagerArchetypeData archetype)
        {
            var index = FindArchetypeIndex(name);
            if (index >= 0)
            {
                archetype = Archetypes[index];
                return true;
            }
            archetype = default;
            return false;
        }
    }
}


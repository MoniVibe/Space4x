using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Types of stats that can be tracked.
    /// </summary>
    public enum StatType : byte
    {
        None = 0,
        
        // Core stats (1-19)
        Command = 1,
        Tactics = 2,
        Logistics = 3,
        Diplomacy = 4,
        Engineering = 5,
        Resolve = 6,
        
        // Physical attributes (20-29)
        Physique = 20,
        Finesse = 21,
        Will = 22,
        
        // Derived stats (30-39)
        Initiative = 30,
        Morale = 31,
        Experience = 32,
        
        // Combat stats (40-49)
        Attack = 40,
        Defense = 41,
        Accuracy = 42,
        Evasion = 43,
        
        // Resource stats (50-59)
        Health = 50,
        Energy = 51,
        Focus = 52,
        Stamina = 53
    }

    /// <summary>
    /// Types of XP changes for command logging.
    /// </summary>
    public enum StatXPChangeType : byte
    {
        Gain = 0,
        Spend = 1,
        Reset = 2,
        Transfer = 3,
        Decay = 4,
        Bonus = 5
    }

    /// <summary>
    /// Records stat values at a point in time for rewind replay.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct StatHistorySample : IBufferElementData
    {
        public uint Tick;
        public half Command;
        public half Tactics;
        public half Logistics;
        public half Diplomacy;
        public half Engineering;
        public half Resolve;
        public float GeneralXP;
    }

    /// <summary>
    /// Command log entry for stat XP changes.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct StatXPCommandLogEntry : IBufferElementData
    {
        public uint Tick;
        public Entity TargetEntity;
        public StatType StatType;
        public float XPAmount;
        public StatXPChangeType ChangeType;
        public FixedString32Bytes Source;
    }

    /// <summary>
    /// Configuration for stat history sampling.
    /// </summary>
    public struct StatHistoryConfig : IComponentData
    {
        public uint SampleInterval;   // Ticks between samples
        public byte MaxSamples;       // Maximum history entries
        public bool TrackXPChanges;   // Whether to log XP changes
    }
}


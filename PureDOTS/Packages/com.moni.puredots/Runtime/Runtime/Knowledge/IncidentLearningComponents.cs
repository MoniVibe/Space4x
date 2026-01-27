using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Knowledge
{
    public enum IncidentLearningKind : byte
    {
        Unknown = 0,
        Hit = 1,
        NearMiss = 2,
        Observation = 3,
        Failure = 4
    }

    /// <summary>
    /// Global tuning for incident-driven learning.
    /// </summary>
    public struct IncidentLearningConfig : IComponentData
    {
        public int MaxEntries;
        public float MemoryGainOnHit;
        public float MemoryGainOnNearMiss;
        public float MemoryGainOnObservation;
        public float MemoryGainDefault;
        public float MemoryDecayPerSecond;
        public float IncidentCooldownSeconds;
        public float MinSeverity;
        public float MinBias;
        public float MaxBias;

        public static IncidentLearningConfig Default => new IncidentLearningConfig
        {
            MaxEntries = 4,
            MemoryGainOnHit = 0.35f,
            MemoryGainOnNearMiss = 0.15f,
            MemoryGainOnObservation = 0.05f,
            MemoryGainDefault = 0.1f,
            MemoryDecayPerSecond = 0.003f,
            IncidentCooldownSeconds = 1.5f,
            MinSeverity = 0.01f,
            MinBias = 0f,
            MaxBias = 1f
        };
    }

    /// <summary>
    /// Tag enabling incident learning on an entity.
    /// </summary>
    public struct IncidentLearningAgent : IComponentData
    {
    }

    /// <summary>
    /// Per-category learning memory for an entity.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct IncidentLearningMemory : IBufferElementData
    {
        public FixedString64Bytes CategoryId;
        public float Bias;
        public float RecentSeverity;
        public uint LastIncidentTick;
        public uint NextIncidentAllowedTick;
        public uint LastUpdateTick;
        public ushort IncidentCount;
        public ushort NearMissCount;
    }

    /// <summary>
    /// Singleton tag for the incident learning event buffer.
    /// </summary>
    public struct IncidentLearningEventBuffer : IComponentData
    {
    }

    /// <summary>
    /// Incident event routed to learning agents.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct IncidentLearningEvent : IBufferElementData
    {
        public Entity Target;
        public Entity Source;
        public float3 Position;
        public FixedString64Bytes CategoryId;
        public float Severity;
        public IncidentLearningKind Kind;
        public uint Tick;
    }

    /// <summary>
    /// Sample incident categories for early integrations.
    /// </summary>
    public static class IncidentLearningCategories
    {
        public static readonly FixedString64Bytes TreeFall = BuildTreeFall();
        public static readonly FixedString64Bytes TreeFallNearMiss = BuildTreeFallNearMiss();
        public static readonly FixedString64Bytes FallingDebris = BuildFallingDebris();
        public static readonly FixedString64Bytes ConstructionIncident = BuildConstructionIncident();
        public static readonly FixedString64Bytes ConstructionCollapse = BuildConstructionCollapse();
        public static readonly FixedString64Bytes ToolFailure = BuildToolFailure();

        private static FixedString64Bytes BuildTreeFall()
        {
            var id = new FixedString64Bytes();
            id.Append('t');
            id.Append('r');
            id.Append('e');
            id.Append('e');
            id.Append('_');
            id.Append('f');
            id.Append('a');
            id.Append('l');
            id.Append('l');
            return id;
        }

        private static FixedString64Bytes BuildTreeFallNearMiss()
        {
            var id = new FixedString64Bytes();
            id.Append('t');
            id.Append('r');
            id.Append('e');
            id.Append('e');
            id.Append('_');
            id.Append('f');
            id.Append('a');
            id.Append('l');
            id.Append('l');
            id.Append('_');
            id.Append('n');
            id.Append('e');
            id.Append('a');
            id.Append('r');
            id.Append('_');
            id.Append('m');
            id.Append('i');
            id.Append('s');
            id.Append('s');
            return id;
        }

        private static FixedString64Bytes BuildFallingDebris()
        {
            var id = new FixedString64Bytes();
            id.Append('f');
            id.Append('a');
            id.Append('l');
            id.Append('l');
            id.Append('i');
            id.Append('n');
            id.Append('g');
            id.Append('_');
            id.Append('d');
            id.Append('e');
            id.Append('b');
            id.Append('r');
            id.Append('i');
            id.Append('s');
            return id;
        }

        private static FixedString64Bytes BuildConstructionIncident()
        {
            var id = new FixedString64Bytes();
            id.Append('c');
            id.Append('o');
            id.Append('n');
            id.Append('s');
            id.Append('t');
            id.Append('r');
            id.Append('u');
            id.Append('c');
            id.Append('t');
            id.Append('i');
            id.Append('o');
            id.Append('n');
            id.Append('_');
            id.Append('i');
            id.Append('n');
            id.Append('c');
            id.Append('i');
            id.Append('d');
            id.Append('e');
            id.Append('n');
            id.Append('t');
            return id;
        }

        private static FixedString64Bytes BuildConstructionCollapse()
        {
            var id = new FixedString64Bytes();
            id.Append('c');
            id.Append('o');
            id.Append('n');
            id.Append('s');
            id.Append('t');
            id.Append('r');
            id.Append('u');
            id.Append('c');
            id.Append('t');
            id.Append('i');
            id.Append('o');
            id.Append('n');
            id.Append('_');
            id.Append('c');
            id.Append('o');
            id.Append('l');
            id.Append('l');
            id.Append('a');
            id.Append('p');
            id.Append('s');
            id.Append('e');
            return id;
        }

        private static FixedString64Bytes BuildToolFailure()
        {
            var id = new FixedString64Bytes();
            id.Append('t');
            id.Append('o');
            id.Append('o');
            id.Append('l');
            id.Append('_');
            id.Append('f');
            id.Append('a');
            id.Append('i');
            id.Append('l');
            id.Append('u');
            id.Append('r');
            id.Append('e');
            return id;
        }
    }

    public static class IncidentLearningUtility
    {
        public static void ApplyDecay(ref IncidentLearningMemory memory, uint currentTick, float secondsPerTick, float decayPerSecond, float minBias)
        {
            if (decayPerSecond <= 0f || currentTick <= memory.LastUpdateTick)
            {
                memory.LastUpdateTick = currentTick;
                return;
            }

            var ticksElapsed = currentTick - memory.LastUpdateTick;
            var decay = decayPerSecond * secondsPerTick * ticksElapsed;
            memory.Bias = math.max(minBias, memory.Bias - decay);
            memory.RecentSeverity = math.max(0f, memory.RecentSeverity - decay);
            memory.LastUpdateTick = currentTick;
        }
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Knowledge
{
    /// <summary>
    /// Applies incident learning events to agent memory in a deterministic, event-driven pass.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(IncidentLearningBootstrapSystem))]
    public partial struct IncidentLearningSystem : ISystem
    {
        private BufferLookup<IncidentLearningMemory> _memoryLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<IncidentLearningEventBuffer>();

            _memoryLookup = state.GetBufferLookup<IncidentLearningMemory>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<IncidentLearningEventBuffer>(out var eventEntity))
            {
                return;
            }

            var events = state.EntityManager.GetBuffer<IncidentLearningEvent>(eventEntity);
            if (events.Length == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var secondsPerTick = math.max(timeState.FixedDeltaTime, 1e-4f);
            var config = SystemAPI.HasSingleton<IncidentLearningConfig>()
                ? SystemAPI.GetSingleton<IncidentLearningConfig>()
                : IncidentLearningConfig.Default;
            var cooldownTicks = config.IncidentCooldownSeconds > 0f
                ? (uint)math.ceil(config.IncidentCooldownSeconds / secondsPerTick)
                : 0u;

            _memoryLookup.Update(ref state);

            for (int i = 0; i < events.Length; i++)
            {
                var incident = events[i];
                if (incident.Target == Entity.Null || !_memoryLookup.HasBuffer(incident.Target))
                {
                    continue;
                }

                var severity = math.clamp(incident.Severity, 0f, 1f);
                if (severity < config.MinSeverity)
                {
                    continue;
                }

                var incidentTick = incident.Tick != 0u ? incident.Tick : currentTick;
                var memories = _memoryLookup[incident.Target];
                var index = FindMemoryIndex(memories, incident.CategoryId);

                if (index < 0)
                {
                    if (config.MaxEntries > 0 && memories.Length >= config.MaxEntries)
                    {
                        var eviction = FindEvictionIndex(memories);
                        memories.RemoveAt(eviction);
                    }

                    index = memories.Length;
                    memories.Add(new IncidentLearningMemory
                    {
                        CategoryId = incident.CategoryId,
                        Bias = 0f,
                        RecentSeverity = 0f,
                        LastIncidentTick = 0u,
                        NextIncidentAllowedTick = 0u,
                        LastUpdateTick = incidentTick,
                        IncidentCount = 0,
                        NearMissCount = 0
                    });
                }

                var memory = memories[index];
                IncidentLearningUtility.ApplyDecay(ref memory, currentTick, secondsPerTick, config.MemoryDecayPerSecond, config.MinBias);

                if (currentTick < memory.NextIncidentAllowedTick)
                {
                    memories[index] = memory;
                    continue;
                }

                var gain = ResolveGain(in config, incident.Kind);
                memory.Bias = math.clamp(memory.Bias + severity * gain, config.MinBias, config.MaxBias);
                memory.RecentSeverity = math.max(memory.RecentSeverity, severity);
                memory.LastIncidentTick = incidentTick;
                memory.LastUpdateTick = currentTick;
                if (cooldownTicks > 0u)
                {
                    memory.NextIncidentAllowedTick = currentTick + cooldownTicks;
                }

                if (incident.Kind == IncidentLearningKind.NearMiss)
                {
                    memory.NearMissCount = (ushort)math.min(ushort.MaxValue, memory.NearMissCount + 1);
                }
                else
                {
                    memory.IncidentCount = (ushort)math.min(ushort.MaxValue, memory.IncidentCount + 1);
                }

                memories[index] = memory;
            }

            events.Clear();
        }

        private static int FindMemoryIndex(DynamicBuffer<IncidentLearningMemory> memories, in FixedString64Bytes categoryId)
        {
            for (int i = 0; i < memories.Length; i++)
            {
                if (memories[i].CategoryId.Equals(categoryId))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindEvictionIndex(DynamicBuffer<IncidentLearningMemory> memories)
        {
            var oldestTick = uint.MaxValue;
            var oldestIndex = 0;
            for (int i = 0; i < memories.Length; i++)
            {
                var tick = memories[i].LastIncidentTick;
                if (tick < oldestTick)
                {
                    oldestTick = tick;
                    oldestIndex = i;
                }
            }

            return oldestIndex;
        }

        private static float ResolveGain(in IncidentLearningConfig config, IncidentLearningKind kind)
        {
            return kind switch
            {
                IncidentLearningKind.Hit => config.MemoryGainOnHit,
                IncidentLearningKind.NearMiss => config.MemoryGainOnNearMiss,
                IncidentLearningKind.Observation => config.MemoryGainOnObservation,
                _ => config.MemoryGainDefault
            };
        }
    }
}

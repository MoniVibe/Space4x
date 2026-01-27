using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Captures replay-relevant events into a tooling-friendly buffer for deterministic validation.
    /// </summary>
    [UpdateInGroup(typeof(HistorySystemGroup))]
    public sealed partial class ReplayCaptureSystem : SystemBase
    {
        private EntityQuery _streamQuery;
        private List<ReplayCaptureEvent> _pendingEvents;

        protected override void OnCreate()
        {
            _pendingEvents = new List<ReplayCaptureEvent>(32);
            _streamQuery = GetEntityQuery(ComponentType.ReadWrite<ReplayCaptureStream>());
            EnsureStreamEntity();
        }

        protected override void OnDestroy()
        {
            _pendingEvents.Clear();
        }

        /// <summary>
        /// Enqueues an event for the current frame.
        /// </summary>
        public void RecordEvent(in ReplayCaptureEvent replayEvent)
        {
            _pendingEvents.Add(replayEvent);
        }

        /// <summary>
        /// Records an event if the replay capture system exists in the specified world.
        /// </summary>
        public static void RecordEvent(World world, ReplayableEvent.EventType type, uint tick, in FixedString64Bytes label, float value = 0f)
        {
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var captureSystem = world.GetExistingSystemManaged<ReplayCaptureSystem>();
            if (captureSystem == null)
            {
                return;
            }

            captureSystem.RecordEvent(new ReplayCaptureEvent
            {
                Tick = tick,
                Type = type,
                Label = label,
                Value = value
            });
        }

        /// <summary>
        /// Provides a label representing an event type for UI consumers.
        /// </summary>
        public static FixedString32Bytes GetEventTypeLabel(ReplayableEvent.EventType type)
        {
            return type switch
            {
                ReplayableEvent.EventType.Damage => "Damage",
                ReplayableEvent.EventType.Impulse => "Impulse",
                ReplayableEvent.EventType.Spawn => "Spawn",
                ReplayableEvent.EventType.Destroy => "Destroy",
                ReplayableEvent.EventType.StateChange => "State",
                ReplayableEvent.EventType.Custom => "Custom",
                _ => "Unknown"
            };
        }

        protected override void OnUpdate()
        {
            EnsureStreamEntity();

            if (_pendingEvents.Count == 0)
            {
                return;
            }

            var entity = _streamQuery.GetSingletonEntity();
            var buffer = EntityManager.GetBuffer<ReplayCaptureEvent>(entity);
            buffer.Clear();

            for (int i = 0; i < _pendingEvents.Count; i++)
            {
                buffer.Add(_pendingEvents[i]);
            }

            var stream = EntityManager.GetComponentData<ReplayCaptureStream>(entity);
            stream.EventCount = buffer.Length;
            stream.Version++;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                stream.LastTick = timeState.Tick;
            }

            if (buffer.Length > 0)
            {
                var latest = buffer[buffer.Length - 1];
                stream.LastEventType = latest.Type;
                stream.LastEventLabel = latest.Label;
            }

            EntityManager.SetComponentData(entity, stream);

            _pendingEvents.Clear();
        }

        private void EnsureStreamEntity()
        {
            if (_streamQuery.IsEmptyIgnoreFilter)
            {
                var entity = EntityManager.CreateEntity(typeof(ReplayCaptureStream));
                EntityManager.SetComponentData(entity, new ReplayCaptureStream
                {
                    Version = 0,
                    LastTick = 0,
                    EventCount = 0,
                    LastEventType = ReplayableEvent.EventType.Custom,
                    LastEventLabel = default
                });
                EntityManager.AddBuffer<ReplayCaptureEvent>(entity);
            }
            else
            {
                var entity = _streamQuery.GetSingletonEntity();
                if (!EntityManager.HasBuffer<ReplayCaptureEvent>(entity))
                {
                    EntityManager.AddBuffer<ReplayCaptureEvent>(entity);
                }
            }
        }
    }
}

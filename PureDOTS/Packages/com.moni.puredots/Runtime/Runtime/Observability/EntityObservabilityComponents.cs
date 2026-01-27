using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Observability
{
    /// <summary>
    /// Enables per-entity event logging for local observability/debugging.
    /// Opt-in by adding <see cref="EntityEventLogState" /> + <see cref="EntityEventLogEntry" /> buffer.
    /// </summary>
    public struct EntityEventLogState : IComponentData
    {
        public ushort WriteIndex;
        public ushort Capacity;
    }

    /// <summary>
    /// Compact per-entity event record. Keep payload small; use telemetry stream for large exports.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct EntityEventLogEntry : IBufferElementData
    {
        public uint Tick;
        public FixedString64Bytes EventId;
        public FixedString64Bytes Detail;
        public Entity Related;
    }

    public static class EntityEventLogUtility
    {
        public static void Append(ref EntityEventLogState state, DynamicBuffer<EntityEventLogEntry> buffer, uint tick, in FixedString64Bytes eventId, in FixedString64Bytes detail, Entity related = default)
        {
            if (state.Capacity == 0)
            {
                return;
            }

            if (buffer.Length < state.Capacity)
            {
                buffer.Add(new EntityEventLogEntry
                {
                    Tick = tick,
                    EventId = eventId,
                    Detail = detail,
                    Related = related
                });
                return;
            }

            var index = state.WriteIndex;
            if (index >= buffer.Length)
            {
                index = 0;
            }

            buffer[index] = new EntityEventLogEntry
            {
                Tick = tick,
                EventId = eventId,
                Detail = detail,
                Related = related
            };

            state.WriteIndex = (ushort)((index + 1) % state.Capacity);
        }
    }
}





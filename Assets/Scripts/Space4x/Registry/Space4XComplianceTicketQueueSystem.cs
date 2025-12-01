using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Collects compliance tickets across entities and writes them into a deterministic queue for planner/narrative systems.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XAffiliationComplianceSystem))]
    public partial struct Space4XComplianceTicketQueueSystem : ISystem
    {
        private EntityQuery _ticketQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _ticketQuery = SystemAPI.QueryBuilder()
                .WithAll<ComplianceTicket>()
                .Build();

            EnsureQueueEntity(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_ticketQuery.IsEmptyIgnoreFilter)
            {
                ClearQueue(ref state);
                return;
            }

            var queueEntity = EnsureQueueEntity(ref state);
            var queue = SystemAPI.GetBuffer<ComplianceTicketQueueEntry>(queueEntity);
            queue.Clear();

            var entries = new NativeList<QueueEntry>(Allocator.Temp);
            foreach (var (tickets, entity) in SystemAPI.Query<DynamicBuffer<ComplianceTicket>>().WithEntityAccess())
            {
                for (int i = 0; i < tickets.Length; i++)
                {
                    var ticket = tickets[i];
                    entries.Add(new QueueEntry
                    {
                        Source = entity,
                        Affiliation = ticket.Affiliation,
                        Type = ticket.Type,
                        Severity = ticket.Severity,
                        Tick = ticket.Tick
                    });
                }
            }

            entries.Sort(new TicketComparer());

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                queue.Add(new ComplianceTicketQueueEntry
                {
                    Source = entry.Source,
                    Affiliation = entry.Affiliation,
                    Type = entry.Type,
                    Severity = entry.Severity,
                    Tick = entry.Tick
                });
            }

            entries.Dispose();
        }

        private Entity EnsureQueueEntity(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<ComplianceTicketQueue>(out var entity))
            {
                if (!state.EntityManager.HasBuffer<ComplianceTicketQueueEntry>(entity))
                {
                    state.EntityManager.AddBuffer<ComplianceTicketQueueEntry>(entity);
                }
                return entity;
            }

            entity = state.EntityManager.CreateEntity(typeof(ComplianceTicketQueue));
            state.EntityManager.AddBuffer<ComplianceTicketQueueEntry>(entity);
            return entity;
        }

        private void ClearQueue(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<ComplianceTicketQueue>(out var entity) &&
                state.EntityManager.HasBuffer<ComplianceTicketQueueEntry>(entity))
            {
                var queue = state.EntityManager.GetBuffer<ComplianceTicketQueueEntry>(entity);
                queue.Clear();
            }
        }

        private struct QueueEntry
        {
            public Entity Source;
            public Entity Affiliation;
            public ComplianceBreachType Type;
            public half Severity;
            public uint Tick;
        }

        private struct TicketComparer : IComparer<QueueEntry>
        {
            public int Compare(QueueEntry x, QueueEntry y)
            {
                var tickCompare = x.Tick.CompareTo(y.Tick);
                if (tickCompare != 0)
                {
                    return tickCompare;
                }

                var typeCompare = x.Type.CompareTo(y.Type);
                if (typeCompare != 0)
                {
                    return typeCompare;
                }

                return x.Source.Index.CompareTo(y.Source.Index);
            }
        }
    }
}

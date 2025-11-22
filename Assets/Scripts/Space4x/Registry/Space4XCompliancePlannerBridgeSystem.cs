using PureDOTS.Systems;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Copies compliance tickets into a planner inbox for AI/narrative systems.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XComplianceTicketQueueSystem))]
    public partial struct Space4XCompliancePlannerBridgeSystem : ISystem
    {
        private EntityQuery _queueQuery;

        public void OnCreate(ref SystemState state)
        {
            _queueQuery = SystemAPI.QueryBuilder()
                .WithAll<ComplianceTicketQueue, ComplianceTicketQueueEntry>()
                .Build();

            EnsureInbox(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_queueQuery.IsEmptyIgnoreFilter)
            {
                ClearInbox(ref state);
                return;
            }

            var queueEntity = _queueQuery.GetSingletonEntity();
            var queue = state.EntityManager.GetBuffer<ComplianceTicketQueueEntry>(queueEntity);
            var inboxEntity = EnsureInbox(ref state);
            var inbox = state.EntityManager.GetBuffer<CompliancePlannerTicket>(inboxEntity);
            inbox.Clear();

            for (int i = 0; i < queue.Length; i++)
            {
                var entry = queue[i];
                inbox.Add(new CompliancePlannerTicket
                {
                    Source = entry.Source,
                    Affiliation = entry.Affiliation,
                    Type = entry.Type,
                    Severity = entry.Severity,
                    Tick = entry.Tick
                });
            }
        }

        private Entity EnsureInbox(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<CompliancePlannerInbox>(out var entity))
            {
                if (!state.EntityManager.HasBuffer<CompliancePlannerTicket>(entity))
                {
                    state.EntityManager.AddBuffer<CompliancePlannerTicket>(entity);
                }

                return entity;
            }

            entity = state.EntityManager.CreateEntity(typeof(CompliancePlannerInbox));
            state.EntityManager.AddBuffer<CompliancePlannerTicket>(entity);
            return entity;
        }

        private void ClearInbox(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<CompliancePlannerInbox>(out var entity) &&
                state.EntityManager.HasBuffer<CompliancePlannerTicket>(entity))
            {
                var inbox = state.EntityManager.GetBuffer<CompliancePlannerTicket>(entity);
                inbox.Clear();
            }
        }
    }
}

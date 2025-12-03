using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Consumes compliance tickets from the queue and triggers mutiny/desertion state changes.
    /// Mutiny: Entity switches affiliation or becomes hostile
    /// Desertion: Entity despawns or leaves
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XComplianceTicketQueueSystem))]
    public partial struct Space4XMutinySystem : ISystem
    {
        private EntityQuery _queueQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<ComplianceTicketQueue>();

            _queueQuery = SystemAPI.QueryBuilder()
                .WithAll<ComplianceTicketQueue, ComplianceTicketQueueEntry>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_queueQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var queueEntity = _queueQuery.GetSingletonEntity();
            var queue = SystemAPI.GetBuffer<ComplianceTicketQueueEntry>(queueEntity);

            if (queue.Length == 0)
            {
                return;
            }

            var mutinyLookup = state.GetComponentLookup<MutinyState>(false);
            var affiliationLookup = state.GetBufferLookup<AffiliationTag>(false);
            mutinyLookup.Update(ref state);
            affiliationLookup.Update(ref state);

            // Process tickets in order
            for (int i = 0; i < queue.Length; i++)
            {
                var ticket = queue[i];
                
                // Only process tickets from current or past ticks
                if (ticket.Tick > timeState.Tick)
                {
                    continue;
                }

                ProcessTicket(ref state, ticket, mutinyLookup, affiliationLookup, timeState.Tick);
            }
        }

        [BurstCompile]
        private void ProcessTicket(ref SystemState state, ComplianceTicketQueueEntry ticket, ComponentLookup<MutinyState> mutinyLookup, BufferLookup<AffiliationTag> affiliationLookup, uint currentTick)
        {
            var sourceEntity = ticket.Source;
            if (!state.EntityManager.Exists(sourceEntity))
            {
                return;
            }

            // Add or update mutiny state
            if (!mutinyLookup.HasComponent(sourceEntity))
            {
                state.EntityManager.AddComponentData(sourceEntity, new MutinyState
                {
                    State = MutinyStateType.None,
                    TriggerTick = 0,
                    Severity = 0f
                });
                mutinyLookup.Update(ref state);
            }

            var mutinyState = mutinyLookup.GetRefRW(sourceEntity);
            var severity = (float)ticket.Severity;

            // Determine action based on breach type
            switch (ticket.Type)
            {
                case ComplianceBreachType.Mutiny:
                    HandleMutiny(ref state, sourceEntity, mutinyState, affiliationLookup, severity, currentTick);
                    break;

                case ComplianceBreachType.Desertion:
                    HandleDesertion(ref state, sourceEntity, mutinyState, severity, currentTick);
                    break;

                case ComplianceBreachType.Independence:
                    HandleIndependence(ref state, sourceEntity, mutinyState, affiliationLookup, severity, currentTick);
                    break;
            }
        }

        [BurstCompile]
        private void HandleMutiny(ref SystemState state, Entity entity, RefRW<MutinyState> mutinyState, BufferLookup<AffiliationTag> affiliationLookup, float severity, uint currentTick)
        {
            // Mutiny: Switch affiliation or become hostile
            mutinyState.ValueRW.State = MutinyStateType.Mutiny;
            mutinyState.ValueRW.TriggerTick = currentTick;
            mutinyState.ValueRW.Severity = severity;

            // Clear existing affiliations and add hostile tag
            if (affiliationLookup.HasBuffer(entity))
            {
                var affiliations = affiliationLookup[entity];
                affiliations.Clear();
                
                // Optionally add a "Hostile" or "Rebel" affiliation
                // For now, just clear affiliations to mark as mutinied
            }

            // Mark entity for potential hostile behavior (could be used by combat systems)
            if (!state.EntityManager.HasComponent<MutiniedTag>(entity))
            {
                state.EntityManager.AddComponent<MutiniedTag>(entity);
            }
        }

        [BurstCompile]
        private void HandleDesertion(ref SystemState state, Entity entity, RefRW<MutinyState> mutinyState, float severity, uint currentTick)
        {
            // Desertion: Mark for despawning or leaving
            mutinyState.ValueRW.State = MutinyStateType.Desertion;
            mutinyState.ValueRW.TriggerTick = currentTick;
            mutinyState.ValueRW.Severity = severity;

            // Mark for despawning (actual despawning handled by another system or immediately)
            if (!state.EntityManager.HasComponent<DesertedTag>(entity))
            {
                state.EntityManager.AddComponent<DesertedTag>(entity);
            }

            // Optionally despawn immediately if severity is high
            if (severity >= 0.8f)
            {
                state.EntityManager.DestroyEntity(entity);
            }
        }

        [BurstCompile]
        private void HandleIndependence(ref SystemState state, Entity entity, RefRW<MutinyState> mutinyState, BufferLookup<AffiliationTag> affiliationLookup, float severity, uint currentTick)
        {
            // Independence: Entity becomes neutral/independent
            mutinyState.ValueRW.State = MutinyStateType.Independence;
            mutinyState.ValueRW.TriggerTick = currentTick;
            mutinyState.ValueRW.Severity = severity;

            // Clear affiliations to make entity independent
            if (affiliationLookup.HasBuffer(entity))
            {
                var affiliations = affiliationLookup[entity];
                affiliations.Clear();
            }

            if (!state.EntityManager.HasComponent<IndependentTag>(entity))
            {
                state.EntityManager.AddComponent<IndependentTag>(entity);
            }
        }
    }

    /// <summary>
    /// Tracks mutiny/desertion state for an entity.
    /// </summary>
    public struct MutinyState : IComponentData
    {
        public MutinyStateType State;
        public uint TriggerTick;
        public float Severity;
    }

    /// <summary>
    /// Types of mutiny states.
    /// </summary>
    public enum MutinyStateType : byte
    {
        None = 0,
        Mutiny = 1,
        Desertion = 2,
        Independence = 3
    }

    /// <summary>
    /// Tag component indicating entity has mutinied.
    /// </summary>
    public struct MutiniedTag : IComponentData { }

    /// <summary>
    /// Tag component indicating entity has deserted.
    /// </summary>
    public struct DesertedTag : IComponentData { }

    /// <summary>
    /// Tag component indicating entity is independent.
    /// </summary>
    public struct IndependentTag : IComponentData { }
}


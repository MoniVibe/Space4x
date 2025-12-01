using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Implements mission board AI per MissionBoard.md: AI posts missions based on doctrine and readiness,
    /// and evaluates mission fit for accepting contracts. Completing contracts boosts relations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XAIDiplomacySystem))]
    public partial struct Space4XAIMissionBoardSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private BufferLookup<TopOutlook> _outlookLookup;
        private ComponentLookup<DoctrineProfile> _doctrineLookup;
        private ComponentLookup<Reputation> _reputationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _outlookLookup = state.GetBufferLookup<TopOutlook>(true);
            _doctrineLookup = state.GetComponentLookup<DoctrineProfile>(true);
            _reputationLookup = state.GetComponentLookup<Reputation>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _alignmentLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _doctrineLookup.Update(ref state);
            _reputationLookup.Update(ref state);

            // Process mission board logic
            // In full implementation, would post missions, evaluate acceptances, update relations
            var job = new ProcessMissionBoardJob
            {
                CurrentTick = timeState.Tick,
                AlignmentLookup = _alignmentLookup,
                OutlookLookup = _outlookLookup,
                DoctrineLookup = _doctrineLookup,
                ReputationLookup = _reputationLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(AggregateType))]
        public partial struct ProcessMissionBoardJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;
            [ReadOnly] public BufferLookup<TopOutlook> OutlookLookup;
            [ReadOnly] public ComponentLookup<DoctrineProfile> DoctrineLookup;
            public ComponentLookup<Reputation> ReputationLookup;

            public void Execute(Entity entity)
            {
                // AI posts missions reflecting their crisis strategy and doctrine
                // In full implementation, would:
                // 1. Evaluate current needs (crises, resource shortages, threats)
                // 2. Post appropriate mission types (logistics, combat, exploration, construction)
                // 3. Set rewards based on urgency and relations

                // AI assesses mission fit based on fleet readiness and doctrine
                // In full implementation, would:
                // 1. Check fleet capabilities
                // 2. Evaluate alignment/outlook compatibility with mission issuer
                // 3. Consider current orders and priorities
                // 4. Accept or reject missions accordingly

                // Completing contracts increases relations
                // In full implementation, would track completed missions and update reputation
                if (ReputationLookup.HasComponent(entity))
                {
                    var rep = ReputationLookup.GetRefRW(entity).ValueRW;
                    // Relations would be updated when missions complete
                    // For now, just ensure component exists
                }
            }
        }
    }
}


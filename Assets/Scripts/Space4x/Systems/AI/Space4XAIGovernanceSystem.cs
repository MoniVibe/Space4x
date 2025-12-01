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
    /// Implements AI governance per AIGovernance.md: crisis responses based on outlooks, stockpile management,
    /// and construction priorities. Warlike invest in defenses, lawful good support allies, corrupt use cynical tactics.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XFleetCoordinationAISystem))]
    public partial struct Space4XAIGovernanceSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private BufferLookup<TopOutlook> _outlookLookup;
        private BufferLookup<EthicAxisValue> _axisLookup;
        private ComponentLookup<DoctrineProfile> _doctrineLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _outlookLookup = state.GetBufferLookup<TopOutlook>(true);
            _axisLookup = state.GetBufferLookup<EthicAxisValue>(true);
            _doctrineLookup = state.GetComponentLookup<DoctrineProfile>(true);
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
            _axisLookup.Update(ref state);
            _doctrineLookup.Update(ref state);

            // Process governance decisions for aggregate entities (colonies, factions, empires)
            // This is a simplified version - full implementation would evaluate crises, resources, etc.
            var job = new ProcessGovernanceJob
            {
                CurrentTick = timeState.Tick,
                AlignmentLookup = _alignmentLookup,
                OutlookLookup = _outlookLookup,
                AxisLookup = _axisLookup,
                DoctrineLookup = _doctrineLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(AggregateType))]
        public partial struct ProcessGovernanceJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;
            [ReadOnly] public BufferLookup<TopOutlook> OutlookLookup;
            [ReadOnly] public BufferLookup<EthicAxisValue> AxisLookup;
            [ReadOnly] public ComponentLookup<DoctrineProfile> DoctrineLookup;

            public void Execute(Entity entity)
            {
                // Get alignment and outlooks to determine governance behavior
                if (!AlignmentLookup.HasComponent(entity))
                {
                    return;
                }

                var alignment = AlignmentLookup[entity];
                var lawfulness = AlignmentMath.Lawfulness(alignment);
                var integrity = AlignmentMath.IntegrityNormalized(alignment);
                var good = math.saturate(0.5f * (1f + (float)alignment.Good));

                // Get war axis value
                float warAxis = 0f;
                if (AxisLookup.HasBuffer(entity))
                {
                    var axes = AxisLookup[entity];
                    for (int i = 0; i < axes.Length; i++)
                    {
                        if (axes[i].Axis == EthicAxisId.War)
                        {
                            warAxis = (float)axes[i].Value;
                            break;
                        }
                    }
                }

                // Determine crisis posture based on outlook
                // Warlike: Allocate resources to offensive/defensive capabilities
                if (warAxis > 0.5f)
                {
                    // Warlike governance - invest in defenses and strikes
                    // In full implementation, would queue construction orders, fleet deployments, etc.
                }

                // Lawful Good/Pure Altruists: Offer aid to allies, strengthen defenses
                if (lawfulness > 0.7f && good > 0.7f && integrity > 0.7f)
                {
                    // Altruistic governance - support allies
                    // In full implementation, would post aid missions, offer resources, etc.
                }

                // Corrupt/Low Integrity: Use allies as shields, prioritize self-preservation
                if (integrity < 0.3f)
                {
                    // Corrupt governance - cynical tactics
                    // In full implementation, would manipulate relations, exploit allies, etc.
                }

                // Chaotic: Erratic responses but trend toward balanced preparation
                var chaos = AlignmentMath.Chaos(alignment);
                if (chaos > 0.6f)
                {
                    // Chaotic governance - unpredictable but balanced
                    // In full implementation, would occasionally make unexpected decisions
                }

                // Stockpile management: AI aims to stockpile up to usable thresholds
                // When storage reaches capacity, prioritize constructing additional storage
                // (Handled by other systems in full implementation)
            }
        }
    }
}


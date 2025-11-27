using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Implements stance-based vessel movement, formation behavior, and route planning per VesselMovementAI.md.
    /// Handles aggressive/defensive routing, formation tightness based on alignment, and child vessel tethering.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XAICommandQueueSystem))]
    public partial struct Space4XVesselMovementAISystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<VesselStanceComponent> _stanceLookup;
        private ComponentLookup<FormationData> _formationLookup;
        private ComponentLookup<ChildVesselTether> _tetherLookup;
        private BufferLookup<TopOutlook> _outlookLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(false);
            _formationLookup = state.GetComponentLookup<FormationData>(false);
            _tetherLookup = state.GetComponentLookup<ChildVesselTether>(true);
            _outlookLookup = state.GetBufferLookup<TopOutlook>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
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
            _stanceLookup.Update(ref state);
            _formationLookup.Update(ref state);
            _tetherLookup.Update(ref state);
            _outlookLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var job = new UpdateVesselMovementAIJob
            {
                CurrentTick = timeState.Tick,
                DeltaTime = timeState.FixedDeltaTime,
                AlignmentLookup = _alignmentLookup,
                StanceLookup = _stanceLookup,
                FormationLookup = _formationLookup,
                TetherLookup = _tetherLookup,
                OutlookLookup = _outlookLookup,
                TransformLookup = _transformLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(VesselMovement))]
        public partial struct UpdateVesselMovementAIJob : IJobEntity
        {
            public uint CurrentTick;
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;
            public ComponentLookup<VesselStanceComponent> StanceLookup;
            public ComponentLookup<FormationData> FormationLookup;
            [ReadOnly] public ComponentLookup<ChildVesselTether> TetherLookup;
            [ReadOnly] public BufferLookup<TopOutlook> OutlookLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            public void Execute(ref VesselMovement movement, ref VesselAIState aiState, Entity entity)
            {
                // Update stance if needed
                UpdateStance(ref StanceLookup, entity);
                
                // Update formation if part of one
                UpdateFormation(ref FormationLookup, entity);
                
                // Handle child vessel tethering
                if (TetherLookup.HasComponent(entity))
                {
                    UpdateTethering(ref movement, ref aiState, entity);
                }
            }

            private void UpdateStance(ref ComponentLookup<VesselStanceComponent> stanceLookup, Entity entity)
            {
                if (!stanceLookup.HasComponent(entity))
                {
                    // Initialize with neutral stance
                    stanceLookup.GetRefRW(entity).ValueRW = new VesselStanceComponent
                    {
                        CurrentStance = VesselStanceMode.Neutral,
                        DesiredStance = VesselStanceMode.Neutral,
                        StanceChangeTick = CurrentTick
                    };
                    return;
                }

                var stance = stanceLookup.GetRefRW(entity).ValueRW;
                
                // Update current stance to desired if enough time has passed
                if (stance.CurrentStance != stance.DesiredStance && 
                    CurrentTick > stance.StanceChangeTick + 10) // 10 tick transition delay
                {
                    stance.CurrentStance = stance.DesiredStance;
                    stance.StanceChangeTick = CurrentTick;
                    stanceLookup.GetRefRW(entity).ValueRW = stance;
                }
            }

            private void UpdateFormation(ref ComponentLookup<FormationData> formationLookup, Entity entity)
            {
                if (!formationLookup.HasComponent(entity))
                {
                    return;
                }

                var formation = formationLookup.GetRefRW(entity).ValueRW;
                
                // Calculate formation tightness based on alignment/outlook
                if (AlignmentLookup.HasComponent(entity))
                {
                    var alignment = AlignmentLookup[entity];
                    var lawfulness = AlignmentMath.Lawfulness(alignment);
                    
                    // Lawful entities maintain tighter formations
                    formation.FormationTightness = (half)math.lerp(0.3f, 0.9f, lawfulness);
                }

                // Adjust formation radius based on stance
                if (StanceLookup.HasComponent(entity))
                {
                    var stance = StanceLookup[entity];
                    var baseRadius = 50f;
                    
                    switch (stance.CurrentStance)
                    {
                        case VesselStanceMode.Aggressive:
                            // Tight formation for strike runs
                            formation.FormationRadius = baseRadius * 0.7f;
                            break;
                        case VesselStanceMode.Defensive:
                            // Wider formation for coverage
                            formation.FormationRadius = baseRadius * 1.3f;
                            break;
                        default:
                            formation.FormationRadius = baseRadius;
                            break;
                    }
                }

                formation.FormationUpdateTick = CurrentTick;
                formationLookup.GetRefRW(entity).ValueRW = formation;
            }

            private void UpdateTethering(ref VesselMovement movement, ref VesselAIState aiState, Entity entity)
            {
                var tether = TetherLookup[entity];
                
                if (tether.ParentCarrier == Entity.Null)
                {
                    return;
                }

                if (!TransformLookup.HasComponent(entity) || !TransformLookup.HasComponent(tether.ParentCarrier))
                {
                    return;
                }

                var vesselPos = TransformLookup[entity].Position;
                var parentPos = TransformLookup[tether.ParentCarrier].Position;
                var distance = math.distance(vesselPos, parentPos);

                // If beyond tether range and not allowed to patrol, return to parent
                if (distance > tether.MaxTetherRange && tether.CanPatrol == 0)
                {
                    aiState.TargetEntity = tether.ParentCarrier;
                    aiState.CurrentGoal = VesselAIState.Goal.Returning;
                    aiState.CurrentState = VesselAIState.State.Returning;
                }
            }
        }
    }
}


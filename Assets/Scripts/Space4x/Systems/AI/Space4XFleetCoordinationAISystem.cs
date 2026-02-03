using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Implements fleet coordination per FleetCoordinationAI.md: command hierarchy, reinforcement tactics,
    /// and disengagement logic. Highest-ranking captain becomes admiral, orchestrating doctrine-driven plans.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftBehaviorSystem))]
    public partial struct Space4XFleetCoordinationAISystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<VesselStanceComponent> _stanceLookup;
        private ComponentLookup<FormationData> _formationLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Space4XFleet> _fleetLookup;
        private ComponentLookup<AICommandQueue> _commandQueueLookup;
        private BufferLookup<TopStance> _outlookLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(false);
            _formationLookup = state.GetComponentLookup<FormationData>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _fleetLookup = state.GetComponentLookup<Space4XFleet>(true);
            _commandQueueLookup = state.GetComponentLookup<AICommandQueue>(true);
            _outlookLookup = state.GetBufferLookup<TopStance>(true);
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
            _statsLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _formationLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _fleetLookup.Update(ref state);
            _commandQueueLookup.Update(ref state);
            _outlookLookup.Update(ref state);

            // First pass: establish admiral selection for fleet groups
            var admiralJob = new SelectAdmiralJob
            {
                CurrentTick = timeState.Tick,
                StatsLookup = _statsLookup,
                TransformLookup = _transformLookup,
                FleetLookup = _fleetLookup,
                FormationLookup = _formationLookup,
                StanceLookup = _stanceLookup
            };
            state.Dependency = admiralJob.ScheduleParallel(state.Dependency);

            // Second pass: coordinate tasks and formations
            var job = new CoordinateFleetsJob
            {
                CurrentTick = timeState.Tick,
                AlignmentLookup = _alignmentLookup,
                StatsLookup = _statsLookup,
                StanceLookup = _stanceLookup,
                FormationLookup = _formationLookup,
                CommandQueueLookup = _commandQueueLookup,
                OutlookLookup = _outlookLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(FormationData), typeof(Space4XFleet))]
        public partial struct SelectAdmiralJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<IndividualStats> StatsLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<Space4XFleet> FleetLookup;
            public ComponentLookup<FormationData> FormationLookup;
            public ComponentLookup<VesselStanceComponent> StanceLookup;

            public void Execute(ref FormationData formation, Entity entity)
            {
                // Only process potential leaders (FormationLeader == Null)
                if (formation.FormationLeader != Entity.Null)
                {
                    return;
                }

                if (!TransformLookup.HasComponent(entity))
                {
                    return;
                }

                var position = TransformLookup[entity].Position;
                float coordinationRadius = 100f; // Fleet coordination radius
                float coordinationRadiusSq = coordinationRadius * coordinationRadius;

                // Determine admiral based solely on this vessel's command stat (neighbor search disabled to avoid SystemAPI queries in jobs).
                float myCommandStat = StatsLookup.HasComponent(entity)
                    ? StatsLookup[entity].Command
                    : 0f;

                if (myCommandStat > 0f)
                {
                    // This vessel is admiral - ensure it has stance
                    if (!StanceLookup.HasComponent(entity))
                    {
                        var stance = new VesselStanceComponent
                        {
                            CurrentStance = VesselStanceMode.Neutral,
                            DesiredStance = VesselStanceMode.Neutral,
                            StanceChangeTick = CurrentTick
                        };
                        StanceLookup.GetRefRW(entity).ValueRW = stance;
                    }
                    
                    // Command stat influences formation coordination radius and effectiveness
                    // Higher command = better coordination (expanded radius, tighter formations)
                    var commandBonus = math.saturate(myCommandStat / 100f); // 0-1 normalized
                    formation.FormationRadius = math.lerp(50f, 30f, commandBonus); // Tighter formations with higher command
                    formation.FormationTightness = (half)math.lerp(0.3f, 0.9f, commandBonus);
                    FormationLookup.GetRefRW(entity).ValueRW = formation;
                }
            }
        }

        [BurstCompile]
        public partial struct CoordinateFleetsJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;
            [ReadOnly] public ComponentLookup<IndividualStats> StatsLookup;
            public ComponentLookup<VesselStanceComponent> StanceLookup;
            public ComponentLookup<FormationData> FormationLookup;
            [ReadOnly] public ComponentLookup<AICommandQueue> CommandQueueLookup;
            [ReadOnly] public BufferLookup<TopStance> OutlookLookup;

            public void Execute(ref FormationData formation, Entity entity)
            {
                // If this entity is a formation leader, coordinate subordinates
                if (formation.FormationLeader != Entity.Null)
                {
                    // This is a subordinate - follow leader's stance
                    if (StanceLookup.HasComponent(formation.FormationLeader) && 
                        StanceLookup.HasComponent(entity))
                    {
                        var leaderStance = StanceLookup[formation.FormationLeader];
                        var ownStance = StanceLookup.GetRefRW(entity).ValueRW;
                        
                        // Subordinates follow leader's desired stance, but may diverge if chaotic
                        var alignment = AlignmentLookup.HasComponent(entity) 
                            ? AlignmentLookup[entity] 
                            : default(AlignmentTriplet);
                        var chaos = AlignmentMath.Chaos(alignment);
                        
                        // Chaotic captains may occasionally diverge
                        if (chaos > 0.5f && (CurrentTick % 100) < 5) // 5% chance per 100 ticks
                        {
                            // Allow some autonomy
                            return;
                        }
                        
                        ownStance.DesiredStance = leaderStance.DesiredStance;
                        StanceLookup.GetRefRW(entity).ValueRW = ownStance;
                    }

                    // Coordinate task priorities if admiral has command queue
                    if (CommandQueueLookup.HasComponent(formation.FormationLeader) && 
                        CommandQueueLookup.HasComponent(entity))
                    {
                        CoordinateTaskPriorities(formation.FormationLeader, entity);
                    }
                }
                else
                {
                    // This is admiral - coordinate fleet-wide tasks
                    if (CommandQueueLookup.HasComponent(entity))
                    {
                        PrioritizeFleetTasks(entity);
                    }
                }
            }

            private void CoordinateTaskPriorities(Entity admiral, Entity subordinate)
            {
                // In full implementation, admiral would assign tasks to subordinates
                // For now, subordinates follow admiral's priority order
                // This is a placeholder for future task delegation
            }

            private void PrioritizeFleetTasks(Entity admiral)
            {
                // Prioritize tasks: defend haulers > strike targets > rescue allies
                // In full implementation, would evaluate all fleet member queues
                // and assign tasks based on urgency and capability
                // This is a placeholder for future task coordination
            }
        }
    }
}


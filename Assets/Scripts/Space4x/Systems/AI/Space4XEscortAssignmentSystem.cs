using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Keeps escort vessels tethered to their assigned carrier and releases them on schedule.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    public partial struct Space4XEscortAssignmentSystem : ISystem
    {
        private ComponentLookup<ChildVesselTether> _tetherLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EscortAssignment>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _tetherLookup = state.GetComponentLookup<ChildVesselTether>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _tetherLookup.Update(ref state);
            var currentTick = time.Tick;

            foreach (var (assignment, aiState, entity) in SystemAPI.Query<RefRW<EscortAssignment>, RefRW<VesselAIState>>()
                         .WithEntityAccess())
            {
                if (assignment.ValueRO.Released != 0)
                {
                    continue;
                }

                if (assignment.ValueRO.Target != Entity.Null)
                {
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Escort;
                    aiState.ValueRW.CurrentState = VesselAIState.State.MovingToTarget;
                    aiState.ValueRW.TargetEntity = assignment.ValueRO.Target;
                }

                if (assignment.ValueRO.ReleaseTick != 0 && currentTick >= assignment.ValueRO.ReleaseTick)
                {
                    assignment.ValueRW.Released = 1;
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.None;
                    aiState.ValueRW.CurrentState = VesselAIState.State.Idle;
                    aiState.ValueRW.TargetEntity = Entity.Null;
                    aiState.ValueRW.TargetPosition = float3.zero;

                    if (_tetherLookup.HasComponent(entity))
                    {
                        var tether = _tetherLookup.GetRefRW(entity);
                        tether.ValueRW.ParentCarrier = Entity.Null;
                        tether.ValueRW.CanPatrol = 1;
                    }
                }
            }
        }
    }
}

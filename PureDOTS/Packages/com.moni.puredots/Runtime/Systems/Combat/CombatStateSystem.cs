using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Combat.State;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Manages combat state transitions and timed state durations.
    /// Processes state change requests and emits state change events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct CombatStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.DeltaTime;
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process state change requests
            foreach (var (request, stateData, events, entity) in SystemAPI
                .Query<RefRO<CombatStateChangeRequest>, RefRW<CombatStateData>, DynamicBuffer<CombatStateChangeEvent>>()
                .WithEntityAccess())
            {
                var req = request.ValueRO;
                var currentState = stateData.ValueRO.Current;

                // Check if transition is valid
                bool canTransition = req.Force ||
                    CombatStateHelpers.IsValidTransition(currentState, req.RequestedState);

                if (canTransition)
                {
                    // Emit state change event
                    events.Add(new CombatStateChangeEvent
                    {
                        AffectedEntity = entity,
                        FromState = currentState,
                        ToState = req.RequestedState,
                        Tick = currentTick,
                        CauseEntity = req.CauseEntity
                    });

                    // Update state
                    var newStateData = stateData.ValueRO;
                    newStateData.Previous = currentState;
                    newStateData.Current = req.RequestedState;
                    newStateData.StateEnteredTick = currentTick;

                    // Set duration for timed states
                    if (req.RequestedState == CombatState.Stunned)
                    {
                        newStateData.StunDuration = req.Duration > 0 ? req.Duration : 1f;
                    }
                    else if (req.RequestedState == CombatState.Recovering)
                    {
                        newStateData.RecoveryTime = req.Duration > 0 ? req.Duration : 0.5f;
                    }
                    else if (req.RequestedState == CombatState.KnockedDown)
                    {
                        newStateData.RecoveryTime = req.Duration > 0 ? req.Duration : 2f;
                    }

                    stateData.ValueRW = newStateData;
                }

                // Remove request
                ecb.RemoveComponent<CombatStateChangeRequest>(entity);
            }

            // Update timed states
            new UpdateTimedStatesJob
            {
                DeltaTime = deltaTime,
                CurrentTick = currentTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct UpdateTimedStatesJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref CombatStateData stateData,
                ref DynamicBuffer<CombatStateChangeEvent> events)
            {
                switch (stateData.Current)
                {
                    case CombatState.Stunned:
                        stateData.StunDuration -= DeltaTime;
                        if (stateData.StunDuration <= 0f)
                        {
                            // Transition to recovering
                            TransitionTo(ref stateData, ref events, entity, CombatState.Recovering, CurrentTick);
                            stateData.RecoveryTime = 0.5f; // Brief recovery after stun
                        }
                        break;

                    case CombatState.Recovering:
                        stateData.RecoveryTime -= DeltaTime;
                        if (stateData.RecoveryTime <= 0f)
                        {
                            // Return to idle or engaged based on context
                            TransitionTo(ref stateData, ref events, entity, CombatState.Idle, CurrentTick);
                        }
                        break;

                    case CombatState.KnockedDown:
                        stateData.RecoveryTime -= DeltaTime;
                        if (stateData.RecoveryTime <= 0f)
                        {
                            // Get up - transition to recovering
                            TransitionTo(ref stateData, ref events, entity, CombatState.Recovering, CurrentTick);
                            stateData.RecoveryTime = 0.3f;
                        }
                        break;
                }
            }

            private static void TransitionTo(
                ref CombatStateData stateData,
                ref DynamicBuffer<CombatStateChangeEvent> events,
                Entity entity,
                CombatState newState,
                uint tick)
            {
                events.Add(new CombatStateChangeEvent
                {
                    AffectedEntity = entity,
                    FromState = stateData.Current,
                    ToState = newState,
                    Tick = tick,
                    CauseEntity = Entity.Null
                });

                stateData.Previous = stateData.Current;
                stateData.Current = newState;
                stateData.StateEnteredTick = tick;
            }
        }
    }

    /// <summary>
    /// Auto-flee system - triggers flee state when health is low.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(CombatStateSystem))]
    public partial struct CombatAutoFleeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new AutoFleeJob
            {
                Ecb = ecb
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct AutoFleeJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                in CombatStateData stateData,
                in CombatStateConfig config,
                in Health health)
            {
                // Skip if can't auto-flee or already fleeing/dead
                if (!config.CanAutoFlee ||
                    stateData.Current == CombatState.Fleeing ||
                    stateData.Current == CombatState.Dead ||
                    CombatStateHelpers.IsIncapacitated(stateData.Current))
                {
                    return;
                }

                // Check health threshold
                float healthPercent = health.MaxHealth > 0 ? health.Current / health.MaxHealth : 0f;
                if (healthPercent <= config.FleeHealthThreshold)
                {
                    // Request flee state
                    Ecb.AddComponent(entityInQueryIndex, entity, new CombatStateChangeRequest
                    {
                        RequestedState = CombatState.Fleeing,
                        Duration = 0f,
                        CauseEntity = Entity.Null,
                        Force = false
                    });
                }
            }
        }
    }

    /// <summary>
    /// Death detection system - transitions to dead state when health reaches zero.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(CombatStateSystem))]
    public partial struct CombatDeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new DeathDetectionJob
            {
                Ecb = ecb
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct DeathDetectionJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                in CombatStateData stateData,
                in Health health)
            {
                // Skip if already dead
                if (stateData.Current == CombatState.Dead)
                {
                    return;
                }

                // Check if health is zero or below
                if (health.Current <= 0f)
                {
                    // Force transition to dead
                    Ecb.AddComponent(entityInQueryIndex, entity, new CombatStateChangeRequest
                    {
                        RequestedState = CombatState.Dead,
                        Duration = 0f,
                        CauseEntity = Entity.Null,
                        Force = true // Death overrides all states
                    });
                }
            }
        }
    }
}


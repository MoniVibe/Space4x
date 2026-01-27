using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using PureDOTS.Runtime.AI.Routine;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Systems.AI
{
    /// <summary>
    /// System that manages the global day/night cycle.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct DayCycleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<DayCycleState>();
            state.RequireForUpdate<RoutineConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var config = SystemAPI.GetSingleton<RoutineConfig>();
            var dayCycle = SystemAPI.GetSingletonRW<DayCycleState>();

            if (dayCycle.ValueRW.IsPaused)
                return;

            // Advance time
            float hoursPerSecond = 24f / config.DayLengthSeconds;
            float hourAdvance = timeState.DeltaTime * hoursPerSecond * dayCycle.ValueRW.TimeScale;
            
            float newHour = dayCycle.ValueRW.CurrentHour + hourAdvance;
            
            // Check for day rollover
            if (newHour >= 24f)
            {
                newHour -= 24f;
                dayCycle.ValueRW.DayNumber++;
            }
            
            dayCycle.ValueRW.CurrentHour = newHour;
            dayCycle.ValueRW.CurrentPhase = RoutineHelpers.GetPhaseForHour(newHour, config);
        }
    }

    /// <summary>
    /// System that transitions entity routines based on day phase.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DayCycleSystem))]
    [BurstCompile]
    public partial struct DailyRoutineSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<DayCycleState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var dayCycle = SystemAPI.GetSingleton<DayCycleState>();
            uint currentTick = timeState.Tick;

            // Process all entities with routines
            foreach (var (routine, schedule, phaseEvents, entity) in 
                SystemAPI.Query<RefRW<EntityRoutine>, DynamicBuffer<RoutineSchedule>, DynamicBuffer<PhaseChangeEvent>>()
                    .WithEntityAccess())
            {
                var currentPhase = dayCycle.CurrentPhase;
                
                // Check for phase change
                if (routine.ValueRW.CurrentPhase != currentPhase)
                {
                    var oldPhase = routine.ValueRW.CurrentPhase;
                    routine.ValueRW.CurrentPhase = currentPhase;
                    routine.ValueRW.PhaseStartTime = dayCycle.CurrentHour;
                    routine.ValueRW.LastPhaseChangeTick = currentTick;

                    // Get scheduled activity for new phase
                    var scheduledActivity = RoutineHelpers.GetScheduledActivity(schedule, currentPhase);
                    routine.ValueRW.ScheduledActivity = scheduledActivity;

                    // If not interrupted, switch to scheduled activity
                    if (!routine.ValueRW.IsInterrupted)
                    {
                        routine.ValueRW.CurrentActivity = scheduledActivity;
                        routine.ValueRW.ActivityStartTime = dayCycle.CurrentHour;
                    }

                    // Emit phase change event
                    phaseEvents.Add(new PhaseChangeEvent
                    {
                        OldPhase = oldPhase,
                        NewPhase = currentPhase,
                        NewActivity = routine.ValueRW.CurrentActivity,
                        Tick = currentTick
                    });
                }
            }
        }
    }

    /// <summary>
    /// System that processes routine interrupt requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DailyRoutineSystem))]
    [BurstCompile]
    public partial struct RoutineInterruptSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process interrupt requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<RoutineInterruptRequest>>().WithEntityAccess())
            {
                var req = request.ValueRO;
                
                if (!SystemAPI.HasComponent<EntityRoutine>(req.TargetEntity) ||
                    !SystemAPI.HasBuffer<RoutineSchedule>(req.TargetEntity))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var routine = SystemAPI.GetComponent<EntityRoutine>(req.TargetEntity);
                var schedule = SystemAPI.GetBuffer<RoutineSchedule>(req.TargetEntity);

                // Check if interrupt is allowed
                if (RoutineHelpers.CanInterrupt(routine, schedule, req.Priority))
                {
                    // Emit interrupt event
                    if (SystemAPI.HasBuffer<RoutineInterruptEvent>(req.TargetEntity))
                    {
                        var events = SystemAPI.GetBuffer<RoutineInterruptEvent>(req.TargetEntity);
                        events.Add(new RoutineInterruptEvent
                        {
                            InterruptedActivity = routine.CurrentActivity,
                            NewActivity = req.NewActivity,
                            Priority = req.Priority,
                            Tick = currentTick
                        });
                    }

                    // Apply interrupt
                    routine.IsInterrupted = true;
                    routine.InterruptPriority = req.Priority;
                    routine.CurrentActivity = req.NewActivity;
                    
                    SystemAPI.SetComponent(req.TargetEntity, routine);
                }

                ecb.DestroyEntity(entity);
            }
        }
    }

    /// <summary>
    /// System that clears expired interrupts.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RoutineInterruptSystem))]
    [BurstCompile]
    public partial struct RoutineInterruptClearSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<DayCycleState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var dayCycle = SystemAPI.GetSingleton<DayCycleState>();

            // Clear interrupts when phase changes (simple approach)
            // More sophisticated: track interrupt duration
            foreach (var (routine, schedule) in 
                SystemAPI.Query<RefRW<EntityRoutine>, DynamicBuffer<RoutineSchedule>>())
            {
                if (routine.ValueRW.IsInterrupted)
                {
                    // Check if we've entered a new phase since interrupt
                    // (Simple clear - could be enhanced with duration tracking)
                    var scheduledActivity = RoutineHelpers.GetScheduledActivity(schedule, routine.ValueRW.CurrentPhase);
                    
                    // If interrupt activity matches scheduled, clear interrupt flag
                    if (routine.ValueRW.CurrentActivity == scheduledActivity)
                    {
                        routine.ValueRW.IsInterrupted = false;
                        routine.ValueRW.InterruptPriority = 0;
                    }
                }
            }
        }
    }
}


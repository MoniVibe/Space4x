using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// Calculates steering outputs based on active steering behaviors.
    /// Game-agnostic: works for any moving entity.
    /// </summary>
    /// <remarks>SteeringTarget is supplied by AI systems or external scripts; no sensor dependency required.</remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SteeringSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);

            new SteeringJob
            {
                TransformLookup = _transformLookup,
                DeltaTime = timeState.FixedDeltaTime,
                CurrentTick = timeState.Tick
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct SteeringJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public float DeltaTime;
            public uint CurrentTick;

            public void Execute(
                Entity entity,
                ref SteeringOutput output,
                ref SteeringTarget target,
                in SteeringConfig config,
                in LocalTransform transform,
                ref WanderState wanderState)
            {
                var position = transform.Position;
                var forward = math.forward(transform.Rotation);
                var currentVelocity = output.DesiredVelocity; // Use previous as estimate

                // Get target position
                var targetPos = target.TargetPosition;
                var targetVelocity = target.TargetVelocity;
                
                if (target.UseEntity && target.TargetEntity != Entity.Null && 
                    TransformLookup.HasComponent(target.TargetEntity))
                {
                    targetPos = TransformLookup[target.TargetEntity].Position;
                }

                // Calculate steering based on behavior
                float3 steering = float3.zero;
                
                switch (target.Behavior)
                {
                    case SteeringBehavior.Seek:
                        steering = SteeringCalculations.Seek(
                            position, targetPos, currentVelocity, config.MaxSpeed);
                        break;
                        
                    case SteeringBehavior.Flee:
                        steering = SteeringCalculations.Flee(
                            position, targetPos, currentVelocity, config.MaxSpeed);
                        break;
                        
                    case SteeringBehavior.Arrive:
                        steering = SteeringCalculations.Arrive(
                            position, targetPos, currentVelocity, config.MaxSpeed,
                            config.ArriveSlowingRadius, config.ArriveStopRadius);
                        break;
                        
                    case SteeringBehavior.Wander:
                        // Update random seed
                        if (wanderState.LastUpdateTick != CurrentTick)
                        {
                            wanderState.RandomSeed = (uint)(entity.Index * 1000 + CurrentTick);
                            wanderState.LastUpdateTick = CurrentTick;
                        }
                        
                        var wanderDir = SteeringCalculations.Wander(
                            position, forward,
                            ref wanderState.WanderAngle,
                            config.WanderRadius,
                            config.WanderDistance,
                            config.WanderJitter * DeltaTime,
                            wanderState.RandomSeed);
                        steering = wanderDir * config.MaxSpeed - currentVelocity;
                        break;
                        
                    case SteeringBehavior.Pursue:
                        steering = SteeringCalculations.Pursue(
                            position, targetPos, targetVelocity,
                            currentVelocity, config.MaxSpeed);
                        break;
                        
                    case SteeringBehavior.Evade:
                        steering = SteeringCalculations.Evade(
                            position, targetPos, targetVelocity,
                            currentVelocity, config.MaxSpeed);
                        break;
                        
                    case SteeringBehavior.None:
                    default:
                        output.IsValid = false;
                        return;
                }

                // Clamp steering force
                var steeringMag = math.length(steering);
                if (steeringMag > config.MaxAcceleration)
                {
                    steering = (steering / steeringMag) * config.MaxAcceleration;
                }

                // Apply steering to velocity
                var newVelocity = currentVelocity + steering * DeltaTime;
                var speed = math.length(newVelocity);
                if (speed > config.MaxSpeed)
                {
                    newVelocity = (newVelocity / speed) * config.MaxSpeed;
                }

                // Calculate desired rotation
                float3 desiredRotation = float3.zero;
                if (speed > 0.01f)
                {
                    var desiredForward = math.normalize(newVelocity);
                    var currentForward = forward;
                    
                    // Calculate angle difference
                    var angle = math.acos(math.clamp(math.dot(currentForward, desiredForward), -1f, 1f));
                    var cross = math.cross(currentForward, desiredForward);
                    var rotationSign = math.sign(cross.y);
                    
                    desiredRotation.y = rotationSign * math.min(
                        math.degrees(angle), 
                        config.MaxRotationSpeed * DeltaTime);
                }

                output.DesiredVelocity = newVelocity;
                output.DesiredRotation = desiredRotation;
                output.ActiveBehavior = target.Behavior;
                output.Weight = target.Priority;
                output.IsValid = true;
            }
        }
    }

    /// <summary>
    /// System to apply steering output to entity movement.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SteeringSystem))]
    public partial struct SteeringApplicationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = timeState.FixedDeltaTime;

            foreach (var (output, transform) in 
                SystemAPI.Query<RefRO<SteeringOutput>, RefRW<LocalTransform>>())
            {
                if (!output.ValueRO.IsValid)
                {
                    continue;
                }

                // Apply velocity
                var velocity = output.ValueRO.DesiredVelocity;
                transform.ValueRW.Position += velocity * deltaTime;

                // Apply rotation
                var rotationDelta = output.ValueRO.DesiredRotation;
                if (math.lengthsq(rotationDelta) > 0.0001f)
                {
                    var rotationQuat = quaternion.EulerXYZ(math.radians(rotationDelta));
                    transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, rotationQuat);
                }
            }
        }
    }
}


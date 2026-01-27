using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Movement;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Movement
{
    /// <summary>
    /// Integrates movement kinematics: samples spec curves + PilotProficiency multipliers,
    /// clamps by Caps bitset and JerkClamp, applies terrain constraints for Dim==2.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CommandComposeSystem))]
    public partial struct MovementIntegrateSystem : ISystem
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
            var deltaTime = timeState.DeltaTime;

            var job = new MovementIntegrateJob
            {
                DeltaTime = deltaTime
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct MovementIntegrateJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(
                Entity entity,
                ref MovementState movementState,
                ref LocalTransform transform,
                in MovementModelRef modelRef,
                in PilotProficiency proficiency)
            {
                if (!modelRef.Blob.IsCreated)
                {
                    return;
                }

                ref var spec = ref modelRef.Blob.Value;

                // Get desired direction and current velocity
                float3 desired = movementState.Desired;
                float3 currentVel = movementState.Vel;

                // Project desired onto allowed axes by caps
                float3 forward = transform.Forward();
                float3 right = transform.Right();
                float3 up = transform.Up();

                bool canStrafe = (spec.Caps & MovementCaps.Strafe) != 0;
                bool canVertical = (spec.Caps & MovementCaps.Vertical) != 0;
                bool canReverse = (spec.Caps & MovementCaps.Reverse) != 0;

                // Project desired velocity onto allowed axes
                float3 vCmd = float3.zero;

                // Forward/backward component
                float forwardDot = math.dot(desired, forward);
                if (forwardDot >= 0f || canReverse)
                {
                    vCmd += forward * forwardDot;
                }
                else
                {
                    // Can't reverse, clamp to zero
                    vCmd += forward * math.max(0f, forwardDot);
                }

                // Strafe component
                if (canStrafe)
                {
                    float rightDot = math.dot(desired, right);
                    vCmd += right * rightDot;
                }

                // Vertical component
                if (canVertical && spec.Dim == 3)
                {
                    float upDot = math.dot(desired, up);
                    vCmd += up * upDot;
                }
                else if (spec.Dim == 2)
                {
                    // 2D movement: zero vertical component
                    vCmd.y = 0f;
                }

                // Normalize command vector
                float cmdLength = math.length(vCmd);
                if (cmdLength > 1e-6f)
                {
                    vCmd = math.normalize(vCmd);
                }
                else
                {
                    vCmd = forward; // Default to forward
                }

                // Sample acceleration curve (simplified: use throttle = 1.0 for now)
                float throttle = 1.0f;
                float accelForward = SampleCurve(ref spec.AccelForward, throttle) * proficiency.ControlMult;
                float accelStrafe = canStrafe ? SampleCurve(ref spec.AccelStrafe, throttle) * proficiency.ControlMult : 0f;
                float accelVertical = canVertical ? SampleCurve(ref spec.AccelVertical, throttle) * proficiency.ControlMult : 0f;

                // Compute desired velocity magnitude
                float maxSpeed = SampleCurve(ref spec.MaxSpeed, throttle);
                float3 desiredVel = vCmd * maxSpeed;

                // Compute acceleration delta
                float3 dv = desiredVel - currentVel;
                float dvLength = math.length(dv);

                // Clamp by jerk limit
                float maxJerk = spec.JerkClamp * DeltaTime;
                if (dvLength > maxJerk)
                {
                    dv = math.normalize(dv) * maxJerk;
                }

                // Apply acceleration
                float3 newVel = currentVel + dv;

                // Clamp by max speed
                float newVelLength = math.length(newVel);
                if (newVelLength > maxSpeed)
                {
                    newVel = math.normalize(newVel) * maxSpeed;
                }

                // Apply terrain constraints for 2D movement
                if (spec.Dim == 2)
                {
                    // Zero vertical velocity for 2D movement
                    newVel.y = 0f;

                    // TODO: Apply slope clamping and ground friction
                    // This would require terrain height/slope queries
                }

                // Update movement state
                movementState.Vel = newVel;

                // Update transform position (or let physics system handle it)
                transform.Position += newVel * DeltaTime;
            }

            private static float SampleCurve(ref Curve1D curve, float t01)
            {
                if (curve.Knots.Length == 0)
                {
                    return 1f; // Default value
                }

                // Clamp t to [0, 1]
                t01 = math.clamp(t01, 0f, 1f);

                // Linear interpolation between knots
                float indexFloat = t01 * (curve.Knots.Length - 1);
                int index = (int)math.floor(indexFloat);
                int nextIndex = math.min(index + 1, curve.Knots.Length - 1);
                float fraction = indexFloat - index;

                float value = math.lerp(curve.Knots[index], curve.Knots[nextIndex], fraction);
                return value;
            }
        }
    }
}


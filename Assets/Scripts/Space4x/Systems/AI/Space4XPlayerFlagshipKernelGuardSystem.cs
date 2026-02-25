using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Enforces pure movement kernel ownership for player flagship transforms.
    /// Any external write after the kernel pose stamp is treated as a violation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct Space4XPlayerFlagshipKernelGuardSystem : ISystem
    {
        private const float PositionTolerance = 0.001f;
        private const float RotationToleranceDegrees = 0.25f;

        private uint _lastViolationTick;
        private Entity _lastViolationEntity;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PlayerFlagshipKernelPoseStamp>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var stamp = SystemAPI.GetSingleton<PlayerFlagshipKernelPoseStamp>();
            if (stamp.Active == 0)
            {
                return;
            }

            var tick = timeState.Tick;
            foreach (var (transformRef, inputRef, entity) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRO<PlayerFlagshipFlightInput>>()
                         .WithAll<PlayerFlagshipTag>()
                         .WithEntityAccess())
            {
                var input = inputRef.ValueRO;
                if (entity != stamp.FlagshipEntity ||
                    input.PureKernelMode == 0 ||
                    input.MovementEnabled == 0 ||
                    stamp.Tick != tick)
                {
                    continue;
                }

                var current = transformRef.ValueRO;
                var positionDelta = math.length(current.Position - stamp.Position);

                var currentRotation = math.normalize(current.Rotation);
                var stampedRotation = math.normalize(stamp.Rotation);
                var rotationDot = math.abs(math.dot(currentRotation.value, stampedRotation.value));
                rotationDot = math.clamp(rotationDot, -1f, 1f);
                var rotationAngleDegrees = math.degrees(math.acos(rotationDot) * 2f);

                if (positionDelta <= PositionTolerance && rotationAngleDegrees <= RotationToleranceDegrees)
                {
                    continue;
                }

                // Enforce kernel ownership by restoring stamped pose.
                current.Position = stamp.Position;
                current.Rotation = stamp.Rotation;
                transformRef.ValueRW = current;

                if (_lastViolationTick != tick || _lastViolationEntity != entity)
                {
                    _lastViolationTick = tick;
                    _lastViolationEntity = entity;
                    UnityEngine.Debug.LogError(
                        $"[Space4XMovementKernelGuard] Unauthorized flagship transform write detected and reverted. tick={tick} entity={entity} posDelta={positionDelta:F4} rotDeltaDeg={rotationAngleDegrees:F3}");
                }
            }
        }
    }
}

using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Data contract describing 6DOF player flight limits and response tuning.
    /// </summary>
    [Serializable]
    public struct ShipFlightProfile : IComponentData
    {
        public float MaxForwardSpeed;
        public float MaxReverseSpeed;
        public float MaxStrafeSpeed;
        public float MaxVerticalSpeed;
        public float ForwardAcceleration;
        public float ReverseAcceleration;
        public float StrafeAcceleration;
        public float VerticalAcceleration;
        public float BoostMultiplier;
        public float PassiveDriftDrag;
        public float DampenerDeceleration;
        public float RetroBrakeAcceleration;
        public float RollSpeedDegrees;
        public float CursorTurnSharpness;
        public float MaxAngularSpeedDegrees;
        public float AngularAccelerationDegrees;
        public float AngularDampingDegrees;
        public float AngularDeadbandDegrees;
        public float MaxCursorLeadDegrees;
        public float TurnAuthorityAtMaxSpeed;
        public float AngularOvershootRatio;
        public float MaxCursorPitchDegrees;
        public byte DefaultInertialDampenersEnabled;

        public bool IsConfigured =>
            MaxForwardSpeed > 0.01f &&
            ForwardAcceleration > 0.01f &&
            ReverseAcceleration > 0.01f &&
            StrafeAcceleration > 0.01f &&
            VerticalAcceleration > 0.01f;

        public ShipFlightProfile Sanitized()
        {
            var turnNorm = math.saturate(CursorTurnSharpness / 12f);
            var resolvedMaxAngularSpeedDegrees = MaxAngularSpeedDegrees > 0.01f
                ? math.max(1f, MaxAngularSpeedDegrees)
                : math.lerp(8f, 24f, turnNorm);
            var resolvedAngularAccelerationDegrees = AngularAccelerationDegrees > 0.01f
                ? math.max(1f, AngularAccelerationDegrees)
                : math.lerp(24f, 90f, turnNorm);
            var resolvedAngularDampingDegrees = AngularDampingDegrees > 0.01f
                ? math.max(1f, AngularDampingDegrees)
                : resolvedAngularAccelerationDegrees;
            var resolvedAngularDeadbandDegrees = AngularDeadbandDegrees > 0.01f
                ? math.clamp(AngularDeadbandDegrees, 0f, 8f)
                : 0.6f;
            var resolvedMaxCursorLeadDegrees = MaxCursorLeadDegrees > 0.01f
                ? math.clamp(MaxCursorLeadDegrees, 1f, 179f)
                : math.lerp(28f, 170f, turnNorm);
            var resolvedTurnAuthorityAtMaxSpeed = TurnAuthorityAtMaxSpeed > 0.01f
                ? math.clamp(TurnAuthorityAtMaxSpeed, 0.05f, 1f)
                : 0.45f;
            var resolvedAngularOvershootRatio = AngularOvershootRatio > 0.0001f
                ? math.clamp(AngularOvershootRatio, 0f, 0.75f)
                : math.lerp(0.22f, 0.08f, turnNorm);
            return new ShipFlightProfile
            {
                MaxForwardSpeed = math.max(0.1f, MaxForwardSpeed),
                MaxReverseSpeed = math.max(0.1f, MaxReverseSpeed),
                MaxStrafeSpeed = math.max(0.1f, MaxStrafeSpeed),
                MaxVerticalSpeed = math.max(0.1f, MaxVerticalSpeed),
                ForwardAcceleration = math.max(0.1f, ForwardAcceleration),
                ReverseAcceleration = math.max(0.1f, ReverseAcceleration),
                StrafeAcceleration = math.max(0.1f, StrafeAcceleration),
                VerticalAcceleration = math.max(0.1f, VerticalAcceleration),
                BoostMultiplier = math.max(1f, BoostMultiplier),
                PassiveDriftDrag = math.max(0f, PassiveDriftDrag),
                DampenerDeceleration = math.max(0.1f, DampenerDeceleration),
                RetroBrakeAcceleration = math.max(0.1f, RetroBrakeAcceleration),
                RollSpeedDegrees = math.max(1f, RollSpeedDegrees),
                CursorTurnSharpness = math.max(0.1f, CursorTurnSharpness),
                MaxAngularSpeedDegrees = resolvedMaxAngularSpeedDegrees,
                AngularAccelerationDegrees = resolvedAngularAccelerationDegrees,
                AngularDampingDegrees = resolvedAngularDampingDegrees,
                AngularDeadbandDegrees = resolvedAngularDeadbandDegrees,
                MaxCursorLeadDegrees = resolvedMaxCursorLeadDegrees,
                TurnAuthorityAtMaxSpeed = resolvedTurnAuthorityAtMaxSpeed,
                AngularOvershootRatio = resolvedAngularOvershootRatio,
                MaxCursorPitchDegrees = math.clamp(MaxCursorPitchDegrees, 1f, 89f),
                DefaultInertialDampenersEnabled = DefaultInertialDampenersEnabled != 0 ? (byte)1 : (byte)0
            };
        }

        public static ShipFlightProfile CreateDefault(string presetId)
        {
            if (!string.IsNullOrWhiteSpace(presetId))
            {
                if (presetId.Contains("capsule", StringComparison.OrdinalIgnoreCase) ||
                    presetId.Contains("interceptor", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateInterceptorDefault();
                }

                if (presetId.Contains("sphere", StringComparison.OrdinalIgnoreCase) ||
                    presetId.Contains("frigate", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateFrigateDefault();
                }
            }

            return CreateCarrierDefault();
        }

        private static ShipFlightProfile CreateCarrierDefault()
        {
            return new ShipFlightProfile
            {
                MaxForwardSpeed = 120f,
                MaxReverseSpeed = 68f,
                MaxStrafeSpeed = 56f,
                MaxVerticalSpeed = 48f,
                ForwardAcceleration = 96f,
                ReverseAcceleration = 82f,
                StrafeAcceleration = 72f,
                VerticalAcceleration = 62f,
                BoostMultiplier = 1.6f,
                PassiveDriftDrag = 0.02f,
                DampenerDeceleration = 46f,
                RetroBrakeAcceleration = 84f,
                RollSpeedDegrees = 62f,
                CursorTurnSharpness = 8.5f,
                MaxAngularSpeedDegrees = 22f,
                AngularAccelerationDegrees = 52f,
                AngularDampingDegrees = 64f,
                AngularDeadbandDegrees = 0.8f,
                MaxCursorLeadDegrees = 120f,
                TurnAuthorityAtMaxSpeed = 0.42f,
                AngularOvershootRatio = 0.25f,
                MaxCursorPitchDegrees = 62f,
                DefaultInertialDampenersEnabled = 0
            };
        }

        private static ShipFlightProfile CreateFrigateDefault()
        {
            return new ShipFlightProfile
            {
                MaxForwardSpeed = 160f,
                MaxReverseSpeed = 92f,
                MaxStrafeSpeed = 86f,
                MaxVerticalSpeed = 72f,
                ForwardAcceleration = 132f,
                ReverseAcceleration = 118f,
                StrafeAcceleration = 102f,
                VerticalAcceleration = 88f,
                BoostMultiplier = 1.85f,
                PassiveDriftDrag = 0.018f,
                DampenerDeceleration = 58f,
                RetroBrakeAcceleration = 102f,
                RollSpeedDegrees = 84f,
                CursorTurnSharpness = 10.5f,
                MaxAngularSpeedDegrees = 28f,
                AngularAccelerationDegrees = 76f,
                AngularDampingDegrees = 92f,
                AngularDeadbandDegrees = 0.65f,
                MaxCursorLeadDegrees = 140f,
                TurnAuthorityAtMaxSpeed = 0.46f,
                AngularOvershootRatio = 0.2f,
                MaxCursorPitchDegrees = 66f,
                DefaultInertialDampenersEnabled = 0
            };
        }

        private static ShipFlightProfile CreateInterceptorDefault()
        {
            return new ShipFlightProfile
            {
                MaxForwardSpeed = 210f,
                MaxReverseSpeed = 126f,
                MaxStrafeSpeed = 124f,
                MaxVerticalSpeed = 108f,
                ForwardAcceleration = 190f,
                ReverseAcceleration = 170f,
                StrafeAcceleration = 154f,
                VerticalAcceleration = 142f,
                BoostMultiplier = 2.05f,
                PassiveDriftDrag = 0.014f,
                DampenerDeceleration = 64f,
                RetroBrakeAcceleration = 118f,
                RollSpeedDegrees = 122f,
                CursorTurnSharpness = 13.5f,
                MaxAngularSpeedDegrees = 38f,
                AngularAccelerationDegrees = 124f,
                AngularDampingDegrees = 140f,
                AngularDeadbandDegrees = 0.45f,
                MaxCursorLeadDegrees = 168f,
                TurnAuthorityAtMaxSpeed = 0.55f,
                AngularOvershootRatio = 0.12f,
                MaxCursorPitchDegrees = 72f,
                DefaultInertialDampenersEnabled = 0
            };
        }
    }

    /// <summary>
    /// Runtime player flight state that should persist on the flagship entity.
    /// </summary>
    public struct ShipFlightRuntimeState : IComponentData
    {
        public float3 VelocityWorld;
        public byte InertialDampenersEnabled;
        public float AngularSpeedRadians;
        public float ForwardThrottle;
        public float StrafeThrottle;
        public float VerticalThrottle;
    }
}

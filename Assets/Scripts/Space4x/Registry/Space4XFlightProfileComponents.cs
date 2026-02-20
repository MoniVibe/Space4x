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

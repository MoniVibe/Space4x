using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.InputKernel;

namespace Space4X.Registry
{
    /// <summary>
    /// Marks the entity currently controlled as the player flagship in FleetCrawl runtime.
    /// </summary>
    public struct PlayerFlagshipTag : IComponentData
    {
    }

    /// <summary>
    /// Per-tick player flight intent captured by UI/input and consumed by fixed-step ECS movement.
    /// </summary>
    public struct PlayerFlagshipFlightInput : IComponentData
    {
        public float Forward;
        public float Strafe;
        public float Vertical;
        public float Roll;
        public float3 TranslationForward;
        public float3 TranslationUp;
        public float3 CursorLookDirection;
        public float3 CursorUpDirection;
        public byte TranslationBasisOverride;
        public byte AutoAlignToTranslation;
        public byte CursorSteeringActive;
        public byte FighterSteeringMode;
        public byte BoostPressed;
        public byte RetroBrakePressed;
        public byte ToggleDampenersRequested;
        public byte MovementEnabled;
        public byte PureKernelMode;

        public static PlayerFlagshipFlightInput Disabled => new PlayerFlagshipFlightInput
        {
            Forward = 0f,
            Strafe = 0f,
            Vertical = 0f,
            Roll = 0f,
            TranslationForward = new float3(0f, 0f, 1f),
            TranslationUp = new float3(0f, 1f, 0f),
            CursorLookDirection = new float3(0f, 0f, 1f),
            CursorUpDirection = new float3(0f, 1f, 0f),
            TranslationBasisOverride = 0,
            AutoAlignToTranslation = 0,
            CursorSteeringActive = 0,
            FighterSteeringMode = 0,
            BoostPressed = 0,
            RetroBrakePressed = 0,
            ToggleDampenersRequested = 0,
            MovementEnabled = 0,
            PureKernelMode = 0
        };

        public InputKernelLocomotionIntent ToKernelLocomotionIntent(uint sampleTick)
        {
            return new InputKernelLocomotionIntent
            {
                Forward = Forward,
                Strafe = Strafe,
                Vertical = Vertical,
                Roll = Roll,
                TranslationForward = TranslationForward,
                TranslationUp = TranslationUp,
                CursorLookDirection = CursorLookDirection,
                CursorUpDirection = CursorUpDirection,
                TranslationBasisOverride = TranslationBasisOverride,
                AutoAlignToTranslation = AutoAlignToTranslation,
                CursorSteeringActive = CursorSteeringActive,
                SteeringMode = FighterSteeringMode != 0 ? (byte)1 : (byte)0,
                BoostPressed = BoostPressed,
                RetroBrakePressed = RetroBrakePressed,
                ToggleAuxiliaryAction = ToggleDampenersRequested,
                MovementEnabled = MovementEnabled,
                KernelModeRequested = PureKernelMode,
                SampleTick = sampleTick
            };
        }

        public static PlayerFlagshipFlightInput FromKernelLocomotionIntent(in InputKernelLocomotionIntent intent)
        {
            return new PlayerFlagshipFlightInput
            {
                Forward = intent.Forward,
                Strafe = intent.Strafe,
                Vertical = intent.Vertical,
                Roll = intent.Roll,
                TranslationForward = intent.TranslationForward,
                TranslationUp = intent.TranslationUp,
                CursorLookDirection = intent.CursorLookDirection,
                CursorUpDirection = intent.CursorUpDirection,
                TranslationBasisOverride = intent.TranslationBasisOverride,
                AutoAlignToTranslation = intent.AutoAlignToTranslation,
                CursorSteeringActive = intent.CursorSteeringActive,
                FighterSteeringMode = intent.SteeringMode != 0 ? (byte)1 : (byte)0,
                BoostPressed = intent.BoostPressed,
                RetroBrakePressed = intent.RetroBrakePressed,
                ToggleDampenersRequested = intent.ToggleAuxiliaryAction,
                MovementEnabled = intent.MovementEnabled,
                PureKernelMode = intent.KernelModeRequested
            };
        }
    }
}

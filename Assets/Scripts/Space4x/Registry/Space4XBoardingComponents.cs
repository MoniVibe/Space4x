using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Boarding target envelope.
    /// </summary>
    public enum Space4XBoardingTargetKind : byte
    {
        Ship = 0,
        Station = 1,
        Colony = 2
    }

    /// <summary>
    /// Runtime phase for a boarding action.
    /// </summary>
    public enum Space4XBoardingPhase : byte
    {
        None = 0,
        WaitingForWindow = 1,
        Launching = 2,
        Breaching = 3,
        Fighting = 4,
        Captured = 5,
        Repelled = 6,
        Aborted = 7
    }

    /// <summary>
    /// Final boarding result.
    /// </summary>
    public enum Space4XBoardingOutcome : byte
    {
        None = 0,
        Captured = 1,
        Repelled = 2,
        Aborted = 3
    }

    /// <summary>
    /// Individual role used by boarding formations and equipment policy.
    /// </summary>
    public enum Space4XBoardingRole : byte
    {
        Assault = 0,
        Breacher = 1,
        Heavy = 2,
        Marksman = 3,
        Medic = 4,
        Engineer = 5,
        Commander = 6
    }

    /// <summary>
    /// Aggregate ship-side boarding personnel profile.
    /// This controls available boarders when no explicit manifest is used.
    /// </summary>
    public struct Space4XBoardingDeploymentProfile : IComponentData
    {
        public int AvailableBoarders;
        public int ReserveBoarders;
        public int MaxDeployPerAction;
        public float AverageTraining01;
        public float AverageArmor01;
        public float AverageWeapon01;

        public static Space4XBoardingDeploymentProfile Starter => new Space4XBoardingDeploymentProfile
        {
            AvailableBoarders = 30,
            ReserveBoarders = 0,
            MaxDeployPerAction = 30,
            AverageTraining01 = 0.45f,
            AverageArmor01 = 0.4f,
            AverageWeapon01 = 0.4f
        };
    }

    /// <summary>
    /// Explicit manifest entry for boarders represented as real individual entities.
    /// Add this buffer to ships/stations for individual-driven boarding resolution.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct Space4XBoardingManifestEntry : IBufferElementData
    {
        public Entity Individual;
        public Space4XBoardingRole Role;
        public half Readiness01;
        public half ArmorTier01;
        public half WeaponTier01;
        public byte Active;
    }

    /// <summary>
    /// Ship-side boarding profile (marines, security, launch capability).
    /// </summary>
    public struct Space4XBoardingProfile : IComponentData
    {
        /// <summary>
        /// Strength multiplier for outgoing marines.
        /// </summary>
        public float AssaultStrength;

        /// <summary>
        /// Strength multiplier for internal defenders.
        /// </summary>
        public float DefenseStrength;

        /// <summary>
        /// Additional security saturation inside the ship.
        /// </summary>
        public float InternalSecurity;

        /// <summary>
        /// Mitigates boarding casualties [0..1].
        /// </summary>
        public float CasualtyMitigation01;

        public static Space4XBoardingProfile Default => new Space4XBoardingProfile
        {
            AssaultStrength = 1f,
            DefenseStrength = 1f,
            InternalSecurity = 1f,
            CasualtyMitigation01 = 0.1f
        };
    }

    /// <summary>
    /// Boarding order and runtime state.
    /// Attach to the attacker entity.
    /// </summary>
    public struct Space4XBoardingOrder : IComponentData
    {
        public Entity Target;
        public Space4XBoardingTargetKind TargetKind;
        public uint IssuedTick;
        public uint StartedTick;
        public uint LastResolveTick;
        public uint CompletedTick;
        public uint MaxDurationTicks;
        public ushort RequestedBoarderCount;
        public ushort CommittedBoarderCount;
        public ushort DefenderEstimatedBoarderCount;
        public float DesiredRangeMeters;
        public float TroopCommitment01;
        public float PodPenetration;
        public float ElectronicWarfareSupport;
        public float AssaultProgress01;
        public float AttackerForce;
        public float DefenderForce;
        public float AttackerLosses;
        public float DefenderLosses;
        public float LastKnownDistanceMeters;
        public Space4XBoardingPhase Phase;
        public Space4XBoardingOutcome Outcome;
        public byte AutoClearOnResolve;

        public static Space4XBoardingOrder Create(Entity target, uint issuedTick = 0u)
        {
            return new Space4XBoardingOrder
            {
                Target = target,
                TargetKind = Space4XBoardingTargetKind.Ship,
                IssuedTick = issuedTick,
                StartedTick = 0u,
                LastResolveTick = 0u,
                CompletedTick = 0u,
                MaxDurationTicks = 120u,
                RequestedBoarderCount = 0,
                CommittedBoarderCount = 0,
                DefenderEstimatedBoarderCount = 0,
                DesiredRangeMeters = 550f,
                TroopCommitment01 = 1f,
                PodPenetration = 0f,
                ElectronicWarfareSupport = 0f,
                AssaultProgress01 = 0f,
                AttackerForce = 0f,
                DefenderForce = 0f,
                AttackerLosses = 0f,
                DefenderLosses = 0f,
                LastKnownDistanceMeters = 0f,
                Phase = Space4XBoardingPhase.None,
                Outcome = Space4XBoardingOutcome.None,
                AutoClearOnResolve = 0
            };
        }
    }

    /// <summary>
    /// Capture marker on a boarded ship.
    /// </summary>
    public struct Space4XBoardingCaptureState : IComponentData
    {
        public Entity Captor;
        public uint CapturedTick;
        public float HullRatioAtCapture;
        public float RemainingDefenderForce;
    }

    /// <summary>
    /// Global boarding tuning.
    /// </summary>
    public struct Space4XBoardingTuning : IComponentData
    {
        public float MaxBoardingRangeMeters;
        public float MaxShieldRatioToStart;
        public float MaxHullRatioToStart;
        public float MaxShieldRatioToCapture;
        public float MaxHullRatioToCapture;
        public float BaseProgressPerTick;
        public float AdvantageProgressScale;
        public float BaseCasualtyPerTick;
        public float FocusBonusScale;
        public float ShieldDefenseScale;
        public float CriticalHullDefenseFloor;
        public float CaptureThreshold;
        public int StarterMinBoarders;
        public int StarterMaxBoarders;
        public int HardMaxBoardersPerAction;
        public float BoarderCountExponent;
        public float BoarderForceScale;
        public float BoarderQualityScale;
        public uint MinResolveIntervalTicks;
        public uint DefaultMaxDurationTicks;
        public uint PostCaptureSubsystemDisableTicks;

        public static Space4XBoardingTuning Default => new Space4XBoardingTuning
        {
            MaxBoardingRangeMeters = 600f,
            MaxShieldRatioToStart = 0.15f,
            MaxHullRatioToStart = 0.65f,
            MaxShieldRatioToCapture = 0.2f,
            MaxHullRatioToCapture = 0.7f,
            BaseProgressPerTick = 0.06f,
            AdvantageProgressScale = 0.07f,
            BaseCasualtyPerTick = 0.035f,
            FocusBonusScale = 0.75f,
            ShieldDefenseScale = 0.6f,
            CriticalHullDefenseFloor = 0.45f,
            CaptureThreshold = 1f,
            StarterMinBoarders = 5,
            StarterMaxBoarders = 30,
            HardMaxBoardersPerAction = 1000,
            BoarderCountExponent = 0.85f,
            BoarderForceScale = 0.12f,
            BoarderQualityScale = 0.75f,
            MinResolveIntervalTicks = 1u,
            DefaultMaxDurationTicks = 120u,
            PostCaptureSubsystemDisableTicks = 45u
        };
    }
}

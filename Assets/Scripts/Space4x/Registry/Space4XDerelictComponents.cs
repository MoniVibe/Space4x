using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Cause of dereliction.
    /// </summary>
    public enum DerelictCause : byte
    {
        Unknown = 0,
        Combat = 1,
        SystemFailure = 2,
        CrewDeath = 3,
        Mutiny = 4,
        Abandoned = 5,
        Ancient = 6,
        Sabotage = 7
    }

    /// <summary>
    /// Condition of the derelict hull.
    /// </summary>
    public enum DerelictCondition : byte
    {
        /// <summary>
        /// Pristine - easily salvageable or reactivatable.
        /// </summary>
        Pristine = 0,

        /// <summary>
        /// Damaged but functional - partial salvage possible.
        /// </summary>
        Damaged = 1,

        /// <summary>
        /// Heavily damaged - limited salvage.
        /// </summary>
        Ruined = 2,

        /// <summary>
        /// Stripped - only scrap value remains.
        /// </summary>
        Stripped = 3,

        /// <summary>
        /// Decaying - hazardous to approach.
        /// </summary>
        Decaying = 4
    }

    /// <summary>
    /// Hazards present on a derelict.
    /// </summary>
    [System.Flags]
    public enum DerelictHazard : byte
    {
        None = 0,
        Radiation = 1 << 0,
        SpaceFauna = 1 << 1,
        Unstable = 1 << 2,
        TrappedCrew = 1 << 3,
        Boobytrapped = 1 << 4,
        ContaminationBio = 1 << 5,
        ContaminationChem = 1 << 6,
        HostileAI = 1 << 7
    }

    /// <summary>
    /// State of a derelict vessel or station.
    /// </summary>
    public struct DerelictState : IComponentData
    {
        /// <summary>
        /// Cause of dereliction.
        /// </summary>
        public DerelictCause Cause;

        /// <summary>
        /// Current condition.
        /// </summary>
        public DerelictCondition Condition;

        /// <summary>
        /// Active hazards.
        /// </summary>
        public DerelictHazard Hazards;

        /// <summary>
        /// Remaining hull integrity [0, 1].
        /// </summary>
        public half HullRemaining;

        /// <summary>
        /// How much has been scanned [0, 1].
        /// </summary>
        public half ScanProgress;

        /// <summary>
        /// Tick when dereliction occurred.
        /// </summary>
        public uint DerelictionTick;

        /// <summary>
        /// Original owner faction ID.
        /// </summary>
        public ushort OriginalFaction;

        /// <summary>
        /// Original ship class/type.
        /// </summary>
        public ushort OriginalClass;

        /// <summary>
        /// Whether derelict has been claimed.
        /// </summary>
        public byte IsClaimed;

        /// <summary>
        /// Entity that claimed this derelict.
        /// </summary>
        public Entity ClaimedBy;

        /// <summary>
        /// Whether reverse engineering evidence has been extracted.
        /// </summary>
        public byte EvidenceExtracted;

        public static DerelictState FromCombat(uint tick, ushort faction, ushort shipClass)
        {
            return new DerelictState
            {
                Cause = DerelictCause.Combat,
                Condition = DerelictCondition.Damaged,
                Hazards = DerelictHazard.Radiation | DerelictHazard.Unstable,
                HullRemaining = (half)0.3f,
                ScanProgress = (half)0f,
                DerelictionTick = tick,
                OriginalFaction = faction,
                OriginalClass = shipClass,
                IsClaimed = 0,
                ClaimedBy = Entity.Null,
                EvidenceExtracted = 0
            };
        }

        public static DerelictState Ancient(uint tick)
        {
            return new DerelictState
            {
                Cause = DerelictCause.Ancient,
                Condition = DerelictCondition.Ruined,
                Hazards = DerelictHazard.SpaceFauna | DerelictHazard.HostileAI,
                HullRemaining = (half)0.5f,
                ScanProgress = (half)0f,
                DerelictionTick = tick,
                OriginalFaction = 0,
                OriginalClass = 0,
                IsClaimed = 0,
                ClaimedBy = Entity.Null,
                EvidenceExtracted = 0
            };
        }
    }

    /// <summary>
    /// Expected yield from salvaging this derelict.
    /// </summary>
    public struct SalvageYield : IComponentData
    {
        /// <summary>
        /// Scrap metal value.
        /// </summary>
        public float ScrapMetal;

        /// <summary>
        /// Recoverable fuel.
        /// </summary>
        public float Fuel;

        /// <summary>
        /// Recoverable ammunition.
        /// </summary>
        public float Ammunition;

        /// <summary>
        /// Recoverable provisions.
        /// </summary>
        public float Provisions;

        /// <summary>
        /// Technology samples (0-10 scale).
        /// </summary>
        public byte TechSamples;

        /// <summary>
        /// Rare artifacts (0-5 scale).
        /// </summary>
        public byte Artifacts;

        /// <summary>
        /// Whether ship is reactivatable.
        /// </summary>
        public byte CanReactivate;

        /// <summary>
        /// Estimated value in credits.
        /// </summary>
        public uint EstimatedValue;

        public static SalvageYield FromCondition(DerelictCondition condition, ushort shipClass)
        {
            float baseMetal = shipClass * 100f;
            float modifier = condition switch
            {
                DerelictCondition.Pristine => 1f,
                DerelictCondition.Damaged => 0.7f,
                DerelictCondition.Ruined => 0.4f,
                DerelictCondition.Stripped => 0.1f,
                DerelictCondition.Decaying => 0.05f,
                _ => 0.5f
            };

            return new SalvageYield
            {
                ScrapMetal = baseMetal * modifier,
                Fuel = baseMetal * 0.2f * modifier,
                Ammunition = baseMetal * 0.1f * modifier,
                Provisions = baseMetal * 0.05f * modifier,
                TechSamples = (byte)(condition <= DerelictCondition.Damaged ? 2 : 0),
                Artifacts = 0,
                CanReactivate = (byte)(condition == DerelictCondition.Pristine ? 1 : 0),
                EstimatedValue = (uint)(baseMetal * modifier * 10f)
            };
        }
    }

    /// <summary>
    /// Phase of a salvage operation.
    /// </summary>
    public enum SalvagePhase : byte
    {
        None = 0,
        Approaching = 1,
        Scanning = 2,
        HazardAssessment = 3,
        HazardMitigation = 4,
        Extraction = 5,
        Reactivation = 6,
        Complete = 7,
        Aborted = 8
    }

    /// <summary>
    /// Active salvage operation.
    /// </summary>
    public struct SalvageOperation : IComponentData
    {
        /// <summary>
        /// Target derelict entity.
        /// </summary>
        public Entity Target;

        /// <summary>
        /// Entity performing salvage.
        /// </summary>
        public Entity Salvager;

        /// <summary>
        /// Current phase.
        /// </summary>
        public SalvagePhase Phase;

        /// <summary>
        /// Progress in current phase [0, 1].
        /// </summary>
        public half Progress;

        /// <summary>
        /// Risk level of operation [0, 1].
        /// </summary>
        public half RiskLevel;

        /// <summary>
        /// Speed modifier based on crew skill.
        /// </summary>
        public half SpeedModifier;

        /// <summary>
        /// Resources extracted so far.
        /// </summary>
        public float ExtractedMetal;
        public float ExtractedFuel;
        public float ExtractedAmmo;

        /// <summary>
        /// Whether reactivation is being attempted.
        /// </summary>
        public byte AttemptingReactivation;

        /// <summary>
        /// Tick when operation started.
        /// </summary>
        public uint StartTick;

        public static SalvageOperation Begin(Entity target, Entity salvager, uint tick)
        {
            return new SalvageOperation
            {
                Target = target,
                Salvager = salvager,
                Phase = SalvagePhase.Approaching,
                Progress = (half)0f,
                RiskLevel = (half)0.5f,
                SpeedModifier = (half)1f,
                ExtractedMetal = 0f,
                ExtractedFuel = 0f,
                ExtractedAmmo = 0f,
                AttemptingReactivation = 0,
                StartTick = tick
            };
        }
    }

    /// <summary>
    /// Log of salvage operation events.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct SalvageEvent : IBufferElementData
    {
        /// <summary>
        /// Type of event.
        /// </summary>
        public SalvageEventType Type;

        /// <summary>
        /// Tick when event occurred.
        /// </summary>
        public uint Tick;

        /// <summary>
        /// Outcome (0 = neutral, 1 = positive, 2 = negative).
        /// </summary>
        public byte Outcome;

        /// <summary>
        /// Associated value (damage taken, resources found, etc).
        /// </summary>
        public float Value;
    }

    /// <summary>
    /// Types of salvage events.
    /// </summary>
    public enum SalvageEventType : byte
    {
        Started = 0,
        ScanComplete = 1,
        HazardDetected = 2,
        HazardMitigated = 3,
        HazardTriggered = 4,
        ResourcesFound = 5,
        TechDiscovered = 6,
        ArtifactFound = 7,
        CrewRescued = 8,
        AmbushTriggered = 9,
        ReactivationSuccess = 10,
        ReactivationFailed = 11,
        OperationComplete = 12,
        OperationAborted = 13
    }

    /// <summary>
    /// Tag for entities with salvage capability.
    /// </summary>
    public struct SalvageCapable : IComponentData
    {
        /// <summary>
        /// Salvage speed modifier.
        /// </summary>
        public half SpeedBonus;

        /// <summary>
        /// Risk reduction modifier.
        /// </summary>
        public half RiskReduction;

        /// <summary>
        /// Yield bonus modifier.
        /// </summary>
        public half YieldBonus;

        /// <summary>
        /// Whether can attempt reactivation.
        /// </summary>
        public byte CanReactivate;

        public static SalvageCapable Default => new SalvageCapable
        {
            SpeedBonus = (half)0f,
            RiskReduction = (half)0f,
            YieldBonus = (half)0f,
            CanReactivate = 0
        };

        public static SalvageCapable SalvageVessel => new SalvageCapable
        {
            SpeedBonus = (half)0.5f,
            RiskReduction = (half)0.3f,
            YieldBonus = (half)0.2f,
            CanReactivate = 1
        };
    }

    /// <summary>
    /// Tag for derelict entities.
    /// </summary>
    public struct DerelictTag : IComponentData { }

    /// <summary>
    /// Utility functions for salvage and derelict operations.
    /// </summary>
    public static class DerelictUtility
    {
        /// <summary>
        /// Calculates hazard risk based on active hazards.
        /// </summary>
        public static float CalculateHazardRisk(DerelictHazard hazards)
        {
            float risk = 0f;

            if ((hazards & DerelictHazard.Radiation) != 0) risk += 0.15f;
            if ((hazards & DerelictHazard.SpaceFauna) != 0) risk += 0.25f;
            if ((hazards & DerelictHazard.Unstable) != 0) risk += 0.3f;
            if ((hazards & DerelictHazard.TrappedCrew) != 0) risk += 0.1f;
            if ((hazards & DerelictHazard.Boobytrapped) != 0) risk += 0.35f;
            if ((hazards & DerelictHazard.ContaminationBio) != 0) risk += 0.2f;
            if ((hazards & DerelictHazard.ContaminationChem) != 0) risk += 0.15f;
            if ((hazards & DerelictHazard.HostileAI) != 0) risk += 0.3f;

            return math.min(risk, 1f);
        }

        /// <summary>
        /// Generates hazards based on cause and age.
        /// </summary>
        public static DerelictHazard GenerateHazards(DerelictCause cause, uint ageInTicks, uint seed)
        {
            var random = new Unity.Mathematics.Random(seed);
            DerelictHazard hazards = DerelictHazard.None;

            // Base hazards from cause
            switch (cause)
            {
                case DerelictCause.Combat:
                    hazards |= DerelictHazard.Radiation;
                    if (random.NextFloat() < 0.4f) hazards |= DerelictHazard.Unstable;
                    break;

                case DerelictCause.SystemFailure:
                    if (random.NextFloat() < 0.3f) hazards |= DerelictHazard.Radiation;
                    if (random.NextFloat() < 0.2f) hazards |= DerelictHazard.ContaminationChem;
                    break;

                case DerelictCause.Mutiny:
                    if (random.NextFloat() < 0.5f) hazards |= DerelictHazard.TrappedCrew;
                    if (random.NextFloat() < 0.2f) hazards |= DerelictHazard.Boobytrapped;
                    break;

                case DerelictCause.Ancient:
                    if (random.NextFloat() < 0.6f) hazards |= DerelictHazard.HostileAI;
                    if (random.NextFloat() < 0.4f) hazards |= DerelictHazard.SpaceFauna;
                    break;
            }

            // Age-based hazards
            if (ageInTicks > 1000)
            {
                if (random.NextFloat() < 0.3f) hazards |= DerelictHazard.SpaceFauna;
            }

            return hazards;
        }

        /// <summary>
        /// Calculates condition decay based on age.
        /// </summary>
        public static DerelictCondition CalculateDecay(DerelictCondition initial, uint ageInTicks)
        {
            int decaySteps = (int)(ageInTicks / 500); // Decay every 500 ticks

            int condition = (int)initial + decaySteps;
            return (DerelictCondition)math.min(condition, (int)DerelictCondition.Decaying);
        }

        /// <summary>
        /// Calculates salvage time based on condition and hazards.
        /// </summary>
        public static uint CalculateSalvageTime(DerelictCondition condition, DerelictHazard hazards, float speedMod)
        {
            uint baseTime = condition switch
            {
                DerelictCondition.Pristine => 100,
                DerelictCondition.Damaged => 150,
                DerelictCondition.Ruined => 200,
                DerelictCondition.Stripped => 50,
                DerelictCondition.Decaying => 250,
                _ => 150
            };

            // Add time for hazard mitigation
            int hazardCount = math.countbits((uint)hazards);
            baseTime += (uint)(hazardCount * 30);

            return (uint)(baseTime / math.max(speedMod, 0.5f));
        }

        /// <summary>
        /// Rolls for hazard encounter during salvage.
        /// </summary>
        public static bool RollHazardEncounter(float riskLevel, float riskReduction, uint seed)
        {
            var random = new Unity.Mathematics.Random(seed);
            float adjustedRisk = riskLevel * (1f - riskReduction);
            return random.NextFloat() < adjustedRisk * 0.1f; // 10% base chance scaled by risk
        }
    }
}

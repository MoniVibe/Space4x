using Unity.Entities;

namespace PureDOTS.Runtime.Performance
{
    public enum TierProfileId : byte
    {
        Laptop = 0,
        Mid = 1,
        High = 2,
        Cinematic = 3,
        Debug = 4
    }

    /// <summary>
    /// Simulation LOD tier for AI/perception cost shaping.
    /// </summary>
    public enum AILODTier : byte
    {
        Tier0_Full = 0,
        Tier1_Reduced = 1,
        Tier2_EventDriven = 2,
        Tier3_Aggregate = 3
    }

    /// <summary>
    /// Per-entity tier assignment (written by tier assignment systems, read by sensing/AI).
    /// </summary>
    public struct AIFidelityTier : IComponentData
    {
        public AILODTier Tier;
        public uint LastChangeTick;
        public byte ReasonMask;
    }

    /// <summary>
    /// Policy surface for performance-aware AI. Drives global budgets and per-tier cadence knobs.
    /// </summary>
    public struct TierProfileSettings : IComponentData
    {
        public TierProfileId ActiveProfile;
        public uint Version;

        // Cadence (ticks)
        public int Tier0SensorCadenceTicks;
        public int Tier1SensorCadenceTicks;
        public int Tier2SensorCadenceTicks;
        public int Tier3SensorCadenceTicks;

        public int Tier0EvaluationCadenceTicks;
        public int Tier1EvaluationCadenceTicks;
        public int Tier2EvaluationCadenceTicks;
        public int Tier3EvaluationCadenceTicks;

        public int Tier0ResolutionCadenceTicks;
        public int Tier1ResolutionCadenceTicks;
        public int Tier2ResolutionCadenceTicks;
        public int Tier3ResolutionCadenceTicks;

        // Budgets (per tick)
        public int Tier0MaxPerceptionChecksPerTick;
        public int Tier1MaxPerceptionChecksPerTick;
        public int Tier2MaxPerceptionChecksPerTick;
        public int Tier3MaxPerceptionChecksPerTick;

        public int Tier0MaxTacticalDecisionsPerTick;
        public int Tier1MaxTacticalDecisionsPerTick;
        public int Tier2MaxTacticalDecisionsPerTick;
        public int Tier3MaxTacticalDecisionsPerTick;

        // Interest hysteresis (ticks) to avoid thrash.
        public uint TierHysteresisTicks;

        public static TierProfileSettings CreateDefaults(TierProfileId profile)
        {
            // Medium-first default: assume most entities are Tier1.
            var settings = new TierProfileSettings
            {
                ActiveProfile = profile,
                Version = 1,

                Tier0SensorCadenceTicks = 1,
                Tier1SensorCadenceTicks = 4,
                Tier2SensorCadenceTicks = 16,
                Tier3SensorCadenceTicks = 64,

                Tier0EvaluationCadenceTicks = 1,
                Tier1EvaluationCadenceTicks = 6,
                Tier2EvaluationCadenceTicks = 24,
                Tier3EvaluationCadenceTicks = 96,

                Tier0ResolutionCadenceTicks = 1,
                Tier1ResolutionCadenceTicks = 6,
                Tier2ResolutionCadenceTicks = 24,
                Tier3ResolutionCadenceTicks = 96,

                Tier0MaxPerceptionChecksPerTick = 64,
                Tier1MaxPerceptionChecksPerTick = 24,
                Tier2MaxPerceptionChecksPerTick = 8,
                Tier3MaxPerceptionChecksPerTick = 0,

                Tier0MaxTacticalDecisionsPerTick = 64,
                Tier1MaxTacticalDecisionsPerTick = 24,
                Tier2MaxTacticalDecisionsPerTick = 8,
                Tier3MaxTacticalDecisionsPerTick = 0,

                TierHysteresisTicks = 30
            };

            // Profile presets (no hard caps elsewhere; these are defaults)
            switch (profile)
            {
                case TierProfileId.Laptop:
                    settings.Tier0MaxPerceptionChecksPerTick = 32;
                    settings.Tier1MaxPerceptionChecksPerTick = 12;
                    settings.Tier0MaxTacticalDecisionsPerTick = 32;
                    settings.Tier1MaxTacticalDecisionsPerTick = 12;
                    settings.Tier1SensorCadenceTicks = 6;
                    settings.Tier1EvaluationCadenceTicks = 10;
                    settings.TierHysteresisTicks = 45;
                    break;
                case TierProfileId.High:
                    settings.Tier0MaxPerceptionChecksPerTick = 96;
                    settings.Tier1MaxPerceptionChecksPerTick = 40;
                    settings.Tier0MaxTacticalDecisionsPerTick = 96;
                    settings.Tier1MaxTacticalDecisionsPerTick = 40;
                    settings.Tier1SensorCadenceTicks = 3;
                    settings.Tier1EvaluationCadenceTicks = 5;
                    settings.TierHysteresisTicks = 20;
                    break;
                case TierProfileId.Cinematic:
                    settings.Tier0MaxPerceptionChecksPerTick = 128;
                    settings.Tier1MaxPerceptionChecksPerTick = 64;
                    settings.Tier0MaxTacticalDecisionsPerTick = 128;
                    settings.Tier1MaxTacticalDecisionsPerTick = 64;
                    settings.Tier1SensorCadenceTicks = 2;
                    settings.Tier1EvaluationCadenceTicks = 3;
                    settings.TierHysteresisTicks = 15;
                    break;
                case TierProfileId.Debug:
                    settings.Tier0MaxPerceptionChecksPerTick = 128;
                    settings.Tier1MaxPerceptionChecksPerTick = 64;
                    settings.Tier0MaxTacticalDecisionsPerTick = 128;
                    settings.Tier1MaxTacticalDecisionsPerTick = 64;
                    settings.Tier1SensorCadenceTicks = 2;
                    settings.Tier1EvaluationCadenceTicks = 3;
                    settings.TierHysteresisTicks = 5;
                    break;
                case TierProfileId.Mid:
                default:
                    break;
            }

            return settings;
        }
    }
}



using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Derived research unlock tiers and scalars for a colony/faction.
    /// </summary>
    public struct Space4XResearchUnlocks : IComponentData
    {
        public byte ExtractionTier;
        public byte ProcessingTier;
        public byte ProductionTier;
        public byte ModuleTier;
        public byte ShipClassTier;
        public byte SalvageTier;
        public byte TrainingTier;
        public byte BionicsTier;
        public byte ColonizationTier;
        public byte ProductionQueueSlots;

        public float ExtractionScalar;
        public float ProcessingScalar;
        public float ProductionScalar;
        public float SalvageScalar;
        public float TrainingScalar;
        public float BionicsBonus;

        public static Space4XResearchUnlocks FromTech(in TechLevel tech)
        {
            var extraction = tech.MiningTech;
            var processing = tech.ProcessingTech;
            var production = (byte)math.max((int)processing, (int)tech.HaulingTech);
            var module = tech.CombatTech;
            var shipClass = (byte)math.max((int)tech.CombatTech,
                math.max((int)tech.MiningTech, math.max((int)tech.HaulingTech, (int)tech.ProcessingTech)));
            var salvage = tech.CombatTech;
            var training = (byte)math.max((int)tech.HaulingTech, (int)tech.ProcessingTech);
            var bionics = tech.ProcessingTech;
            var colonization = (byte)math.max((int)tech.MiningTech, (int)tech.HaulingTech);

            var queueSlots = (byte)math.clamp(1 + production / 2, 1, 4);

            return new Space4XResearchUnlocks
            {
                ExtractionTier = extraction,
                ProcessingTier = processing,
                ProductionTier = production,
                ModuleTier = module,
                ShipClassTier = shipClass,
                SalvageTier = salvage,
                TrainingTier = training,
                BionicsTier = bionics,
                ColonizationTier = colonization,
                ProductionQueueSlots = queueSlots,
                ExtractionScalar = 1f + extraction * 0.08f,
                ProcessingScalar = 1f + processing * 0.06f,
                ProductionScalar = 1f + production * 0.05f,
                SalvageScalar = 1f + salvage * 0.1f,
                TrainingScalar = 1f + training * 0.05f,
                BionicsBonus = bionics * 0.02f
            };
        }
    }

    /// <summary>
    /// Baseline mining efficiency before research scaling.
    /// </summary>
    public struct MiningEfficiencyBaseline : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Baseline salvage capability before research scaling.
    /// </summary>
    public struct SalvageCapabilityBaseline : IComponentData
    {
        public half SpeedBonus;
        public half RiskReduction;
        public half YieldBonus;
        public byte CanReactivate;
    }

    /// <summary>
    /// Baseline crew reserve training policy before research scaling.
    /// </summary>
    public struct CrewReservePolicyBaseline : IComponentData
    {
        public float TrainingRatePerTick;
        public float MinTraining;
        public float MaxTraining;
    }

    /// <summary>
    /// Baseline crew training state before research scaling.
    /// </summary>
    public struct CrewTrainingStateBaseline : IComponentData
    {
        public float TrainingRatePerTick;
        public float MaxTraining;
    }
}

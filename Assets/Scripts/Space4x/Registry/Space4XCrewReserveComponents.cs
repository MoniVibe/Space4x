using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Aggregate reserve crew pool for colonies, stations, or other providers.
    /// </summary>
    public struct CrewReservePool : IComponentData
    {
        public float Available;
        public float MaxReserve;
        public float TrainingLevel;
    }

    /// <summary>
    /// Policy for building and training reserve crews.
    /// </summary>
    public struct CrewReservePolicy : IComponentData
    {
        public float ReserveFraction;
        public float ReserveCap;
        public float RecruitmentRate;
        public float TrainingRatePerTick;
        public float MinTraining;
        public float MaxTraining;
    }

    /// <summary>
    /// Onboard crew training progression for a ship.
    /// </summary>
    public struct CrewTrainingState : IComponentData
    {
        public float TrainingLevel;
        public float TrainingRatePerTick;
        public float MaxTraining;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Policy for requesting reserve crew transfers.
    /// </summary>
    public struct CrewTransferPolicy : IComponentData
    {
        public float DesiredCrewRatio;
        public int MaxTransferPerTick;
        public float MinProviderTraining;
    }

    /// <summary>
    /// Policy for promoting reserve crew into officer seats.
    /// </summary>
    public struct CrewPromotionPolicy : IComponentData
    {
        public float MinTrainingForOfficer;
        public int MinCrewReserve;
        public int MaxPromotionsPerTick;
        public uint PromotionCooldownTicks;
        public uint LastPromotionTick;
    }
}

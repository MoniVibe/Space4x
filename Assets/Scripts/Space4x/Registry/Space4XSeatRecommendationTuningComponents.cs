using Unity.Entities;

namespace Space4X.Registry
{
    public struct CombatRecommendationTuning
    {
        public float MinWeaponsOnlineRatio;
        public byte MinContacts;
        public byte AttackPriorityBase;
        public byte AttackPriorityPerContact;
        public byte AttackPriorityMin;
    }

    public struct SensorsRecommendationTuning
    {
        public float MinSensorRange;
        public byte PatrolPriority;
        public byte InterceptPriority;
    }

    public struct LogisticsRecommendationTuning
    {
        public float RetreatHullRatio;
        public float ResupplyFuelRatio;
        public float ResupplyAmmoRatio;
        public byte RetreatPriority;
        public byte ResupplyPriority;
    }

    public struct SeatRecommendationTuning : IComponentData
    {
        public CombatRecommendationTuning Combat;
        public SensorsRecommendationTuning Sensors;
        public LogisticsRecommendationTuning Logistics;

        public static SeatRecommendationTuning Default => new SeatRecommendationTuning
        {
            Combat = new CombatRecommendationTuning
            {
                MinWeaponsOnlineRatio = 0.1f,
                MinContacts = 1,
                AttackPriorityBase = 80,
                AttackPriorityPerContact = 10,
                AttackPriorityMin = 10
            },
            Sensors = new SensorsRecommendationTuning
            {
                MinSensorRange = 0f,
                PatrolPriority = 80,
                InterceptPriority = 60
            },
            Logistics = new LogisticsRecommendationTuning
            {
                RetreatHullRatio = 0.3f,
                ResupplyFuelRatio = 0.25f,
                ResupplyAmmoRatio = 0.2f,
                RetreatPriority = 20,
                ResupplyPriority = 40
            }
        };
    }
}

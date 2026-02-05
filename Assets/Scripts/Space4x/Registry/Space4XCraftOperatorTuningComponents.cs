using Unity.Entities;

namespace Space4X.Registry
{
    public struct OperatorDomainTuning
    {
        public float CommandWeight;
        public float TacticsWeight;
        public float LogisticsWeight;
        public float DiplomacyWeight;
        public float EngineeringWeight;
        public float ResolveWeight;
        public float ConsoleQualityScale;
        public float ConsoleQualityBias;
    }

    public struct CraftOperatorTuning : IComponentData
    {
        public OperatorDomainTuning Movement;
        public OperatorDomainTuning Combat;
        public OperatorDomainTuning Sensors;
        public OperatorDomainTuning Logistics;
        public OperatorDomainTuning Communications;
        public OperatorDomainTuning FlightOps;

        public static CraftOperatorTuning Default => new CraftOperatorTuning
        {
            Movement = new OperatorDomainTuning
            {
                CommandWeight = 0.2f,
                TacticsWeight = 0.45f,
                LogisticsWeight = 0f,
                DiplomacyWeight = 0f,
                EngineeringWeight = 0.35f,
                ResolveWeight = 0f,
                ConsoleQualityScale = 1f,
                ConsoleQualityBias = 0f
            },
            Combat = new OperatorDomainTuning
            {
                CommandWeight = 0.3f,
                TacticsWeight = 0.5f,
                LogisticsWeight = 0f,
                DiplomacyWeight = 0f,
                EngineeringWeight = 0f,
                ResolveWeight = 0.2f,
                ConsoleQualityScale = 1f,
                ConsoleQualityBias = 0f
            },
            Sensors = new OperatorDomainTuning
            {
                CommandWeight = 0.2f,
                TacticsWeight = 0.3f,
                LogisticsWeight = 0f,
                DiplomacyWeight = 0f,
                EngineeringWeight = 0.5f,
                ResolveWeight = 0f,
                ConsoleQualityScale = 1f,
                ConsoleQualityBias = 0f
            },
            Logistics = new OperatorDomainTuning
            {
                CommandWeight = 0.25f,
                TacticsWeight = 0f,
                LogisticsWeight = 0.55f,
                DiplomacyWeight = 0f,
                EngineeringWeight = 0f,
                ResolveWeight = 0.2f,
                ConsoleQualityScale = 1f,
                ConsoleQualityBias = 0f
            },
            Communications = new OperatorDomainTuning
            {
                CommandWeight = 0.3f,
                TacticsWeight = 0f,
                LogisticsWeight = 0f,
                DiplomacyWeight = 0.5f,
                EngineeringWeight = 0f,
                ResolveWeight = 0.2f,
                ConsoleQualityScale = 1f,
                ConsoleQualityBias = 0f
            },
            FlightOps = new OperatorDomainTuning
            {
                CommandWeight = 0.3f,
                TacticsWeight = 0.4f,
                LogisticsWeight = 0.3f,
                DiplomacyWeight = 0f,
                EngineeringWeight = 0f,
                ResolveWeight = 0f,
                ConsoleQualityScale = 1f,
                ConsoleQualityBias = 0f
            }
        };
    }
}

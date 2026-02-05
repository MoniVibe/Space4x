using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Craft Operator Tuning")]
    public sealed class Space4XCraftOperatorTuningAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct DomainTuning
        {
            [Range(0f, 1f)] public float commandWeight;
            [Range(0f, 1f)] public float tacticsWeight;
            [Range(0f, 1f)] public float logisticsWeight;
            [Range(0f, 1f)] public float diplomacyWeight;
            [Range(0f, 1f)] public float engineeringWeight;
            [Range(0f, 1f)] public float resolveWeight;
            [Range(0.1f, 2f)] public float consoleQualityScale;
            [Range(-0.5f, 0.5f)] public float consoleQualityBias;

            public DomainTuning Clamp()
            {
                commandWeight = math.clamp(commandWeight, 0f, 1f);
                tacticsWeight = math.clamp(tacticsWeight, 0f, 1f);
                logisticsWeight = math.clamp(logisticsWeight, 0f, 1f);
                diplomacyWeight = math.clamp(diplomacyWeight, 0f, 1f);
                engineeringWeight = math.clamp(engineeringWeight, 0f, 1f);
                resolveWeight = math.clamp(resolveWeight, 0f, 1f);
                consoleQualityScale = math.clamp(consoleQualityScale, 0.1f, 2f);
                consoleQualityBias = math.clamp(consoleQualityBias, -0.5f, 0.5f);
                return this;
            }

            public OperatorDomainTuning ToRuntime()
            {
                return new OperatorDomainTuning
                {
                    CommandWeight = commandWeight,
                    TacticsWeight = tacticsWeight,
                    LogisticsWeight = logisticsWeight,
                    DiplomacyWeight = diplomacyWeight,
                    EngineeringWeight = engineeringWeight,
                    ResolveWeight = resolveWeight,
                    ConsoleQualityScale = consoleQualityScale,
                    ConsoleQualityBias = consoleQualityBias
                };
            }
        }

        [Header("Movement")]
        public DomainTuning movement = new DomainTuning
        {
            commandWeight = 0.2f,
            tacticsWeight = 0.45f,
            logisticsWeight = 0f,
            diplomacyWeight = 0f,
            engineeringWeight = 0.35f,
            resolveWeight = 0f,
            consoleQualityScale = 1f,
            consoleQualityBias = 0f
        };

        [Header("Combat")]
        public DomainTuning combat = new DomainTuning
        {
            commandWeight = 0.3f,
            tacticsWeight = 0.5f,
            logisticsWeight = 0f,
            diplomacyWeight = 0f,
            engineeringWeight = 0f,
            resolveWeight = 0.2f,
            consoleQualityScale = 1f,
            consoleQualityBias = 0f
        };

        [Header("Sensors")]
        public DomainTuning sensors = new DomainTuning
        {
            commandWeight = 0.2f,
            tacticsWeight = 0.3f,
            logisticsWeight = 0f,
            diplomacyWeight = 0f,
            engineeringWeight = 0.5f,
            resolveWeight = 0f,
            consoleQualityScale = 1f,
            consoleQualityBias = 0f
        };

        [Header("Logistics")]
        public DomainTuning logistics = new DomainTuning
        {
            commandWeight = 0.25f,
            tacticsWeight = 0f,
            logisticsWeight = 0.55f,
            diplomacyWeight = 0f,
            engineeringWeight = 0f,
            resolveWeight = 0.2f,
            consoleQualityScale = 1f,
            consoleQualityBias = 0f
        };

        [Header("Communications")]
        public DomainTuning communications = new DomainTuning
        {
            commandWeight = 0.3f,
            tacticsWeight = 0f,
            logisticsWeight = 0f,
            diplomacyWeight = 0.5f,
            engineeringWeight = 0f,
            resolveWeight = 0.2f,
            consoleQualityScale = 1f,
            consoleQualityBias = 0f
        };

        [Header("Flight Ops")]
        public DomainTuning flightOps = new DomainTuning
        {
            commandWeight = 0.3f,
            tacticsWeight = 0.4f,
            logisticsWeight = 0.3f,
            diplomacyWeight = 0f,
            engineeringWeight = 0f,
            resolveWeight = 0f,
            consoleQualityScale = 1f,
            consoleQualityBias = 0f
        };

        private void OnValidate()
        {
            movement = movement.Clamp();
            combat = combat.Clamp();
            sensors = sensors.Clamp();
            logistics = logistics.Clamp();
            communications = communications.Clamp();
            flightOps = flightOps.Clamp();
        }

        private sealed class Baker : Baker<Space4XCraftOperatorTuningAuthoring>
        {
            public override void Bake(Space4XCraftOperatorTuningAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CraftOperatorTuning
                {
                    Movement = authoring.movement.Clamp().ToRuntime(),
                    Combat = authoring.combat.Clamp().ToRuntime(),
                    Sensors = authoring.sensors.Clamp().ToRuntime(),
                    Logistics = authoring.logistics.Clamp().ToRuntime(),
                    Communications = authoring.communications.Clamp().ToRuntime(),
                    FlightOps = authoring.flightOps.Clamp().ToRuntime()
                });
            }
        }
    }
}

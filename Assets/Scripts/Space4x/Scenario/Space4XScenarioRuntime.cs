using Unity.Entities;

namespace Space4x.Scenario
{
    public struct Space4XScenarioRuntime : IComponentData
    {
        public uint StartTick;
        public uint EndTick;
        public float DurationSeconds;
    }
}


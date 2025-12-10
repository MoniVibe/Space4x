using PureDOTS.Runtime;
using Unity.Entities;

namespace Space4X.Demo
{
    /// <summary>
    /// Tracks whether the Space4X demo scenario is active and when it started.
    /// </summary>
    public struct DemoScenarioState : IComponentData
    {
        public bool IsActive;
        public float StartWorldSeconds;

        // Compatibility fields to mirror shared demo scenario state expectations.
        public DemoScenario Current;
        public bool IsInitialized;
        public DemoBootPhase BootPhase;
        public bool EnableGodgame;
        public bool EnableSpace4x;
        public bool EnableEconomy;
    }
}

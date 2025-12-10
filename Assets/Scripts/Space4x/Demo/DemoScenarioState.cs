using Unity.Entities;

namespace Space4X.Demo
{
    /// <summary>
    /// Shared high-level state for the current demo scenario.
    /// Behavior and narrative systems expect a single singleton of this.
    /// </summary>
    public struct DemoScenarioState : IComponentData
    {
        public int ScenarioId;

        public bool CombatEnabled;
        public bool MiningEnabled;
        public bool StrikeCraftEnabled;
        public bool NarrativeEnabled;

        public float TimeSinceStart;
        public int Phase;

        // Compatibility flags used by existing systems.
        public bool EnableSpace4x;
        public bool EnableGodgame;
        public bool EnableEconomy;
        public bool IsInitialized;

        // Optional fields commonly referenced by callers; safe to leave defaulted.
        public bool IsActive;
        public int Stage;
    }
}

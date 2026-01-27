using Unity.Entities;

namespace PureDOTS.Runtime
{
    /// <summary>
    /// Scenario modes for switching between different simulation/game configurations.
    /// Used to gate systems and control what's active in showcase scenes.
    /// </summary>
    public enum ScenarioKind : byte
    {
        /// <summary>
        /// All systems active (default showcase mode).
        /// </summary>
        AllSystemsShowcase = 0,

        /// <summary>
        /// Only Space4X physics, mining, and hand systems active.
        /// </summary>
        Space4XPhysicsOnly = 1,

        /// <summary>
        /// Only Godgame physics and resource systems active.
        /// </summary>
        GodgamePhysicsOnly = 2,

        /// <summary>
        /// Hand throw sandbox - minimal systems, focused on grab/throw testing.
        /// </summary>
        HandThrowSandbox = 3
    }

    /// <summary>
    /// Boot phase for scenario spawning.
    /// Used to spread spawning across multiple frames.
    /// </summary>
    public enum ScenarioBootPhase : byte
    {
        None = 0,
        SpawnGodgame = 1,
        SpawnSpace4x = 2,
        Done = 3
    }

    /// <summary>
    /// Singleton component tracking the current scenario mode.
    /// Systems should check this to determine if they should run.
    /// </summary>
    public struct ScenarioState : IComponentData
    {
        /// <summary>
        /// Current active scenario mode.
        /// </summary>
        public ScenarioKind Current;

        /// <summary>
        /// Whether the scenario has been initialized (entities spawned).
        /// </summary>
        public bool IsInitialized;

        /// <summary>
        /// Current boot phase for phased spawning.
        /// </summary>
        public ScenarioBootPhase BootPhase;

        /// <summary>
        /// Enable Godgame slice (villages, villagers, terrain).
        /// </summary>
        public bool EnableGodgame;

        /// <summary>
        /// Enable Space4X slice (carriers, miners, asteroids).
        /// </summary>
        public bool EnableSpace4x;

        /// <summary>
        /// Enable economy/logistics systems.
        /// </summary>
        public bool EnableEconomy;
    }
}



using Space4X.Modes;

namespace Space4X.Modes.FleetCrawl
{
    /// <summary>
    /// FleetCrawl mode package entry points/constants.
    /// Keeps mode routing explicit at shell level.
    /// </summary>
    public static class Space4XFleetCrawlModePackage
    {
        public static string ScenarioId => Space4XModeSelectionState.FleetCrawlScenarioId;
        public static string ScenarioPath => Space4XModeSelectionState.FleetCrawlScenarioPath;
        public static uint Seed => Space4XModeSelectionState.FleetCrawlSeed;
        public static bool IsActive => Space4XModeSelectionState.IsFleetCrawlModeActive;
    }
}

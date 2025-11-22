using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Current tech tiers per domain; upgraded when diffusion completes.
    /// </summary>
    public struct TechLevel : IComponentData
    {
        public byte MiningTech;
        public byte CombatTech;
        public byte HaulingTech;
        public byte ProcessingTech;
        public uint LastUpgradeTick;
    }

    /// <summary>
    /// Tracks diffusion progress toward new tech levels sourced from a core world or research node.
    /// </summary>
    public struct TechDiffusionState : IComponentData
    {
        public Entity SourceEntity;
        public float DiffusionProgressSeconds;
        public float DiffusionDurationSeconds;
        public byte TargetMiningTech;
        public byte TargetCombatTech;
        public byte TargetHaulingTech;
        public byte TargetProcessingTech;
        public byte Active;
        public uint DiffusionStartTick;
    }

    /// <summary>
    /// Telemetry counters for active/completed tech diffusions.
    /// </summary>
    public struct TechDiffusionTelemetry : IComponentData
    {
        public uint LastUpdateTick;
        public uint LastUpgradeTick;
        public int ActiveDiffusions;
        public uint CompletedUpgrades;
    }

    /// <summary>
    /// Command-log entry for completed tech diffusions to keep rewind/telemetry in sync.
    /// </summary>
    public struct TechDiffusionCommandLogEntry : IBufferElementData
    {
        public uint Tick;
        public Entity TargetEntity;
        public Entity SourceEntity;
        public byte MiningTech;
        public byte CombatTech;
        public byte HaulingTech;
        public byte ProcessingTech;
    }
}

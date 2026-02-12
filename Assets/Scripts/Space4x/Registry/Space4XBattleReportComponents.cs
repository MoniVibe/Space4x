using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Snapshot of a completed battle, intended for gameplay/UI consumption.
    /// Produced by <see cref="Space4XBattleReportSystem"/>.
    /// </summary>
    public struct Space4XBattleReport : IComponentData
    {
        public uint BattleStartTick;
        public uint BattleEndTick;
        public int WinnerSide;

        public int TotalCombatants;
        public int TotalDestroyed;
        public int TotalAlive;

        public int ShotsFired;
        public int ShotsHit;
        public float DamageDealtTotal;
        public float DamageReceivedTotal;
    }

    /// <summary>
    /// Per-side rollup for a completed battle.
    /// </summary>
    public struct Space4XBattleReportSide : IBufferElementData
    {
        public int Side;
        public int ShipsTotal;
        public int ShipsDestroyed;
        public int ShipsAlive;
        public float ShipsAliveRatio;

        public float HullRatio;
        public float DamageDealt;
        public float DamageReceived;
    }
}


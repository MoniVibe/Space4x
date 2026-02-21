using Unity.Collections;
using Unity.Entities;

namespace Space4x.Scenario
{
    public static class Space4XFleetCrawlScenario
    {
        public static readonly FixedString64Bytes ScenarioId = new FixedString64Bytes("space4x_fleetcrawl_micro");
    }

    public enum Space4XFleetCrawlRoomKind : byte
    {
        Combat = 0,
        Relief = 1,
        Boss = 2
    }

    public enum Space4XFleetCrawlRewardKind : byte
    {
        Boon = 0,
        Money = 1,
        Upgrade = 2,
        ReliefNode = 3
    }

    public enum Space4XFleetCrawlBoonGod : byte
    {
        None = 0,
        Athena = 1,
        Ares = 2,
        Artemis = 3,
        Hermes = 4,
        Poseidon = 5,
        Zeus = 6
    }

    public struct Space4XFleetCrawlRunTag : IComponentData
    {
    }

    public struct Space4XFleetCrawlRunSeed : IComponentData
    {
        public uint Value;
    }

    public struct Space4XFleetCrawlRunProgress : IComponentData
    {
        public int RoomIndex;
        public int BossEveryRooms;
        public int RoomsUntilBoss;
        public byte AwaitingGateResolve;
        public uint Digest;
    }

    public struct Space4XFleetCrawlRoomTag : IComponentData
    {
    }

    public struct Space4XFleetCrawlRoomOwner : IComponentData
    {
        public Entity RunEntity;
    }

    public struct Space4XFleetCrawlRoomState : IComponentData
    {
        public Space4XFleetCrawlRoomKind Kind;
        public uint StartTick;
        public uint EndTick;
        public byte Completed;
    }

    public struct Space4XFleetCrawlRewardBagItem : IBufferElementData
    {
        public Space4XFleetCrawlRewardKind RewardKind;
    }

    public struct Space4XFleetCrawlGateOption : IBufferElementData
    {
        public Space4XFleetCrawlRewardKind RewardKind;
        public Space4XFleetCrawlBoonGod BoonGod;
        public uint RollSalt;
    }

    public struct Space4XFleetCrawlBoonChoice : IBufferElementData
    {
        public Space4XFleetCrawlBoonGod God;
        public FixedString64Bytes BoonId;
    }

    public struct Space4XFleetCrawlPendingGatePick : IComponentData
    {
        public int PickedIndex;
        public byte HasPick;
    }

    public struct Space4XFleetCrawlPendingBoonPick : IComponentData
    {
        public int PickedIndex;
        public byte HasPick;
    }

    public struct Space4XFleetCrawlCurrency : IComponentData
    {
        public int Credits;
    }

    public struct Space4XFleetCrawlUpgradePoints : IComponentData
    {
        public int Value;
    }

    public struct Space4XFleetCrawlReliefCount : IComponentData
    {
        public int Value;
    }

    public struct Space4XFleetCrawlRewardApplied : IBufferElementData
    {
        public int RoomIndex;
        public Space4XFleetCrawlRewardKind RewardKind;
        public Space4XFleetCrawlBoonGod BoonGod;
        public int Amount;
        public uint Tick;
    }
}

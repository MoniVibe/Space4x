using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum Space4XMissionType : byte
    {
        Scout = 0,
        Mine = 1,
        HaulDelivery = 2,
        HaulProcure = 3,
        Patrol = 4,
        Intercept = 5,
        BuildStation = 6
    }

    public enum Space4XMissionStatus : byte
    {
        Open = 0,
        Assigned = 1,
        Active = 2,
        Completed = 3,
        Failed = 4,
        Expired = 5
    }

    public enum Space4XMissionPhase : byte
    {
        None = 0,
        ToSource = 1,
        ToDestination = 2
    }

    public enum Space4XMissionCargoState : byte
    {
        None = 0,
        Loading = 1,
        Loaded = 2,
        Delivered = 3
    }

    public struct Space4XMissionOffer : IComponentData
    {
        public uint OfferId;
        public Space4XMissionType Type;
        public Space4XMissionStatus Status;
        public Entity Issuer;
        public ushort IssuerFactionId;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public ushort ResourceTypeIndex;
        public float Units;
        public float RewardCredits;
        public float RewardStanding;
        public float RewardLp;
        public float Risk;
        public byte Priority;
        public uint CreatedTick;
        public uint ExpiryTick;
        public uint AssignedTick;
        public uint CompletedTick;
        public Entity AssignedEntity;
    }

    public struct Space4XMissionAssignment : IComponentData
    {
        public Entity OfferEntity;
        public uint OfferId;
        public Space4XMissionType Type;
        public Space4XMissionStatus Status;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public Entity SourceEntity;
        public float3 SourcePosition;
        public float3 DestinationPosition;
        public Space4XMissionPhase Phase;
        public Space4XMissionCargoState CargoState;
        public ushort ResourceTypeIndex;
        public float Units;
        public float CargoUnits;
        public float RewardCredits;
        public float RewardStanding;
        public float RewardLp;
        public ushort IssuerFactionId;
        public uint StartedTick;
        public uint DueTick;
        public uint CompletedTick;
        public byte AutoComplete;
    }

    public struct Space4XMissionBoardConfig : IComponentData
    {
        public uint GenerationIntervalTicks;
        public uint OfferExpiryTicks;
        public uint AssignmentBaseTicks;
        public uint AssignmentVarianceTicks;
        public byte MaxOffersPerFaction;
        public byte MaxAssignmentsPerTick;
        public float BaseReward;
        public float RewardPerUnit;
        public float RewardPerRing;

        public static Space4XMissionBoardConfig Default => new Space4XMissionBoardConfig
        {
            GenerationIntervalTicks = 240,
            OfferExpiryTicks = 1200,
            AssignmentBaseTicks = 300,
            AssignmentVarianceTicks = 240,
            MaxOffersPerFaction = 10,
            MaxAssignmentsPerTick = 6,
            BaseReward = 40f,
            RewardPerUnit = 0.6f,
            RewardPerRing = 10f
        };
    }

    public struct Space4XMissionBoardState : IComponentData
    {
        public uint LastGenerationTick;
        public uint NextOfferId;
    }
}

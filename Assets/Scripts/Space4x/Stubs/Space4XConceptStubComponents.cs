// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Runtime
{
    public struct MiningOrderTag : IComponentData { }

    public struct HaulOrderTag : IComponentData { }

    public struct ExplorationProbeTag : IComponentData { }

    public struct FleetInterceptIntent : IComponentData
    {
        public Entity Target;
        public byte Priority;
    }

    public struct ComplianceBreachEvent : IComponentData
    {
        public Entity Source;
        public byte Severity;
    }

    public struct TechDiffusionNode : IComponentData
    {
        public int NodeId;
        public byte Tier;
    }

    public struct StationConstructionSite : IComponentData
    {
        public int SiteId;
        public byte Phase;
    }

    public struct AnomalyTag : IComponentData { }

    public struct TradeContractHandle : IComponentData
    {
        public int ContractId;
    }

    public struct CarrierBehaviorTreeRef : IComponentData
    {
        public int TreeId;
        public byte Mode;
    }

    public struct CarrierBehaviorState : IComponentData
    {
        public int ActiveNodeId;
        public byte Status;
    }

    public struct CarrierBehaviorNodeState : IBufferElementData
    {
        public int NodeId;
        public byte Flags;
    }

    public struct CarrierPerceptionConfig : IComponentData
    {
        public float Range;
        public byte ChannelMask;
        public byte MaxStimuli;
    }

    public struct CarrierPerceptionStimulus : IBufferElementData
    {
        public Entity Source;
        public float Strength;
        public byte Channel;
        public uint Timestamp;
    }

    public struct CarrierHangarState : IComponentData
    {
        public byte ReadyFighters;
        public byte LaunchCapacity;
    }

    public struct CarrierBehaviorModifier : IComponentData
    {
        public float Value;
    }

    public struct CarrierInitiativeState : IComponentData
    {
        public float Charge;
    }

    public struct CarrierNeedElement : IBufferElementData
    {
        public byte NeedType;
        public float Urgency;
    }

    public struct FleetHandle : IComponentData
    {
        public int FleetId;
    }

    public struct InterceptState : IComponentData
    {
        public byte Active;
    }

    public struct FleetMembershipElement : IBufferElementData
    {
        public Entity Member;
        public byte Role;
    }

    public struct GuildHandle : IComponentData
    {
        public int GuildId;
    }

    public struct ModuleCraftJob : IComponentData
    {
        public int RecipeId;
    }

    public struct CraftedItemHandle : IComponentData
    {
        public int ItemId;
    }

    public struct CraftQualityState : IComponentData
    {
        public float QualityScore;
    }

    public struct StrikeCraftSpawnRequest : IComponentData
    {
        public Entity Carrier;
        public byte SquadronSize;
        public byte Role;
    }

    public struct FormationAnchor : IComponentData
    {
        public int FormationId;
        public byte State;
    }

    public struct AttackRunIntent : IComponentData
    {
        public Entity Target;
        public byte Priority;
    }

    public struct AttackRunProgress : IComponentData
    {
        public byte Phase;
        public float Value;
    }

    public struct TravelRequest : IComponentData
    {
        public Entity Destination;
        public byte Mode;
    }

    public struct ArrivalWaypoint : IComponentData
    {
        public float3 Position;
    }

    public struct NavigationTicketRef : IComponentData
    {
        public Entity Ticket;
    }

    public struct MovementProfileId : IComponentData
    {
        public int Profile;
    }

    public struct CarrierSensorRig : IComponentData
    {
        public byte ChannelsMask;
    }

    public struct CarrierInterruptTicket : IComponentData
    {
        public byte Category;
    }

    public struct Space4XTimeCommand : IComponentData
    {
        public byte Command;
    }

    public struct Space4XBookmark : IComponentData
    {
        public int BookmarkId;
    }

    public struct Space4XSituationAnchor : IComponentData
    {
        public int SituationId;
    }

    public struct TelemetryStreamHook : IComponentData
    {
        public int StreamId;
    }

    public struct SaveSlotRequest : IComponentData
    {
        public int SlotIndex;
        public byte Action; // save/load
    }

    public struct StationProductionSlot : IComponentData
    {
        public int SlotId;
    }

    public struct RefineryJobRequest : IComponentData
    {
        public Entity Facility;
        public int RecipeId;
    }

    public struct TradeLedgerEntry : IBufferElementData
    {
        public int ContractId;
        public float Amount;
    }

    public struct CarrierDeliveryManifest : IComponentData
    {
        public int ManifestId;
    }
}

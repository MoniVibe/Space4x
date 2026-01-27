using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Signals
{
    /// <summary>
    /// Type of signal being broadcast.
    /// </summary>
    public enum SignalType : byte
    {
        None = 0,
        Distress = 1,       // Help request
        Rally = 2,          // Gather here
        Alert = 3,          // Danger warning
        Message = 4,        // General communication
        Beacon = 5,         // Navigation marker
        Trade = 6,          // Trade offer
        Discovery = 7,      // Found something
        Combat = 8,         // Battle occurring
        Retreat = 9         // Fall back order
    }

    /// <summary>
    /// Signal priority level.
    /// </summary>
    public enum SignalPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Urgent = 3,
        Critical = 4
    }

    /// <summary>
    /// Signal propagation mode.
    /// </summary>
    public enum PropagationMode : byte
    {
        Radial = 0,         // Spreads in all directions
        Directional = 1,    // Cone-shaped
        LineOfSight = 2,    // Requires clear path
        Network = 3         // Through relay chain
    }

    /// <summary>
    /// Active signal broadcast.
    /// </summary>
    public struct Signal : IComponentData
    {
        public SignalType Type;
        public SignalPriority Priority;
        public PropagationMode Propagation;
        public float3 Origin;                   // Source position
        public float3 Direction;                // For directional signals
        public float Range;                     // Maximum reach
        public float CurrentRange;              // Expanding wavefront
        public float Strength;                  // Signal strength 0-1
        public float DecayRate;                 // Strength loss per tick
        public float ExpansionRate;             // How fast range grows
        public uint EmittedTick;
        public uint ExpirationTick;             // When signal expires
        public Entity SourceEntity;
        public FixedString64Bytes PayloadId;    // Message identifier
        public byte IsActive;
    }

    /// <summary>
    /// Entity that can receive signals.
    /// </summary>
    public struct SignalReceiver : IComponentData
    {
        public float Sensitivity;               // Detection multiplier
        public float MaxRange;                  // Reception range
        public byte CanReceiveDistress;
        public byte CanReceiveRally;
        public byte CanReceiveAlert;
        public byte CanReceiveMessage;
        public byte IsJammed;                   // Cannot receive
    }

    /// <summary>
    /// Received signal buffer.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ReceivedSignal : IBufferElementData
    {
        public Entity SignalEntity;
        public SignalType Type;
        public SignalPriority Priority;
        public float3 Origin;
        public float Strength;                  // Received strength
        public float Distance;                  // From receiver
        public uint ReceivedTick;
        public FixedString64Bytes PayloadId;
        public byte WasProcessed;
    }

    /// <summary>
    /// Message queue for delayed communications.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct QueuedMessage : IBufferElementData
    {
        public Entity RecipientEntity;
        public FixedString64Bytes MessageId;
        public FixedString128Bytes Content;
        public SignalPriority Priority;
        public uint QueuedTick;
        public uint DeliveryTick;               // When to deliver
        public byte IsDelivered;
        public byte RequiresAck;                // Needs acknowledgment
    }

    /// <summary>
    /// Alert state for danger awareness.
    /// </summary>
    public struct AlertState : IComponentData
    {
        public byte AlertLevel;                 // 0 = calm, 255 = max alert
        public SignalType TriggeringSignal;
        public float3 ThreatDirection;
        public float ThreatDistance;
        public Entity ThreatEntity;
        public uint AlertStartTick;
        public uint AlertDecayTick;             // When alert reduces
        public byte IsInCombat;
        public byte IsFleeing;
    }

    /// <summary>
    /// Beacon for navigation/relay network.
    /// </summary>
    public struct Beacon : IComponentData
    {
        public FixedString32Bytes BeaconId;
        public float BroadcastRange;
        public float RelayRange;                // Range to next beacon
        public float SignalBoost;               // Amplification factor
        public uint LastPingTick;
        public byte IsActive;
        public byte IsRelay;                    // Part of network
        public byte CanAmplify;                 // Boosts passing signals
    }

    /// <summary>
    /// Connection in beacon network.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct BeaconConnection : IBufferElementData
    {
        public Entity ConnectedBeacon;
        public float Distance;
        public float SignalQuality;             // 0-1 connection quality
        public byte IsActive;
        public byte IsBidirectional;
    }

    /// <summary>
    /// Signal jammer/interference.
    /// </summary>
    public struct SignalJammer : IComponentData
    {
        public float JamRadius;
        public float JamStrength;               // 0-1 blocking power
        public SignalType TargetType;           // Specific or all
        public byte JamAllTypes;
        public byte IsActive;
    }

    /// <summary>
    /// Signal emitter capability.
    /// </summary>
    public struct SignalEmitter : IComponentData
    {
        public float MaxRange;
        public float BaseStrength;
        public float Cooldown;                  // Ticks between emissions
        public uint LastEmissionTick;
        public byte CanBroadcastDistress;
        public byte CanBroadcastRally;
        public byte CanBroadcastAlert;
        public byte IsEnabled;
    }

    /// <summary>
    /// Request to broadcast a signal.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct BroadcastRequest : IBufferElementData
    {
        public SignalType Type;
        public SignalPriority Priority;
        public PropagationMode Propagation;
        public float Range;
        public float Duration;                  // Ticks active
        public float3 Direction;                // For directional
        public FixedString64Bytes PayloadId;
        public uint RequestTick;
        public byte WasProcessed;
    }

    /// <summary>
    /// Signal history for analysis.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct SignalHistoryEntry : IBufferElementData
    {
        public SignalType Type;
        public float3 Origin;
        public float Strength;
        public uint ReceivedTick;
        public Entity SourceEntity;
    }

    /// <summary>
    /// Rally point for gathering.
    /// </summary>
    public struct RallyPoint : IComponentData
    {
        public float3 Position;
        public float GatherRadius;              // Area to gather in
        public float SignalRange;               // How far rally broadcasts
        public uint CreatedTick;
        public uint ExpirationTick;
        public Entity CreatorEntity;
        public byte IsActive;
        public byte IsPermanent;
    }

    /// <summary>
    /// Distress call state.
    /// </summary>
    public struct DistressState : IComponentData
    {
        public float DistressLevel;             // 0-1 urgency
        public float3 DistressPosition;
        public FixedString32Bytes DistressReason;
        public uint DistressStartTick;
        public uint LastBroadcastTick;
        public byte IsInDistress;
        public byte ResponderCount;             // Entities responding
    }

    /// <summary>
    /// Response to a distress signal.
    /// </summary>
    public struct DistressResponse : IComponentData
    {
        public Entity DistressSource;
        public float3 DistressPosition;
        public float ETA;                       // Estimated arrival ticks
        public uint ResponseStartTick;
        public byte IsResponding;
        public byte HasArrived;
    }

    /// <summary>
    /// Signal registry singleton.
    /// </summary>
    public struct SignalRegistry : IComponentData
    {
        public int ActiveSignalCount;
        public int ActiveBeaconCount;
        public int DistressCallCount;
        public int JammerCount;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in signal registry.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct SignalRegistryEntry : IBufferElementData
    {
        public Entity Entity;
        public SignalType Type;
        public float3 Position;
        public float Range;
        public float Strength;
        public uint EmittedTick;
        public byte IsActive;
    }
}


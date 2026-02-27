using Unity.Entities;

namespace Space4X.Runtime
{
    public enum Space4XMultiplayerRole : byte
    {
        Offline = 0,
        Host = 1,
        Client = 2,
        DedicatedServer = 3
    }

    public enum Space4XMultiplayerConnectionRequestKind : byte
    {
        Connect = 1,
        Disconnect = 2
    }

    public enum Space4XMultiplayerSessionEventKind : byte
    {
        Connect = 1,
        Disconnect = 2
    }

    public enum Space4XMultiplayerRejectReason : byte
    {
        None = 0,
        LocalRoleUnavailable = 1,
        ProtocolVersionMismatch = 2,
        ProtocolHashMismatch = 3,
        CapacityExceeded = 4,
        DuplicatePeer = 5,
        UnknownPeer = 6,
        InvalidRequest = 7
    }

    public struct Space4XMultiplayerKernelRootTag : IComponentData
    {
    }

    public struct Space4XMultiplayerKernelMeta : IComponentData
    {
        public const uint CurrentSchemaVersion = 1u;
        public const uint CurrentKernelVersion = 1u;

        public uint SchemaVersion;
        public uint KernelVersion;
    }

    public struct Space4XMultiplayerKernelConfig : IComponentData
    {
        public const uint DefaultProtocolVersion = 1u;
        public const uint DefaultProtocolHash32 = 0x4D503030u; // "MP00"

        public byte Enabled;
        public Space4XMultiplayerRole LocalRole;
        public ushort Reserved0;
        public uint ExpectedProtocolVersion;
        public uint ExpectedProtocolHash32;
        public uint MaxPeers;
        public uint MaxRequestsPerTick;
        public uint MaxRetainedEvents;

        public static Space4XMultiplayerKernelConfig Default => new Space4XMultiplayerKernelConfig
        {
            Enabled = 1,
            LocalRole = Space4XMultiplayerRole.Offline,
            Reserved0 = 0,
            ExpectedProtocolVersion = DefaultProtocolVersion,
            ExpectedProtocolHash32 = DefaultProtocolHash32,
            MaxPeers = 16u,
            MaxRequestsPerTick = 128u,
            MaxRetainedEvents = 2048u
        };
    }

    public struct Space4XMultiplayerKernelState : IComponentData
    {
        public uint SessionSerial;
        public uint LastTick;
        public uint ActivePeers;
        public uint AcceptedConnectionsTotal;
        public uint RejectedConnectionsTotal;
        public uint DisconnectsTotal;
        public uint LastEventSerial;
    }

    public struct Space4XMultiplayerKernelOverflow : IComponentData
    {
        public uint DroppedThisTick;
        public uint DroppedTotal;
        public uint LastOverflowTick;
    }

    public struct Space4XMultiplayerKernelGuard : IComponentData
    {
        public uint ProtocolVersionMismatchTotal;
        public uint ProtocolHashMismatchTotal;
        public uint CapacityRejectTotal;
        public uint DuplicatePeerRejectTotal;
        public uint UnknownPeerRejectTotal;
        public uint LocalRoleRejectTotal;
        public uint InvalidRequestTotal;
        public uint LastRejectTick;
    }

    [InternalBufferCapacity(16)]
    public struct Space4XMultiplayerConnectionRequest : IBufferElementData
    {
        public Space4XMultiplayerConnectionRequestKind Kind;
        public byte RequestedRole;
        public ushort Reserved0;
        public uint PeerId;
        public uint ProtocolVersion;
        public uint ProtocolHash32;
        public uint ContextFlags;
    }

    [InternalBufferCapacity(32)]
    public struct Space4XMultiplayerPeer : IBufferElementData
    {
        public uint PeerId;
        public byte Role;
        public byte Connected;
        public ushort Reserved0;
        public uint ConnectedTick;
        public uint LastSeenTick;
    }

    [InternalBufferCapacity(32)]
    public struct Space4XMultiplayerSessionEvent : IBufferElementData
    {
        public uint Serial;
        public uint Tick;
        public Space4XMultiplayerSessionEventKind Kind;
        public byte Accepted;
        public Space4XMultiplayerRejectReason RejectReason;
        public byte Reserved0;
        public uint PeerId;
        public uint ProtocolVersion;
        public uint ProtocolHash32;
        public uint ContextFlags;
    }
}

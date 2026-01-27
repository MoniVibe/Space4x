using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Power
{
    /// <summary>
    /// Type of power network node.
    /// </summary>
    public enum PowerNodeType : byte
    {
        Source,        // generator plant/module
        Substation,    // step-up/step-down station, repeater, router
        Consumer,      // heavy load building or ship module cluster
        Hub            // abstract aggregation node (district bus, ship bus)
    }

    /// <summary>
    /// Power network node component attached to generators/consumers/substations.
    /// </summary>
    public struct PowerNode : IComponentData
    {
        public int NodeId;
        public PowerNetworkRef Network;
        public PowerNodeType Type;
        public float3 WorldPosition;

        public float LocalLoss;        // transformer loss (0..1)
        public float Quality;          // 0..1 from manufacturer/materials
    }

    /// <summary>
    /// Power transmission edge (stored in network entity buffer).
    /// </summary>
    public struct PowerEdge : IBufferElementData
    {
        public int FromNodeId;
        public int ToNodeId;
        public float Length;           // meters / world units
        public float MaxThroughput;    // MW upper bound
        public float LossPerUnit;      // 0..1 loss per distance unit (before tech)
        public float Quality;          // 0..1
        public byte State;             // Online, Damaged, Offline
    }

    /// <summary>
    /// Infrastructure condition and wear state.
    /// </summary>
    public enum InfrastructureState : byte
    {
        Normal,
        Degraded,
        Faulty
    }

    /// <summary>
    /// Condition and wear tracking for power infrastructure.
    /// </summary>
    public struct InfrastructureCondition : IComponentData
    {
        public float Wear;          // 0..1
        public float FaultRisk;     // derived from Wear, Quality, Load
        public InfrastructureState State;
    }

    /// <summary>
    /// Manufacturer and quality information for infrastructure.
    /// </summary>
    public struct InfrastructureManufacturer : IComponentData
    {
        public int ManufacturerId;
        public byte QualityTier;
    }

    /// <summary>
    /// Precomputed routing information per consumer.
    /// </summary>
    public struct PowerRouteInfo : IComponentData
    {
        public int NetworkId;
        public int NodeId;
        public float PathLoss;      // aggregated from nodes/edges
        public float PathCapacity;  // min edge MaxThroughput along best path
    }
}


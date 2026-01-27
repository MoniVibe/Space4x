using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace PureDOTS.Runtime.Transport.Blobs
{
    /// <summary>
    /// Hyperway link definition blob asset.
    /// Defines a connection between two nodes in the network.
    /// </summary>
    public struct HyperwayLinkDef
    {
        public int LinkId;
        public int FromNodeId;
        public int ToNodeId;
        public float Distance; // in whatever "macro" units
        public float BaseTravelTicks; // baseline time over link
        public float MaxMass; // total carried mass per departure
        public float MaxVolume;
        public float FuelCostBase; // cost to node operator per departure
        public float MinPricePerMass; // default pricing hints
        public byte BiDirectional; // 0/1
        public byte TechTier;
    }

    /// <summary>
    /// Hyperway network definition blob asset.
    /// Contains all nodes and links for a hyperway network.
    /// </summary>
    public struct HyperwayNetworkDefBlob
    {
        public BlobArray<int> NodeIds; // list of node IDs in this network
        public BlobArray<HyperwayLinkDef> Links; // all links in this network
    }
}


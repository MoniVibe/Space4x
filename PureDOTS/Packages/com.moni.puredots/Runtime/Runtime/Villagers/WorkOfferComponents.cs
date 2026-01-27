using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Claimable work offer from resource nodes, jobsites, storehouses.
    /// No spaghetti - clean separation between offer creation and claim processing.
    /// </summary>
    public struct WorkOffer : IComponentData
    {
        public int JobId;               // Index into JobDefinitionCatalog
        public Entity Target;           // Resource node, jobsite, storehouse, etc.
        public byte Slots;              // Parallel workers allowed
        public byte Taken;              // Currently claimed count (atomically incremented)
        public uint Priority;           // Derived from need & job def
        public uint Seed;               // Per-offer deterministic seed for tie-breaking
        public byte RequiredLayerMask;  // Spatial layer mask required for this work
        public float EtaSlope;          // ETA penalty slope for utility calculation
        public uint ExpiresAtTick;      // Tick when offer expires (0 = never expires)
    }
    
    /// <summary>
    /// Claim made by a villager on a WorkOffer.
    /// </summary>
    public struct WorkClaim : IComponentData
    {
        public Entity Offer;            // The WorkOffer entity
        public uint ClaimTick;          // Tick when claim was made
    }
    
    /// <summary>
    /// Nonce component to prevent over-offer spam.
    /// Attached to entities that can emit offers (resource nodes, jobsites, etc.).
    /// </summary>
    public struct OfferNonce : IComponentData
    {
        public uint LastIssuedTick;
        
        /// <summary>
        /// Check if an offer can be issued based on cooldown.
        /// </summary>
        public static bool CanIssueOffer(uint now, ref OfferNonce nonce, uint cooldownTicks = 10)
        {
            if (now - nonce.LastIssuedTick < cooldownTicks)
            {
                return false;
            }
            nonce.LastIssuedTick = now;
            return true;
        }
    }
    
    /// <summary>
    /// Component tracking open offer count per source entity for back-pressure.
    /// Prevents offer spam by capping the number of concurrent offers per source.
    /// </summary>
    public struct OfferSourceCap : IComponentData
    {
        public byte MaxOpenOffers;      // Maximum concurrent offers allowed (e.g., 5 per resource node)
        public byte CurrentOpenOffers;  // Current count of open offers from this source
        public uint LastCapHitTick;     // Last tick when cap was hit (for logging/telemetry)
    }
    
    /// <summary>
    /// Per-villager seed for deterministic tie-breaking.
    /// </summary>
    public struct VillagerSeed : IComponentData
    {
        public uint Value;
        
        /// <summary>
        /// Deterministic tie-breaker using villager seed and tick.
        /// </summary>
        public static uint TieBreak(uint baseSeed, uint villagerId, uint tick)
        {
            var rnd = new Unity.Mathematics.Random(baseSeed ^ villagerId ^ tick);
            return rnd.NextUInt();
        }
    }
}


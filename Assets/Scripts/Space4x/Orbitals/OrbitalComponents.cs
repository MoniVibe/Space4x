using System.Runtime.InteropServices;
using Unity.Entities;

namespace Space4X.Orbitals
{
    /// <summary>
    /// Kind of orbital object (satellite, megastructure, etc.).
    /// </summary>
    public enum OrbitalKind : byte
    {
        Comet,
        Asteroid,
        IceChunk,
        Derelict,
        StrangeSatellite,
        Station,
        Megastructure,
        AncientRuins,
        AlienShrine,
        GateFragment,
        SuperResource,
        Scenic,
        HazardZone
    }

    /// <summary>
    /// Tag component marking an entity as an orbital object (satellite, megastructure, etc.).
    /// </summary>
    public struct OrbitalObjectTag : IComponentData { }

    /// <summary>
    /// State for orbital objects (satellites, megastructures, etc.).
    /// </summary>
    public struct OrbitalObjectState : IComponentData
    {
        /// <summary>Kind of orbital object.</summary>
        public OrbitalKind Kind;
        
        /// <summary>True if orbital is hidden until discovered.</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool Hidden;
        
        /// <summary>True if ships can dock with this orbital.</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool CanDock;
        
        /// <summary>True if orbital offers missions or interactions.</summary>
        [MarshalAs(UnmanagedType.U1)]
        public bool OffersMission;
    }
}

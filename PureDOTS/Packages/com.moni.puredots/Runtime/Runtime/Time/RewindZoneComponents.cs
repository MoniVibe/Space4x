using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Defines a spatial zone ("bubble") for local rewinds.
    /// Entities within the radius of this zone participate in spatial rewind for the specified track.
    /// </summary>
    public struct RewindZone : IComponentData
    {
        public RewindTrackId Track;
        public float3 Center;
        public float Radius;
    }

    /// <summary>
    /// Component marking which rewind track an entity participates in, and optionally which zone.
    /// If Zone is Entity.Null, the entity participates in global rewind for the track.
    /// If Zone points to a RewindZone entity, the entity only participates when within that zone's radius.
    /// </summary>
    public struct RewindScope : IComponentData
    {
        public RewindTrackId Track;
        public Entity Zone;
    }
}


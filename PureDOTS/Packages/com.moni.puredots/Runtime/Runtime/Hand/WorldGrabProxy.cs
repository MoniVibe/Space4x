using Unity.Entities;

namespace PureDOTS.Runtime.Hand
{
    /// <summary>
    /// Links a selection proxy to a world-grab target entity.
    /// </summary>
    public struct WorldGrabProxy : IComponentData
    {
        public Entity Target;
    }
}

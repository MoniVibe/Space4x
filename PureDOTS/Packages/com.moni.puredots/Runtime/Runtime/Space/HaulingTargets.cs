using Unity.Entities;

namespace PureDOTS.Runtime.Space
{
    public struct HaulingTarget : IComponentData
    {
        public Entity SourcePile;
        public Entity DestinationEntity;
    }
}

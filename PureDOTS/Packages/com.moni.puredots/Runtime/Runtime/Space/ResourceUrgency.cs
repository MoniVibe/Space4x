using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Space
{
    public struct ResourceUrgency : IComponentData
    {
        public FixedString64Bytes ResourceTypeId;
        public float UrgencyWeight;
    }
}

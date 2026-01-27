using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    public struct HeadlessTpsCap : IComponentData
    {
        public float TargetTps;
    }
}

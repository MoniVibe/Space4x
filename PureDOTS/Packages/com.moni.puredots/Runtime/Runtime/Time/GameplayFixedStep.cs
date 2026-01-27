using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    public struct GameplayFixedStep : IComponentData
    {
        public float FixedDeltaTime;
    }
}

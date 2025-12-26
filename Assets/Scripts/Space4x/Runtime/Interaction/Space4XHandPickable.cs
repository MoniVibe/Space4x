using Unity.Entities;

namespace Space4X.Runtime.Interaction
{
    public struct Space4XHandPickable : IComponentData
    {
        public float MaxMass;
        public float ThrowSpeedMultiplier;
        public float SlingshotSpeedMultiplier;
    }
}

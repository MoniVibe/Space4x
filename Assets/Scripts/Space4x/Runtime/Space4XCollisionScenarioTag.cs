using Unity.Entities;

namespace Space4X.Runtime
{
    /// <summary>
    /// Marks the collision micro scenario so runtime systems can avoid mining overrides.
    /// </summary>
    public struct Space4XCollisionScenarioTag : IComponentData
    {
    }
}

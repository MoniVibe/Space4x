using Unity.Entities;

namespace PureDOTS.Input
{
    /// <summary>
    /// Optional MonoBehaviour bridge that exposes an ECS Entity for hit resolution.
    /// Implement on colliders to let selection/order raycasts resolve to ECS entities.
    /// </summary>
    public interface IEntityBridge
    {
        bool TryGetEntity(out Entity entity);
    }
}

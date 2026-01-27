using PureDOTS.Runtime.Interaction;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component that marks an entity as pickable by the player/god hand.
    /// Add this to any entity that should be able to be picked up and thrown.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PickableAuthoring : MonoBehaviour
    {
        // No configuration needed - just a tag component
    }

    /// <summary>
    /// Baker for PickableAuthoring - adds Pickable component to the entity.
    /// </summary>
    public sealed class PickableAuthoringBaker : Baker<PickableAuthoring>
    {
        public override void Bake(PickableAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Pickable>(entity);
            AddComponent<HeldByPlayer>(entity);
            SetComponentEnabled<HeldByPlayer>(entity, false);
            AddComponent<MovementSuppressed>(entity);
            SetComponentEnabled<MovementSuppressed>(entity, false);
            AddComponent<BeingThrown>(entity);
            SetComponentEnabled<BeingThrown>(entity, false);
        }
    }
}

























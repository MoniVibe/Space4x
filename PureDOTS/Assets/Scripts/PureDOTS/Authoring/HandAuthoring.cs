using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class HandAuthoring : MonoBehaviour { }

    public sealed class HandBaker : Baker<HandAuthoring>
    {
        public override void Bake(HandAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent<HandSingletonTag>(entity);
            AddComponent(entity, new HandState());
            AddBuffer<HandCommand>(entity);
        }
    }
}





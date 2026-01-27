using PureDOTS.Runtime.Combat;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Combat
{
    public sealed class ProjectilePoolAuthoring : MonoBehaviour
    {
        public GameObject ProjectilePrefab;
        public int Capacity = 256;
    }

#if UNITY_EDITOR
    public sealed class ProjectilePoolBaker : Baker<ProjectilePoolAuthoring>
    {
        public override void Bake(ProjectilePoolAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var prefabEntity = authoring.ProjectilePrefab != null
                ? GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic)
                : Entity.Null;

            AddComponent(entity, new ProjectilePoolConfig
            {
                Prefab = prefabEntity,
                Capacity = math.max(0, authoring.Capacity)
            });

            AddComponent(entity, new ProjectilePoolState());
            AddBuffer<ProjectilePoolEntry>(entity);
        }
    }
#endif
}

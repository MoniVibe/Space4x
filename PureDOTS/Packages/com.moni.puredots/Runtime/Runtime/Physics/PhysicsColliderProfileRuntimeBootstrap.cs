using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Runtime.Physics
{
    [DisallowMultipleComponent]
    public class PhysicsColliderProfileRuntimeBootstrap : MonoBehaviour
    {
        public PhysicsColliderProfileDefinition ProfileDefinition;

        private Entity _profileEntity = Entity.Null;
        private BlobAssetReference<PhysicsColliderProfileBlob> _profileBlob;

        private void Awake()
        {
            if (ProfileDefinition == null)
            {
                Debug.LogError("[PhysicsColliderProfileRuntimeBootstrap] ProfileDefinition is missing.");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("[PhysicsColliderProfileRuntimeBootstrap] Default world is not ready.");
                return;
            }

            var entityManager = world.EntityManager;
            var profileQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsColliderProfileComponent>());
            if (profileQuery.CalculateEntityCount() > 0)
            {
                profileQuery.Dispose();
                return;
            }
            profileQuery.Dispose();

            var input = ProfileDefinition.ToBuildInput();
            if (!PhysicsColliderProfileBuilder.TryBuild(input, Allocator.Persistent, out var blobRef))
            {
                Debug.LogWarning("[PhysicsColliderProfileRuntimeBootstrap] ProfileDefinition has no entries; skipping.");
                return;
            }

            _profileBlob = blobRef;
            _profileEntity = entityManager.CreateEntity(typeof(PhysicsColliderProfileComponent));
            entityManager.SetComponentData(_profileEntity, new PhysicsColliderProfileComponent
            {
                Profile = _profileBlob
            });
        }

        private void OnDestroy()
        {
            if (_profileBlob.IsCreated)
            {
                _profileBlob.Dispose();
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            if (_profileEntity != Entity.Null && entityManager.Exists(_profileEntity))
            {
                entityManager.DestroyEntity(_profileEntity);
            }
        }
    }
}

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Space4X.Registry;

// Use the aliases from bootstrap system
using ResourceTypeId = Space4X.Registry.ResourceTypeId;
using MiningOrder = Space4X.Registry.MiningOrder;

namespace Space4X.Demo
{
    /// <summary>
    /// Simple on-screen HUD to visualize mining state without digging through logs.
    /// Shows asteroid count, miner count, and basic state information.
    /// </summary>
    public class Space4XMiningHud : MonoBehaviour
    {
        private World _world;
        private EntityManager _em;
        private EntityQuery _asteroidQuery;
        private EntityQuery _minerQuery;
        private EntityQuery _carrierQuery;

        void Start()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                Debug.LogWarning("[Space4XMiningHud] World not found, HUD will not display.");
                return;
            }

            _em = _world.EntityManager;

            _asteroidQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<ResourceTypeId>(),
                ComponentType.ReadOnly<LocalTransform>());

            _minerQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<MiningOrder>(),
                ComponentType.ReadOnly<LocalTransform>());

            _carrierQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<Space4XFleet>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        void OnGUI()
        {
            if (_world == null || !_world.IsCreated || _em == null)
            {
                return;
            }

            int asteroids = _asteroidQuery.CalculateEntityCount();
            int miners = _minerQuery.CalculateEntityCount();
            int carriers = _carrierQuery.CalculateEntityCount();

            float yPos = 10f;
            float lineHeight = 20f;

            GUI.Label(new Rect(10, yPos, 400, lineHeight), $"Asteroids: {asteroids}");
            yPos += lineHeight;

            GUI.Label(new Rect(10, yPos, 400, lineHeight), $"Miners: {miners}");
            yPos += lineHeight;

            GUI.Label(new Rect(10, yPos, 400, lineHeight), $"Carriers: {carriers}");
            yPos += lineHeight;

            // Show first miner position and state
            using var minerEntities = _minerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (minerEntities.Length > 0)
            {
                var minerEntity = minerEntities[0];
                if (_em.HasComponent<LocalTransform>(minerEntity))
                {
                    var xf = _em.GetComponentData<LocalTransform>(minerEntity);
                    GUI.Label(new Rect(10, yPos, 400, lineHeight), $"Miner[0] pos: ({xf.Position.x:F1}, {xf.Position.y:F1}, {xf.Position.z:F1})");
                    yPos += lineHeight;
                }

                // Show miner cargo if available
                if (_em.HasComponent<MiningVessel>(minerEntity))
                {
                    var vessel = _em.GetComponentData<MiningVessel>(minerEntity);
                    GUI.Label(new Rect(10, yPos, 400, lineHeight), $"Miner[0] cargo: {vessel.CurrentCargo:F1} / {vessel.CargoCapacity:F1}");
                    yPos += lineHeight;
                }

                // Show mining state if available
                if (_em.HasComponent<MiningState>(minerEntity))
                {
                    var miningState = _em.GetComponentData<MiningState>(minerEntity);
                    GUI.Label(new Rect(10, yPos, 400, lineHeight), $"Miner[0] phase: {miningState.Phase}");
                    yPos += lineHeight;
                }
            }

            // Show first asteroid resource amount if available
            using var asteroidEntities = _asteroidQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (asteroidEntities.Length > 0)
            {
                var asteroidEntity = asteroidEntities[0];
                if (_em.HasComponent<Asteroid>(asteroidEntity))
                {
                    var asteroid = _em.GetComponentData<Asteroid>(asteroidEntity);
                    GUI.Label(new Rect(10, yPos, 400, lineHeight), $"Asteroid[0] resources: {asteroid.ResourceAmount:F1} / {asteroid.MaxResourceAmount:F1}");
                    yPos += lineHeight;
                }
            }

            // Show first carrier storage if available
            using var carrierEntities = _carrierQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (carrierEntities.Length > 0)
            {
                var carrierEntity = carrierEntities[0];
                if (_em.HasBuffer<ResourceStorage>(carrierEntity))
                {
                    var storageBuffer = _em.GetBuffer<ResourceStorage>(carrierEntity);
                    float totalStored = 0f;
                    float totalCapacity = 0f;
                    foreach (var storage in storageBuffer)
                    {
                        totalStored += storage.Amount;
                        totalCapacity += storage.Capacity;
                    }
                    GUI.Label(new Rect(10, yPos, 400, lineHeight), $"Carrier[0] storage: {totalStored:F1} / {totalCapacity:F1}");
                }
            }
        }
    }
}


using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Space4X.Registry;
using UnityEngine;

namespace Space4X.Demo
{
    /// <summary>
    /// Debug system to verify that Space4X mining entities exist with correct components.
    /// Logs entity positions and counts once, then disables itself.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct DebugMiningEntitiesSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = true;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Run only once to avoid per-frame spam
            state.Enabled = false;

            int asteroidCount = 0;
            int carrierCount = 0;
            int minerCount = 0;

            foreach (var (xf, rt) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<ResourceTypeId>>())
            {
                asteroidCount++;
                Debug.Log($"[DebugMiningEntities] Asteroid at {xf.ValueRO.Position}");
            }

            foreach (var (xf, fleet) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<Space4XFleet>>())
            {
                carrierCount++;
                Debug.Log($"[DebugMiningEntities] Carrier at {xf.ValueRO.Position}");
            }

            foreach (var (xf, order) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<MiningOrder>>())
            {
                minerCount++;
                Debug.Log($"[DebugMiningEntities] Miner at {xf.ValueRO.Position}");
            }

            Debug.Log($"[DebugMiningEntities] Asteroids={asteroidCount}, Carriers={carrierCount}, Miners={minerCount}");
        }
    }
}


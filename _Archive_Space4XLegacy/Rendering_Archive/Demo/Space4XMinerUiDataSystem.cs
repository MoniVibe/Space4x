using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Space4X.Registry;

namespace Space4X.Demo
{
    /// <summary>
    /// Lightweight UI data component for HUD rendering.
    /// Stores cargo fullness ratio (0-1) for efficient HP bar rendering.
    /// </summary>
    public struct MinerUiData : IComponentData
    {
        public float Fullness01; // 0-1 cargo fill ratio
    }

    /// <summary>
    /// Updates MinerUiData component with cargo fullness for efficient HUD rendering.
    /// Burst-compiled ECS system that runs once per frame, much cheaper than MonoBehaviour queries.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XMinerUiDataSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiningVessel>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new Unity.Entities.EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Update UI data for all miners
            foreach (var (vessel, entity) in SystemAPI.Query<RefRO<MiningVessel>>().WithEntityAccess())
            {
                var vesselData = vessel.ValueRO;
                var fullness = vesselData.CargoCapacity > 0f
                    ? math.saturate(vesselData.CurrentCargo / vesselData.CargoCapacity)
                    : 0f;

                // Add MinerUiData if missing, otherwise update it directly
                if (SystemAPI.HasComponent<MinerUiData>(entity))
                {
                    var uiData = SystemAPI.GetComponentRW<MinerUiData>(entity);
                    uiData.ValueRW.Fullness01 = fullness;
                }
                else
                {
                    ecb.AddComponent(entity, new MinerUiData { Fullness01 = fullness });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


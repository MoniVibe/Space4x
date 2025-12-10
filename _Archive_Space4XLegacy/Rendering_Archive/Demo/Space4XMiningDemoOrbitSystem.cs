using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Space4X.Registry;

namespace Space4X.Demo
{
    /// <summary>
    /// Simple orbiting system for demo mining entities if the full mining loop isn't working.
    /// Moves mining vessels in a circle around resource nodes for visible gameplay.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class Space4XMiningDemoOrbitSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var time = (float)SystemAPI.Time.ElapsedTime;

            // Simple orbiting behavior for demo mining vessels
            foreach (var (transform, miningVessel) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<MiningVessel>>())
            {
                // Orbit around the origin with some offset based on vessel ID hash
                var hash = (uint)miningVessel.ValueRO.VesselId.GetHashCode();
                var radius = 20f + (hash % 20); // 20-40 unit radius
                var speed = 0.5f + (hash % 10) * 0.1f; // 0.5-1.5 speed
                var offset = hash % 360; // Phase offset

                var angle = time * speed + offset;
                var x = math.cos(angle) * radius;
                var z = math.sin(angle) * radius;
                var y = math.sin(angle * 0.5f) * 5f; // Some vertical variation

                transform.ValueRW.Position = new float3(x, y, z);
            }
        }
    }
}










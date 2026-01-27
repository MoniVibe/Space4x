// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Morphing
{
    [BurstCompile]
    public partial struct MorphingStubSystem : ISystem
    {
        [BurstCompile] public void OnCreate(ref SystemState state) { }
        [BurstCompile] public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var morph in SystemAPI.Query<RefRW<TerrainMorphState>>())
            {
                var value = morph.ValueRW;
                value.Deformation = math.max(0f, value.Deformation - dt * value.RecoveryRate);
                morph.ValueRW = value;
            }

            foreach (var breakable in SystemAPI.Query<RefRW<BreakableSurface>>())
            {
                var value = breakable.ValueRW;
                value.Integrity = math.clamp(value.Integrity, 0f, value.MaxIntegrity);
                breakable.ValueRW = value;
            }

            foreach (var burn in SystemAPI.Query<RefRW<BurnState>>())
            {
                var value = burn.ValueRW;
                value.Intensity = math.max(0f, value.Intensity - dt * value.ExtinguishRate);
                burn.ValueRW = value;
            }
        }
    }
}

// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Communication
{
    [BurstCompile]
    public partial struct CommunicationReliabilitySystem : ISystem
    {
        [BurstCompile] public void OnCreate(ref SystemState state) { }
        [BurstCompile] public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (channel, disruption) in SystemAPI.Query<RefRW<CommChannel>, RefRO<CommDisruption>>())
            {
                var c = channel.ValueRW;
                var d = disruption.ValueRO;
                float target = math.saturate(1f - d.Severity);
                c.Reliability = math.lerp(c.Reliability, target, deltaTime * math.max(0.1f, d.RecoveryRate));
                channel.ValueRW = c;
            }
        }
    }
}

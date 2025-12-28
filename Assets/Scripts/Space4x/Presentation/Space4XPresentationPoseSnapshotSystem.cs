using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]
    public partial struct Space4XPresentationPoseSnapshotSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var tick = timeState.Tick;
            foreach (var (transform, snapshot) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<SimPoseSnapshot>>())
            {
                var value = snapshot.ValueRO;
                value.PrevPosition = value.CurrPosition;
                value.PrevRotation = value.CurrRotation;
                value.PrevScale = value.CurrScale;
                value.PrevTick = value.CurrTick;

                value.CurrPosition = transform.ValueRO.Position;
                value.CurrRotation = transform.ValueRO.Rotation;
                value.CurrScale = transform.ValueRO.Scale;
                value.CurrTick = tick;

                snapshot.ValueRW = value;
            }
        }
    }
}

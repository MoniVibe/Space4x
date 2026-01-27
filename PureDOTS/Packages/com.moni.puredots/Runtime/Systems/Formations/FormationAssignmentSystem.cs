using PureDOTS.Runtime.Formations;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Formations
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FormationAssignmentSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FormationConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (config, leaderTransform, entity) in SystemAPI
                         .Query<RefRO<FormationConfig>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (!state.EntityManager.HasBuffer<FormationSlot>(entity))
                {
                    continue;
                }
                var slots = state.EntityManager.GetBuffer<FormationSlot>(entity);
                var forward = leaderTransform.ValueRO.Forward();
                var right = leaderTransform.ValueRO.Right();
                for (int i = 0; i < slots.Length; i++)
                {
                    var offset = ComputeOffset(config.ValueRO, i, forward, right);
                    var existingSlot = slots[i];
                    slots[i] = new FormationSlot
                    {
                        LocalOffset = offset,
                        AssignedEntity = existingSlot.AssignedEntity
                    };
                }
            }
        }

        private float3 ComputeOffset(FormationConfig config, int index, float3 forward, float3 right)
        {
            switch (config.Type)
            {
                case FormationType.Line:
                    return right * ((index - config.SlotCount / 2f) * config.SlotSpacing);
                case FormationType.Wedge:
                    return forward * (index * config.SlotSpacing) + right * ((index % 2 == 0 ? 1 : -1) * config.SlotSpacing);
                case FormationType.Sphere:
                    return forward * (index * config.SlotSpacing * 0.2f);
                case FormationType.Column:
                    return forward * (index * config.SlotSpacing);
                default:
                    return float3.zero;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

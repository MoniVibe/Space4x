using PureDOTS.Runtime.Miracles;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Miracles
{
    /// <summary>
    /// Copies targeting solutions into lightweight preview data that presentation layers can render.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct MiraclePreviewSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiracleTargetSolution>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (solution, previewRef) in SystemAPI.Query<RefRO<MiracleTargetSolution>, RefRW<MiraclePreviewData>>())
            {
                ref readonly var solutionValue = ref solution.ValueRO;
                ref var preview = ref previewRef.ValueRW;

                preview.Position = solutionValue.TargetPoint;
                preview.Radius = solutionValue.Radius;
                preview.IsValid = solutionValue.IsValid;
                preview.ValidityReason = solutionValue.ValidityReason;
                preview.SelectedMiracleId = solutionValue.SelectedMiracleId;
            }
        }
    }
}




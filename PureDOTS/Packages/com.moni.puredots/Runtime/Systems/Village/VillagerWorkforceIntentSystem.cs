using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Village;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Applies workforce intents when villagers are idle or their job becomes irrelevant.
    /// </summary>
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillageWorkforceDecisionSystem))]
    public partial struct VillagerWorkforceIntentSystem : ISystem
    {

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerWorkforceIntent>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var tick = timeState.Tick;

            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton))
            {
                return;
            }

            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (intent, job, entity) in SystemAPI
                         .Query<RefRO<VillagerWorkforceIntent>, RefRW<VillagerJob>>()
                         .WithEntityAccess())
            {
                if (!CanChangeJob(job.ValueRO))
                {
                    continue;
                }

                var newJob = job.ValueRO;
                if (newJob.Type != intent.ValueRO.DesiredJob)
                {
                    newJob.Type = intent.ValueRO.DesiredJob;
                    newJob.Phase = VillagerJob.JobPhase.Assigned;
                    newJob.LastStateChangeTick = tick;
                    newJob.Productivity = math.max(newJob.Productivity, intent.ValueRO.DesireWeight);
                    SystemAPI.SetComponent(entity, newJob);
                }

                ecb.RemoveComponent<VillagerWorkforceIntent>(entity);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool CanChangeJob(in VillagerJob job)
        {
            return job.Phase == VillagerJob.JobPhase.Idle
                || job.Phase == VillagerJob.JobPhase.Completed
                || job.Phase == VillagerJob.JobPhase.Interrupted;
        }
    }
}

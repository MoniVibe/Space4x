using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Presentation
{
    /// <summary>
    /// Optional graybox presentation bridge for villager job effects.
    /// Emits PlayEffectRequest when VillagerJobState.Phase==Act.
    /// All spawns via Begin/End Presentation ECB - no structural changes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(BeginPresentationECBSystem))]
    public partial struct VillagerJobPresentationBridgeSystem : ISystem
    {
        private EntityCommandBuffer.ParallelWriter _ecb;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }
            
            var ecbSingleton = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>();
            _ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            var job = new EmitJobEffectsJob
            {
                Ecb = _ecb
            };
            
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
        
        [BurstCompile]
        public partial struct EmitJobEffectsJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            
            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in VillagerJob job)
            {
                // Emit effect when villager is actively working
                if (job.Phase == VillagerJob.JobPhase.Gathering)
                {
                    // Queue effect request (placeholder - actual PlayEffectRequest component would be defined elsewhere)
                    // Ecb.AddComponent(chunkIndex, entity, new PlayEffectRequest { EffectId = "FX.Job.Act" });
                }
            }
        }
    }
}


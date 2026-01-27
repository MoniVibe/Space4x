using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Interface for modular villager job behaviors.
    /// Each job type (gather, build, craft, combat) implements this interface
    /// to provide consistent execution pattern.
    /// Note: Interfaces cannot be Burst-compiled; implementations should be marked with [BurstCompile].
    /// </summary>
    public interface IVillagerJobBehavior
    {
        /// <summary>
        /// Checks if the villager can start this job.
        /// </summary>
        bool CanStart(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state);
        
        /// <summary>
        /// Starts the job execution.
        /// </summary>
        void StartJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state);
        
        /// <summary>
        /// Updates job execution for one tick.
        /// Returns true if job is complete.
        /// </summary>
        bool UpdateJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state);
        
        /// <summary>
        /// Completes the job and applies rewards/effects.
        /// </summary>
        void CompleteJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state);
        
        /// <summary>
        /// Cancels/interrupts the job.
        /// </summary>
        void CancelJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state);
    }
    
    /// <summary>
    /// STUB: Placeholder job behavior implementations.
    /// Full implementations will be created for each job type (gather, build, craft, combat).
    /// </summary>
    [BurstCompile]
    public struct GatherJobBehavior : IVillagerJobBehavior
    {
        public bool CanStart(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state)
        {
            // TODO: Check if villager has capacity, resource exists, etc.
            return false;
        }
        
        public void StartJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state)
        {
            // TODO: Set villager state to gathering, target resource
        }
        
        public bool UpdateJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state)
        {
            // TODO: Progress gathering, check completion
            return false;
        }
        
        public void CompleteJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state)
        {
            // TODO: Add resources to inventory, update job ticket
        }
        
        public void CancelJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state)
        {
            // TODO: Reset villager state, release job ticket
        }
    }
    
    /// <summary>
    /// STUB: Build job behavior placeholder.
    /// </summary>
    [BurstCompile]
    public struct BuildJobBehavior : IVillagerJobBehavior
    {
        public bool CanStart(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) => false;
        public void StartJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) { }
        public bool UpdateJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) => false;
        public void CompleteJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) { }
        public void CancelJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) { }
    }
    
    /// <summary>
    /// STUB: Craft job behavior placeholder.
    /// </summary>
    [BurstCompile]
    public struct CraftJobBehavior : IVillagerJobBehavior
    {
        public bool CanStart(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) => false;
        public void StartJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) { }
        public bool UpdateJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) => false;
        public void CompleteJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) { }
        public void CancelJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) { }
    }
    
    /// <summary>
    /// STUB: Combat job behavior placeholder.
    /// </summary>
    [BurstCompile]
    public struct CombatJobBehavior : IVillagerJobBehavior
    {
        public bool CanStart(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) => false;
        public void StartJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) { }
        public bool UpdateJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) => false;
        public void CompleteJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) { }
        public void CancelJob(Entity villagerEntity, Entity jobTargetEntity, ref SystemState state) { }
    }
}


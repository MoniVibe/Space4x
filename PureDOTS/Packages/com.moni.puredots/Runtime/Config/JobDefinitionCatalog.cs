using System;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Config
{
    /// <summary>
    /// Runtime blob data for job definition.
    /// Contains job duration, resource costs, rewards, and skill requirements.
    /// </summary>
    public struct JobDefinitionData
    {
        public FixedString64Bytes JobName;
        public byte JobTypeIndex; // Maps to VillagerJob.JobType enum
        
        // Duration and timing
        public float BaseDurationSeconds;
        public float MinDurationSeconds;
        public float MaxDurationSeconds;
        
        // Resource costs (per job execution)
        public BlobArray<JobResourceCost> ResourceCosts;
        
        // Rewards (per job execution)
        public BlobArray<JobResourceReward> ResourceRewards;
        
        // Skill requirements (0-100, 0 = no requirement)
        public byte RequiredPhysique;
        public byte RequiredFinesse;
        public byte RequiredWillpower;
        
        // Productivity multipliers
        public float SkillMultiplier; // How much skill affects productivity
        public float NeedsMultiplier; // How much needs affect productivity
        
        // Priority and scheduling
        public byte BasePriority; // 0-100, higher = more urgent
        public float CooldownSeconds; // Minimum time between job executions
    }
    
    /// <summary>
    /// Resource cost for a job execution.
    /// </summary>
    public struct JobResourceCost
    {
        public ushort ResourceTypeIndex;
        public float Amount;
    }
    
    /// <summary>
    /// Resource reward for a job execution.
    /// </summary>
    public struct JobResourceReward
    {
        public ushort ResourceTypeIndex;
        public float BaseAmount;
        public float SkillBonusMultiplier; // Additional amount per skill point above requirement
    }
    
    /// <summary>
    /// Blob asset containing all job definitions.
    /// </summary>
    public struct JobDefinitionCatalogBlob
    {
        public BlobArray<JobDefinitionData> Jobs;
        
        public int FindJobIndex(byte jobTypeIndex)
        {
            for (int i = 0; i < Jobs.Length; i++)
            {
                ref var jobData = ref Jobs[i];
                if (jobData.JobTypeIndex == jobTypeIndex)
                {
                    return i;
                }
            }
            return -1;
        }
        
        public bool TryGetJobIndex(byte jobTypeIndex, out int jobIndex)
        {
            jobIndex = FindJobIndex(jobTypeIndex);
            return jobIndex >= 0;
        }
        
        public ref JobDefinitionData GetJob(int jobIndex) => ref Jobs[jobIndex];
    }
}

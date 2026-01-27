#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// ScriptableObject asset for defining job definitions.
    /// Each job defines duration, resource costs/rewards, skill requirements, and productivity multipliers.
    /// </summary>
    [CreateAssetMenu(fileName = "JobDefinitionCatalog", menuName = "PureDOTS/Job Definition Catalog")]
    public class JobDefinitionCatalog : ScriptableObject
    {
        [Serializable]
        public class ResourceCost
        {
            public ushort resourceTypeIndex;
            [Range(0f, 1000f)] public float amount = 0f;
        }
        
        [Serializable]
        public class ResourceReward
        {
            public ushort resourceTypeIndex;
            [Range(0f, 1000f)] public float baseAmount = 0f;
            [Range(0f, 2f)] public float skillBonusMultiplier = 0.1f; // Additional per skill point above requirement
        }
        
        [Serializable]
        public class JobDefinition
        {
            [Header("Identity")]
            public string jobName = "New Job";
            public VillagerJob.JobType jobType = VillagerJob.JobType.Gatherer;
            
            [Header("Duration (seconds)")]
            [Range(0.1f, 300f)] public float baseDurationSeconds = 10f;
            [Range(0.1f, 300f)] public float minDurationSeconds = 5f;
            [Range(0.1f, 600f)] public float maxDurationSeconds = 30f;
            
            [Header("Resource Costs")]
            public List<ResourceCost> resourceCosts = new List<ResourceCost>();
            
            [Header("Resource Rewards")]
            public List<ResourceReward> resourceRewards = new List<ResourceReward>();
            
            [Header("Skill Requirements (0-100)")]
            [Range(0, 100)] public int requiredPhysique = 0;
            [Range(0, 100)] public int requiredFinesse = 0;
            [Range(0, 100)] public int requiredWillpower = 0;
            
            [Header("Productivity Multipliers")]
            [Range(0f, 2f)] public float skillMultiplier = 1f; // How much skill affects productivity
            [Range(0f, 2f)] public float needsMultiplier = 1f; // How much needs affect productivity
            
            [Header("Priority & Scheduling")]
            [Range(0, 100)] public int basePriority = 50;
            [Range(0f, 300f)] public float cooldownSeconds = 0f;
        }
        
        [Header("Job Definitions")]
        public List<JobDefinition> jobs = new List<JobDefinition>();
        
        private void OnValidate()
        {
            // Ensure job types are unique
            var typeSet = new HashSet<VillagerJob.JobType>();
            foreach (var job in jobs)
            {
                if (string.IsNullOrEmpty(job.jobName))
                {
                    job.jobName = job.jobType.ToString();
                }
                
                if (typeSet.Contains(job.jobType))
                {
                    Debug.LogWarning($"Duplicate job type: {job.jobType}");
                }
                typeSet.Add(job.jobType);
                
                // Validate duration ranges
                if (job.minDurationSeconds > job.baseDurationSeconds)
                {
                    job.minDurationSeconds = job.baseDurationSeconds;
                }
                if (job.maxDurationSeconds < job.baseDurationSeconds)
                {
                    job.maxDurationSeconds = job.baseDurationSeconds;
                }
            }
        }
    }
}
#endif


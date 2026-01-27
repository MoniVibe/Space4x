#if UNITY_EDITOR
using System.Collections.Generic;
using PureDOTS.Config;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Villagers;

namespace PureDOTS.Authoring
{
    public class JobDefinitionCatalogAuthoring : MonoBehaviour
    {
        [Tooltip("Optional ScriptableObject asset to source job definitions from.")]
        public JobDefinitionCatalog catalogAsset;

        [Tooltip("Inline job definitions (used if no asset provided).")]
        public List<JobDefinitionCatalog.JobDefinition> inlineJobs = new();

        public IReadOnlyList<JobDefinitionCatalog.JobDefinition> GetDefinitions()
        {
            if (catalogAsset != null && catalogAsset.jobs != null && catalogAsset.jobs.Count > 0)
            {
                return catalogAsset.jobs;
            }

            return inlineJobs;
        }
    }

    /// <summary>
    /// Baker for JobDefinitionCatalog data.
    /// Converts job definitions into runtime blob asset.
    /// </summary>
    public class JobDefinitionCatalogBaker : Baker<JobDefinitionCatalogAuthoring>
    {
        public override void Bake(JobDefinitionCatalogAuthoring authoring)
        {
            var definitions = authoring.GetDefinitions();
            var entity = GetEntity(TransformUsageFlags.None);
            
            if (definitions == null || definitions.Count == 0)
            {
                // Create empty catalog
                var builder = new BlobBuilder(Allocator.Temp);
                ref var catalog = ref builder.ConstructRoot<JobDefinitionCatalogBlob>();
                builder.Allocate(ref catalog.Jobs, 0);
                var blobAsset = builder.CreateBlobAssetReference<JobDefinitionCatalogBlob>(Allocator.Persistent);
                builder.Dispose();
                
                AddBlobAsset(ref blobAsset, out _);
                AddComponent(entity, new JobDefinitionCatalogComponent { Catalog = blobAsset });
                return;
            }

            // Build blob asset from job definitions
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var catalogBlob = ref blobBuilder.ConstructRoot<JobDefinitionCatalogBlob>();
            var jobsArray = blobBuilder.Allocate(ref catalogBlob.Jobs, definitions.Count);
            
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i];
                
                // Allocate resource costs
                var costsArray = blobBuilder.Allocate(ref jobsArray[i].ResourceCosts, def.resourceCosts.Count);
                for (int j = 0; j < def.resourceCosts.Count; j++)
                {
                    costsArray[j] = new JobResourceCost
                    {
                        ResourceTypeIndex = def.resourceCosts[j].resourceTypeIndex,
                        Amount = math.max(0f, def.resourceCosts[j].amount)
                    };
                }
                
                // Allocate resource rewards
                var rewardsArray = blobBuilder.Allocate(ref jobsArray[i].ResourceRewards, def.resourceRewards.Count);
                for (int j = 0; j < def.resourceRewards.Count; j++)
                {
                    rewardsArray[j] = new JobResourceReward
                    {
                        ResourceTypeIndex = def.resourceRewards[j].resourceTypeIndex,
                        BaseAmount = math.max(0f, def.resourceRewards[j].baseAmount),
                        SkillBonusMultiplier = math.max(0f, def.resourceRewards[j].skillBonusMultiplier)
                    };
                }
                
                jobsArray[i] = new JobDefinitionData
                {
                    JobName = new FixedString64Bytes(def.jobName),
                    JobTypeIndex = (byte)def.jobType,
                    BaseDurationSeconds = math.max(0.1f, def.baseDurationSeconds),
                    MinDurationSeconds = math.max(0.1f, def.minDurationSeconds),
                    MaxDurationSeconds = math.max(def.baseDurationSeconds, def.maxDurationSeconds),
                    RequiredPhysique = (byte)math.clamp(def.requiredPhysique, 0, 100),
                    RequiredFinesse = (byte)math.clamp(def.requiredFinesse, 0, 100),
                    RequiredWillpower = (byte)math.clamp(def.requiredWillpower, 0, 100),
                    SkillMultiplier = math.clamp(def.skillMultiplier, 0f, 2f),
                    NeedsMultiplier = math.clamp(def.needsMultiplier, 0f, 2f),
                    BasePriority = (byte)math.clamp(def.basePriority, 0, 100),
                    CooldownSeconds = math.max(0f, def.cooldownSeconds)
                };
            }
            
            var catalogBlobAsset = blobBuilder.CreateBlobAssetReference<JobDefinitionCatalogBlob>(Allocator.Persistent);
            blobBuilder.Dispose();
            
            AddBlobAsset(ref catalogBlobAsset, out _);
            AddComponent(entity, new JobDefinitionCatalogComponent { Catalog = catalogBlobAsset });
        }
    }
}
#endif


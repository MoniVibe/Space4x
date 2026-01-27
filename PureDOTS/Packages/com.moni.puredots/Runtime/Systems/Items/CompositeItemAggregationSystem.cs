using PureDOTS.Runtime.Items;
using PureDOTS.Runtime.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Items
{
    /// <summary>
    /// Aggregates part stats into composite item stats.
    /// Runs in FixedStepSimulationSystemGroup and recalculates when parts change.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct CompositeItemAggregationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ItemPartCatalogBlobRef>(out var catalogRef) ||
                !catalogRef.Blob.IsCreated)
            {
                return;
            }

            new AggregateCompositeItemJob { Catalog = catalogRef.Blob }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct AggregateCompositeItemJob : IJobEntity
        {
            [ReadOnly]
            public BlobAssetReference<ItemPartCatalogBlob> Catalog;

            void Execute(
                Entity entity,
                ref CompositeItem composite,
                DynamicBuffer<ItemPart> parts)
            {
                ref var catalog = ref Catalog.Value;

                if (parts.Length == 0)
                {
                    // No parts - mark as broken
                    composite.Flags |= CompositeItemFlags.Broken;
                    composite.AggregatedDurability = (half)0;
                    composite.AggregatedTier = QualityTier.Poor;
                    return;
                }

                // Compute hash of part data for change detection
                uint newHash = ComputePartsHash(parts);
                if (newHash == composite.AggregationHash && composite.AggregationHash != 0)
                {
                    return; // No changes, skip recalculation
                }

                // Aggregate durability (weighted average)
                float totalWeight = 0f;
                float weightedDurability = 0f;
                float weightedQuality = 0f;
                uint totalRarityWeight = 0;
                bool hasBrokenCriticalPart = false;
                bool hasDamagedPart = false;

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (part.PartTypeId >= catalog.PartSpecs.Length)
                        continue; // Invalid part type

                    var spec = catalog.PartSpecs[part.PartTypeId];
                    float weight = spec.AggregationWeight;

                    // Check if part is broken
                    if (part.Durability01 <= 0f)
                    {
                        if (spec.IsCritical)
                        {
                            hasBrokenCriticalPart = true;
                        }
                        part.Flags |= PartFlags.Broken;
                    }
                    else if (part.Durability01 < spec.DamageThreshold01)
                    {
                        hasDamagedPart = true;
                        part.Flags |= PartFlags.Damaged;
                    }
                    else
                    {
                        part.Flags &= ~(PartFlags.Broken | PartFlags.Damaged);
                    }

                    // Apply material durability modifier
                    float materialMod = 1f;
                    int materialIndex = FindMaterialIndex(ref catalog, part.Material);
                    if (materialIndex >= 0 && materialIndex < catalog.MaterialDurabilityMods.Length)
                    {
                        materialMod = catalog.MaterialDurabilityMods[materialIndex];
                    }

                    float effectiveDurability = part.Durability01 * materialMod * spec.DurabilityMultiplier;
                    float effectiveQuality = part.Quality01;

                    totalWeight += weight;
                    weightedDurability += effectiveDurability * weight;
                    weightedQuality += effectiveQuality * weight;
                    totalRarityWeight += part.RarityWeight;

                    parts[i] = part; // Update flags
                }

                // Compute aggregated values
                if (totalWeight > 0f)
                {
                    composite.AggregatedDurability = (half)math.clamp(weightedDurability / totalWeight, 0f, 1f);
                    float avgQuality = weightedQuality / totalWeight;
                    composite.AggregatedTier = QualityTierFromScore(avgQuality);
                }
                else
                {
                    composite.AggregatedDurability = (half)0;
                    composite.AggregatedTier = QualityTier.Poor;
                }

                // Update flags
                composite.Flags = CompositeItemFlags.None;
                if (hasBrokenCriticalPart)
                {
                    composite.Flags |= CompositeItemFlags.Broken;
                }
                if (hasDamagedPart)
                {
                    composite.Flags |= CompositeItemFlags.Damaged;
                }
                if (composite.AggregatedDurability < 0.5f)
                {
                    composite.Flags |= CompositeItemFlags.NeedsRepair;
                }

                composite.AggregationHash = newHash;
            }

            private uint ComputePartsHash(DynamicBuffer<ItemPart> parts)
            {
                uint hash = (uint)parts.Length;
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    hash = math.hash(new uint2(hash, (uint)p.PartTypeId));
                    hash = math.hash(new uint2(hash, math.asuint(p.Quality01)));
                    hash = math.hash(new uint2(hash, math.asuint(p.Durability01)));
                    hash = math.hash(new uint2(hash, (uint)p.RarityWeight));
                }
                return hash;
            }

            private int FindMaterialIndex(ref ItemPartCatalogBlob catalog, FixedString32Bytes material)
            {
                for (int i = 0; i < catalog.MaterialNames.Length; i++)
                {
                    if (catalog.MaterialNames[i].Equals(material))
                    {
                        return i;
                    }
                }
                return -1;
            }

            private QualityTier QualityTierFromScore(float score01)
            {
                if (score01 >= 0.80f) return QualityTier.Masterwork;
                if (score01 >= 0.60f) return QualityTier.Excellent;
                if (score01 >= 0.40f) return QualityTier.Good;
                if (score01 >= 0.20f) return QualityTier.Common;
                return QualityTier.Poor;
            }
        }
    }

    /// <summary>
    /// Singleton reference to ItemPartCatalogBlob.
    /// </summary>
    public struct ItemPartCatalogBlobRef : IComponentData
    {
        public BlobAssetReference<ItemPartCatalogBlob> Blob;
    }
}


using PureDOTS.Runtime.Items;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Items
{
    /// <summary>
    /// Applies durability wear to composite items and handles repair requests.
    /// Runs in FixedStepSimulationSystemGroup after CompositeItemAggregationSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(CompositeItemAggregationSystem))]
    public partial struct DurabilityWearSystem : ISystem
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

            var currentTick = SystemAPI.Time.ElapsedTime;

            // Process wear events
            new ApplyWearJob { Catalog = catalogRef.Blob, CurrentTick = (uint)currentTick }.ScheduleParallel();

            // Process repair requests
            new ProcessRepairJob { Catalog = catalogRef.Blob, CurrentTick = (uint)currentTick }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ApplyWearJob : IJobEntity
        {
            [ReadOnly]
            public BlobAssetReference<ItemPartCatalogBlob> Catalog;

            [ReadOnly]
            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref CompositeItem composite,
                ref DynamicBuffer<ItemPart> parts,
                in DynamicBuffer<DurabilityWearEvent> wearEvents)
            {
                ref var catalog = ref Catalog.Value;

                if (wearEvents.Length == 0)
                    return;

                for (int e = 0; e < wearEvents.Length; e++)
                {
                    var wearEvent = wearEvents[e];
                    if (wearEvent.WearTick != CurrentTick)
                        continue; // Process only current tick events

                    ApplyWearToParts(ref parts, wearEvent, ref catalog);
                }

                // Clear processed events
                wearEvents.Clear();
            }

            private void ApplyWearToParts(
                ref DynamicBuffer<ItemPart> parts,
                DurabilityWearEvent wearEvent,
                ref ItemPartCatalogBlob catalog)
            {
                float wearAmount = wearEvent.WearAmount01;

                if (wearEvent.Type == WearType.Targeted && wearEvent.TargetPartTypeId > 0)
                {
                    // Apply to specific part type only
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].PartTypeId == wearEvent.TargetPartTypeId)
                        {
                            var part = parts[i];
                            part.Durability01 = (half)math.max(0f, part.Durability01 - wearAmount);
                            parts[i] = part;
                            return;
                        }
                    }
                }
                else if (wearEvent.Type == WearType.Uniform)
                {
                    // Distribute evenly
                    float perPartWear = wearAmount / parts.Length;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var part = parts[i];
                        part.Durability01 = (half)math.max(0f, part.Durability01 - perPartWear);
                        parts[i] = part;
                    }
                }
                else if (wearEvent.Type == WearType.Weighted)
                {
                    // Distribute by part weight
                    float totalWeight = 0f;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].PartTypeId < catalog.PartSpecs.Length)
                        {
                            totalWeight += catalog.PartSpecs[parts[i].PartTypeId].AggregationWeight;
                        }
                    }

                    if (totalWeight > 0f)
                    {
                        for (int i = 0; i < parts.Length; i++)
                        {
                            if (parts[i].PartTypeId < catalog.PartSpecs.Length)
                            {
                                var spec = catalog.PartSpecs[parts[i].PartTypeId];
                                float partWear = wearAmount * (spec.AggregationWeight / totalWeight);
                                var part = parts[i];
                                part.Durability01 = (half)math.max(0f, part.Durability01 - partWear);
                                parts[i] = part;
                            }
                        }
                    }
                }
                // Random type would require seed - skip for now (can add later)
            }
        }

        [BurstCompile]
        public partial struct ProcessRepairJob : IJobEntity
        {
            [ReadOnly]
            public BlobAssetReference<ItemPartCatalogBlob> Catalog;

            [ReadOnly]
            public uint CurrentTick;

            void Execute(
                Entity entity,
                ref CompositeItem composite,
                ref DynamicBuffer<ItemPart> parts,
                in RepairRequest repairRequest)
            {
                ref var catalog = ref Catalog.Value;

                if (repairRequest.RepairStartTick == 0)
                    return; // Not started yet

                byte skillLevel = repairRequest.RepairSkillLevel;
                float targetDurability = repairRequest.TargetDurability01;

                // Skill caps: journeyman (50) = 80%, master (75) = 95%, legendary (100) = 100%
                float skillCap = skillLevel switch
                {
                    >= 100 => 1.0f,
                    >= 75 => 0.95f,
                    >= 50 => 0.80f,
                    >= 25 => 0.60f,
                    _ => 0.40f
                };

                float actualTarget = math.min(targetDurability, skillCap);
                bool hasFlawedParts = false;

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (part.PartTypeId >= catalog.PartSpecs.Length)
                        continue;

                    var spec = catalog.PartSpecs[parts[i].PartTypeId];

                    // Check if repair skill is sufficient
                    if (skillLevel < spec.RepairSkillRequired)
                    {
                        continue; // Cannot repair this part
                    }

                    // Restore durability up to skill cap
                    if (part.Durability01 < actualTarget)
                    {
                        part.Durability01 = (half)math.min(actualTarget, part.Durability01 + 0.1f); // Gradual repair

                        // Mark as flawed if restored below original quality
                        if (part.Durability01 < part.Quality01)
                        {
                            part.Flags |= PartFlags.Flawed;
                            hasFlawedParts = true;
                        }
                        else
                        {
                            part.Flags &= ~PartFlags.Flawed;
                        }

                        parts[i] = part;
                    }
                }

                // Update composite flags
                if (hasFlawedParts)
                {
                    composite.Flags |= CompositeItemFlags.Flawed;
                }
                else
                {
                    composite.Flags &= ~CompositeItemFlags.Flawed;
                }

                // Clear repair request if all parts are at target
                bool allRepaired = true;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Durability01 < actualTarget)
                    {
                        allRepaired = false;
                        break;
                    }
                }

                if (allRepaired)
                {
                    // Repair complete - remove request component
                    // (System will handle this via ECB if needed)
                }
            }
        }
    }
}


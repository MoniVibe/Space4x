using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Skills;
using PureDOTS.Runtime.Villager;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Runtime.Villager
{
    /// <summary>
    /// Static helper methods for executing villager job behaviors.
    /// Extracted from VillagerJobExecutionSystem to enable modular behavior patterns.
    /// </summary>
    [BurstCompile]
    public static class VillagerJobBehaviors
    {
        /// <summary>
        /// Executes a gathering job (harvesting resources from nodes).
        /// </summary>
        public static void ExecuteGather(
            Entity entity,
            ref VillagerJob job,
            ref VillagerJobTicket ticket,
            ref VillagerJobProgress progress,
            in VillagerNeeds needs,
            in LocalTransform transform,
            DynamicBuffer<VillagerJobCarryItem> carry,
            ComponentLookup<ResourceSourceState> resourceStateLookup,
            [ReadOnly] ComponentLookup<ResourceSourceConfig> resourceConfigLookup,
            ComponentLookup<ResourceJobReservation> resourceReservationLookup,
            [ReadOnly] ComponentLookup<LocalTransform> transformLookup,
            ComponentLookup<SkillSet> skillSetLookup,
            ComponentLookup<VillagerKnowledge> knowledgeLookup,
            [ReadOnly] ComponentLookup<VillagerStats> statsLookup,
            [ReadOnly] ComponentLookup<VillagerAttributes> attributesLookup,
            BufferLookup<VillagerLessonShare> lessonShareLookup,
            BufferLookup<VillagerJobEvent> eventBuffers,
            Entity eventEntity,
            [ReadOnly] BlobAssetReference<KnowledgeLessonEffectBlob> lessonBlob,
            [ReadOnly] SkillXpCurveConfig xpCurve,
            [ReadOnly] BlobAssetReference<ResourceTypeIndexBlob> resourceCatalog,
            float gatherDistanceSq,
            float deltaTime,
            uint currentTick)
        {
            if (job.Type == VillagerJob.JobType.None || ticket.ResourceEntity == Entity.Null)
            {
                return;
            }

            if (!resourceStateLookup.HasComponent(ticket.ResourceEntity) ||
                !transformLookup.HasComponent(ticket.ResourceEntity))
            {
                return;
            }

            var resourceState = resourceStateLookup[ticket.ResourceEntity];
            var hasKnowledge = knowledgeLookup.HasComponent(entity);
            var knowledge = hasKnowledge ? knowledgeLookup[entity] : default;
            var knowledgeDirty = false;
            DynamicBuffer<VillagerLessonShare> lessonShares = default;
            if (lessonShareLookup.HasBuffer(entity))
            {
                lessonShares = lessonShareLookup[entity];
            }
            var ageYears = ResolveAgeYears(entity, statsLookup, currentTick, deltaTime);
            ResolveMindStats(entity, attributesLookup, out var intelligence, out var wisdom);

            var resourceId = ResolveResourceId(resourceCatalog, ticket.ResourceTypeIndex);
            var harvestModifiers = lessonBlob.IsCreated
                ? KnowledgeLessonEffectUtility.EvaluateHarvestModifiers(ref lessonBlob.Value, knowledge.Lessons, resourceId, resourceState.QualityTier)
                : HarvestLessonModifiers.Identity;

            var knowledgeFlags = knowledge.Flags;
            var resourceTransform = transformLookup[ticket.ResourceEntity];
            var distSq = math.distancesq(transform.Position, resourceTransform.Position);

            if (job.Phase == VillagerJob.JobPhase.Assigned)
            {
                job.Phase = VillagerJob.JobPhase.Gathering;
                job.LastStateChangeTick = currentTick;
                ticket.Phase = (byte)VillagerJob.JobPhase.Gathering;
                ticket.LastProgressTick = currentTick;
            }

            if (job.Phase != VillagerJob.JobPhase.Gathering)
            {
                return;
            }

            if (distSq > gatherDistanceSq)
            {
                progress.TimeInPhase += deltaTime;
                return;
            }

            var config = resourceConfigLookup.HasComponent(ticket.ResourceEntity)
                ? resourceConfigLookup[ticket.ResourceEntity]
                : new ResourceSourceConfig { GatherRatePerWorker = 10f };

            var skillId = ResolveSkillId(job.Type);
            var skillLevel = 0f;
            if (skillSetLookup.HasComponent(entity))
            {
                var skillSet = skillSetLookup[entity];
                skillLevel = skillSet.GetLevel(skillId);
            }

            var gatherRate = math.max(0.1f, config.GatherRatePerWorker) * harvestModifiers.YieldMultiplier;
            var timeMultiplier = math.max(0.1f, harvestModifiers.HarvestTimeMultiplier);
            var harvestMultiplier = math.max(0.1f, ResourceQualityUtility.GetHarvestTimeMultiplier(skillLevel)) * timeMultiplier;
            var gatherAmount = (gatherRate * job.Productivity * deltaTime) / harvestMultiplier;
            var energyMultiplier = math.saturate(needs.Energy / 50f);
            gatherAmount *= energyMultiplier;
            gatherAmount = math.min(gatherAmount, resourceState.UnitsRemaining);

            if (gatherAmount <= 0f)
            {
                progress.TimeInPhase += deltaTime;
                return;
            }

            resourceState.UnitsRemaining -= gatherAmount;
            resourceStateLookup[ticket.ResourceEntity] = resourceState;

            if (resourceReservationLookup.HasComponent(ticket.ResourceEntity))
            {
                var reservation = resourceReservationLookup[ticket.ResourceEntity];
                reservation.ReservedUnits = math.max(0f, reservation.ReservedUnits - gatherAmount);
                reservation.LastMutationTick = currentTick;
                resourceReservationLookup[ticket.ResourceEntity] = reservation;
            }

            var ticketReserved = ticket.ReservedUnits;
            ticket.ReservedUnits = math.max(0f, ticketReserved - gatherAmount);

            var villagerQuality = KnowledgeLessonEffectUtility.EvaluateHarvestQuality(
                resourceState.BaseQuality,
                resourceState.QualityVariance,
                resourceState.QualityTier,
                skillLevel,
                harvestModifiers,
                knowledgeFlags);
            var carryTier = (byte)ResourceQualityUtility.DetermineTier(villagerQuality);

            var carryIndex = -1;
            for (int i = 0; i < carry.Length; i++)
            {
                if (carry[i].ResourceTypeIndex == ticket.ResourceTypeIndex && carry[i].TierId == carryTier)
                {
                    carryIndex = i;
                    break;
                }
            }

            var incomingPayload = ResourcePayloadUtility.Create(
                ticket.ResourceTypeIndex,
                gatherAmount,
                carryTier,
                villagerQuality);

            if (carryIndex >= 0)
            {
                var payload = carry[carryIndex].AsPayload();
                ResourcePayloadUtility.Merge(ref payload, in incomingPayload);
                carry[carryIndex].ApplyPayload(payload);
            }
            else
            {
                carry.Add(VillagerJobCarryItem.FromPayload(in incomingPayload));
            }

            GrantHarvestXp(entity, skillId, gatherAmount, skillSetLookup, xpCurve);

            progress.Gathered += gatherAmount;
            progress.TimeInPhase += deltaTime;
            progress.LastUpdateTick = currentTick;
            ticket.LastProgressTick = currentTick;

            var events = eventBuffers[eventEntity];
            events.Add(new VillagerJobEvent
            {
                Tick = currentTick,
                Villager = entity,
                EventType = VillagerJobEventType.JobProgress,
                ResourceTypeIndex = ticket.ResourceTypeIndex,
                Amount = gatherAmount,
                TicketId = ticket.TicketId
            });

            if (hasKnowledge && config.LessonId.Length > 0 && TryLearnResourceLesson(ref knowledge, config.LessonId, skillLevel, gatherAmount, ageYears, intelligence, wisdom, lessonShares, lessonBlob))
            {
                knowledgeDirty = true;
            }

            if (resourceState.UnitsRemaining <= 0f ||
                GetCarryAmount(carry, ticket.ResourceTypeIndex) >= 40f)
            {
                job.Phase = VillagerJob.JobPhase.Delivering;
                job.LastStateChangeTick = currentTick;
                ticket.Phase = (byte)VillagerJob.JobPhase.Delivering;
            }

            if (knowledgeDirty)
            {
                knowledgeLookup[entity] = knowledge;
            }
        }

        /// <summary>
        /// Executes a build job (constructing buildings/structures).
        /// </summary>
        public static void ExecuteBuild(
            Entity entity,
            ref VillagerJob job,
            ref VillagerJobTicket ticket,
            ref VillagerJobProgress progress,
            in VillagerNeeds needs,
            in LocalTransform transform,
            DynamicBuffer<VillagerJobCarryItem> carry,
            [ReadOnly] ComponentLookup<LocalTransform> transformLookup,
            ComponentLookup<SkillSet> skillSetLookup,
            BufferLookup<VillagerJobEvent> eventBuffers,
            Entity eventEntity,
            ComponentLookup<ConstructionSiteProgress> constructionProgressLookup,
            BufferLookup<ConstructionDeliveredElement> constructionDeliveredLookup,
            ComponentLookup<ConstructionSiteFlags> constructionFlagsLookup,
            [ReadOnly] BlobAssetReference<ResourceTypeIndexBlob> resourceCatalog,
            [ReadOnly] SkillXpCurveConfig xpCurve,
            float buildDistanceSq,
            float deltaTime,
            uint currentTick)
        {
            if (job.Type != VillagerJob.JobType.Builder || ticket.ResourceEntity == Entity.Null)
            {
                return;
            }

            // Validate construction site exists
            if (!constructionProgressLookup.HasComponent(ticket.ResourceEntity))
            {
                return;
            }

            // Check distance to construction site
            if (!transformLookup.HasComponent(ticket.ResourceEntity))
            {
                return;
            }

            var siteTransform = transformLookup[ticket.ResourceEntity];
            var distSq = math.distancesq(transform.Position, siteTransform.Position);

            // Phase transition: Assigned -> Building
            if (job.Phase == VillagerJob.JobPhase.Assigned)
            {
                job.Phase = VillagerJob.JobPhase.Building;
                job.LastStateChangeTick = currentTick;
                ticket.Phase = (byte)VillagerJob.JobPhase.Building;
                ticket.LastProgressTick = currentTick;
            }

            if (job.Phase != VillagerJob.JobPhase.Building)
            {
                return;
            }

            // Too far to build
            if (distSq > buildDistanceSq)
            {
                progress.TimeInPhase += deltaTime;
                return;
            }

            var siteProgress = constructionProgressLookup[ticket.ResourceEntity];

            // Check if site is already completed
            if (constructionFlagsLookup.HasComponent(ticket.ResourceEntity))
            {
                var flags = constructionFlagsLookup[ticket.ResourceEntity];
                if ((flags.Value & ConstructionSiteFlags.Completed) != 0)
                {
                    // Construction complete, transition to delivering (if carrying) or idle
                    if (carry.Length > 0)
                    {
                        job.Phase = VillagerJob.JobPhase.Delivering;
                        job.LastStateChangeTick = currentTick;
                        ticket.Phase = (byte)VillagerJob.JobPhase.Delivering;
                    }
                    else
                    {
                        job.Phase = VillagerJob.JobPhase.Idle;
                        job.LastStateChangeTick = currentTick;
                        ticket.Phase = (byte)VillagerJob.JobPhase.Idle;
                    }
                    return;
                }
            }

            // First, try to deliver carried materials to the construction site
            var deliveredMaterials = false;
            if (carry.Length > 0 && constructionDeliveredLookup.HasBuffer(ticket.ResourceEntity))
            {
                var deliveredBuffer = constructionDeliveredLookup[ticket.ResourceEntity];
                
                for (int i = carry.Length - 1; i >= 0; i--)
                {
                    var carryItem = carry[i];
                    var resourceId = ResolveResourceId(resourceCatalog, carryItem.ResourceTypeIndex);
                    
                    // Find matching delivery slot
                    for (int j = 0; j < deliveredBuffer.Length; j++)
                    {
                        var delivered = deliveredBuffer[j];
                        if (delivered.ResourceTypeId.Equals(resourceId))
                        {
                            // Deliver materials
                            delivered.UnitsDelivered += carryItem.Amount;
                            deliveredBuffer[j] = delivered;
                            deliveredMaterials = true;
                            
                            // Emit delivery event
                            if (eventBuffers.HasBuffer(eventEntity))
                            {
                                var events = eventBuffers[eventEntity];
                                events.Add(new VillagerJobEvent
                                {
                                    Tick = currentTick,
                                    Villager = entity,
                                    EventType = VillagerJobEventType.JobProgress,
                                    ResourceTypeIndex = carryItem.ResourceTypeIndex,
                                    Amount = carryItem.Amount,
                                    TicketId = ticket.TicketId
                                });
                            }
                            break;
                        }
                    }
                }
                
                // Clear carry buffer after delivery
                if (deliveredMaterials)
                {
                    carry.Clear();
                }
            }

            // Calculate build rate based on skill
            var skillId = SkillId.Processing; // Construction uses Processing skill
            var skillLevel = 0f;
            if (skillSetLookup.HasComponent(entity))
            {
                var skillSet = skillSetLookup[entity];
                skillLevel = skillSet.GetLevel(skillId);
            }

            // Base build rate modified by skill and energy
            var baseBuildRate = 1f; // Units of progress per second
            var skillMultiplier = 1f + (skillLevel / 100f); // Up to 2x at max skill
            var energyMultiplier = math.saturate(needs.Energy / 50f);
            var buildRate = baseBuildRate * skillMultiplier * energyMultiplier * job.Productivity;
            var buildAmount = buildRate * deltaTime;

            // Apply progress
            siteProgress.CurrentProgress = math.min(siteProgress.CurrentProgress + buildAmount, siteProgress.RequiredProgress);
            constructionProgressLookup[ticket.ResourceEntity] = siteProgress;

            // Grant XP for building
            GrantBuildXp(entity, skillId, buildAmount, skillSetLookup, xpCurve);

            // Update progress tracking
            progress.TimeInPhase += deltaTime;
            progress.LastUpdateTick = currentTick;
            ticket.LastProgressTick = currentTick;

            // Emit progress event
            if (eventBuffers.HasBuffer(eventEntity))
            {
                var events = eventBuffers[eventEntity];
                events.Add(new VillagerJobEvent
                {
                    Tick = currentTick,
                    Villager = entity,
                    EventType = VillagerJobEventType.JobProgress,
                    Amount = buildAmount,
                    TicketId = ticket.TicketId
                });
            }

            // Check for completion
            if (siteProgress.CurrentProgress >= siteProgress.RequiredProgress)
            {
                // Mark site as completed
                if (constructionFlagsLookup.HasComponent(ticket.ResourceEntity))
                {
                    var flags = constructionFlagsLookup[ticket.ResourceEntity];
                    flags.Value |= ConstructionSiteFlags.Completed;
                    constructionFlagsLookup[ticket.ResourceEntity] = flags;
                }

                // Transition to idle
                job.Phase = VillagerJob.JobPhase.Idle;
                job.LastStateChangeTick = currentTick;
                ticket.Phase = (byte)VillagerJob.JobPhase.Idle;

                // Emit completion event
                if (eventBuffers.HasBuffer(eventEntity))
                {
                    var events = eventBuffers[eventEntity];
                    events.Add(new VillagerJobEvent
                    {
                        Tick = currentTick,
                        Villager = entity,
                        EventType = VillagerJobEventType.JobCompleted,
                        TicketId = ticket.TicketId
                    });
                }
            }
        }

        private static void GrantBuildXp(Entity villager, SkillId skillId, float buildAmount, ComponentLookup<SkillSet> skillSetLookup, [ReadOnly] SkillXpCurveConfig xpCurve)
        {
            if (!skillSetLookup.HasComponent(villager) || buildAmount <= 0f)
            {
                return;
            }

            var skillSet = skillSetLookup[villager];
            var pool = XpPool.Finesse; // Building uses Finesse
            var scalar = xpCurve.GetScalar(pool);
            var adjusted = buildAmount * scalar * 0.5f; // Half XP compared to gathering
            skillSet.AddSkillXp(skillId, adjusted);
            skillSet.FinesseXp += adjusted;
            skillSetLookup[villager] = skillSet;
        }

        /// <summary>
        /// Executes a craft job (processing materials into items).
        /// </summary>
        public static void ExecuteCraft(
            Entity entity,
            ref VillagerJob job,
            ref VillagerJobTicket ticket,
            ref VillagerJobProgress progress,
            in VillagerNeeds needs,
            in LocalTransform transform,
            DynamicBuffer<VillagerJobCarryItem> carry,
            [ReadOnly] ComponentLookup<LocalTransform> transformLookup,
            ComponentLookup<SkillSet> skillSetLookup,
            BufferLookup<VillagerJobEvent> eventBuffers,
            Entity eventEntity,
            [ReadOnly] BlobAssetReference<ResourceTypeIndexBlob> resourceCatalog,
            [ReadOnly] SkillXpCurveConfig xpCurve,
            float craftDistanceSq,
            float deltaTime,
            uint currentTick)
        {
            if (job.Type != VillagerJob.JobType.Crafter || ticket.ResourceEntity == Entity.Null)
            {
                return;
            }

            // Check distance to crafting station (ticket.ResourceEntity is the station)
            if (!transformLookup.HasComponent(ticket.ResourceEntity))
            {
                return;
            }

            var stationTransform = transformLookup[ticket.ResourceEntity];
            var distSq = math.distancesq(transform.Position, stationTransform.Position);

            // Phase transition: Assigned -> Crafting
            if (job.Phase == VillagerJob.JobPhase.Assigned)
            {
                job.Phase = VillagerJob.JobPhase.Crafting;
                job.LastStateChangeTick = currentTick;
                ticket.Phase = (byte)VillagerJob.JobPhase.Crafting;
                ticket.LastProgressTick = currentTick;
            }

            if (job.Phase != VillagerJob.JobPhase.Crafting)
            {
                return;
            }

            // Too far to craft
            if (distSq > craftDistanceSq)
            {
                progress.TimeInPhase += deltaTime;
                return;
            }

            // Check if we have materials to craft with
            if (carry.Length == 0)
            {
                // No materials - transition to delivering (empty) which will trigger job completion
                job.Phase = VillagerJob.JobPhase.Delivering;
                job.LastStateChangeTick = currentTick;
                ticket.Phase = (byte)VillagerJob.JobPhase.Delivering;
                return;
            }

            // Calculate craft rate based on skill
            var skillId = SkillId.Processing;
            var skillLevel = 0f;
            if (skillSetLookup.HasComponent(entity))
            {
                var skillSet = skillSetLookup[entity];
                skillLevel = skillSet.GetLevel(skillId);
            }

            // Base craft rate modified by skill and energy
            var baseCraftRate = 2f; // Units processed per second
            var skillMultiplier = 1f + (skillLevel / 100f);
            var energyMultiplier = math.saturate(needs.Energy / 50f);
            var craftRate = baseCraftRate * skillMultiplier * energyMultiplier * job.Productivity;
            var craftAmount = craftRate * deltaTime;

            // Process materials from carry buffer
            // Simple conversion: input materials become output at 1:1 ratio
            // More complex recipes would require recipe definitions
            var totalProcessed = 0f;
            for (int i = carry.Length - 1; i >= 0; i--)
            {
                var item = carry[i];
                var processAmount = math.min(craftAmount - totalProcessed, item.Amount);
                
                if (processAmount <= 0f)
                {
                    break;
                }

                item.Amount -= processAmount;
                totalProcessed += processAmount;

                if (item.Amount <= 0.001f)
                {
                    carry.RemoveAtSwapBack(i);
                }
                else
                {
                    carry[i] = item;
                }
            }

            if (totalProcessed > 0f)
            {
                // Grant XP for crafting
                GrantCraftXp(entity, skillId, totalProcessed, skillSetLookup, xpCurve);

                // Update progress
                progress.Gathered += totalProcessed; // Reuse Gathered for crafted amount
                progress.TimeInPhase += deltaTime;
                progress.LastUpdateTick = currentTick;
                ticket.LastProgressTick = currentTick;

                // Emit progress event
                if (eventBuffers.HasBuffer(eventEntity))
                {
                    var events = eventBuffers[eventEntity];
                    events.Add(new VillagerJobEvent
                    {
                        Tick = currentTick,
                        Villager = entity,
                        EventType = VillagerJobEventType.JobProgress,
                        Amount = totalProcessed,
                        TicketId = ticket.TicketId
                    });
                }
            }

            // Check if all materials processed
            if (carry.Length == 0)
            {
                // Crafting complete - transition to delivering output
                job.Phase = VillagerJob.JobPhase.Delivering;
                job.LastStateChangeTick = currentTick;
                ticket.Phase = (byte)VillagerJob.JobPhase.Delivering;

                // Emit completion event
                if (eventBuffers.HasBuffer(eventEntity))
                {
                    var events = eventBuffers[eventEntity];
                    events.Add(new VillagerJobEvent
                    {
                        Tick = currentTick,
                        Villager = entity,
                        EventType = VillagerJobEventType.JobCompleted,
                        TicketId = ticket.TicketId
                    });
                }
            }
        }

        private static void GrantCraftXp(Entity villager, SkillId skillId, float craftAmount, ComponentLookup<SkillSet> skillSetLookup, [ReadOnly] SkillXpCurveConfig xpCurve)
        {
            if (!skillSetLookup.HasComponent(villager) || craftAmount <= 0f)
            {
                return;
            }

            var skillSet = skillSetLookup[villager];
            var pool = XpPool.Finesse;
            var scalar = xpCurve.GetScalar(pool);
            var adjusted = craftAmount * scalar;
            skillSet.AddSkillXp(skillId, adjusted);
            skillSet.FinesseXp += adjusted;
            skillSetLookup[villager] = skillSet;
        }

        /// <summary>
        /// Executes a combat job (fighting enemies).
        /// </summary>
        public static void ExecuteCombat(
            Entity entity,
            ref VillagerJob job,
            ref VillagerJobTicket ticket,
            ref VillagerJobProgress progress,
            in VillagerNeeds needs,
            in LocalTransform transform,
            DynamicBuffer<VillagerJobCarryItem> carry,
            [ReadOnly] ComponentLookup<LocalTransform> transformLookup,
            ComponentLookup<VillagerCombatStats> combatStatsLookup,
            ComponentLookup<VillagerFlags> villagerFlagsLookup,
            BufferLookup<VillagerJobEvent> eventBuffers,
            Entity eventEntity,
            [ReadOnly] SkillXpCurveConfig xpCurve,
            float combatRangeSq,
            float deltaTime,
            uint currentTick)
        {
            if (job.Type != VillagerJob.JobType.Guard)
            {
                return;
            }

            // Get combat stats
            if (!combatStatsLookup.HasComponent(entity))
            {
                // No combat capability - exit job
                job.Phase = VillagerJob.JobPhase.Idle;
                job.LastStateChangeTick = currentTick;
                ticket.Phase = (byte)VillagerJob.JobPhase.Idle;
                return;
            }

            var combatStats = combatStatsLookup[entity];
            var targetEntity = combatStats.CurrentTarget;

            // Phase transition: Assigned -> Fighting
            if (job.Phase == VillagerJob.JobPhase.Assigned)
            {
                job.Phase = VillagerJob.JobPhase.Fighting;
                job.LastStateChangeTick = currentTick;
                ticket.Phase = (byte)VillagerJob.JobPhase.Fighting;
                ticket.LastProgressTick = currentTick;
            }

            if (job.Phase != VillagerJob.JobPhase.Fighting)
            {
                return;
            }

            // No target - check if we should patrol or idle
            if (targetEntity == Entity.Null)
            {
                // Guard without target - patrol behavior (just update time for now)
                progress.TimeInPhase += deltaTime;
                
                // After some time without a target, return to idle
                if (progress.TimeInPhase > 10f)
                {
                    job.Phase = VillagerJob.JobPhase.Idle;
                    job.LastStateChangeTick = currentTick;
                    ticket.Phase = (byte)VillagerJob.JobPhase.Idle;
                }
                return;
            }

            // Validate target still exists and is alive
            if (!transformLookup.HasComponent(targetEntity))
            {
                // Target gone - clear and continue patrolling
                combatStats.CurrentTarget = Entity.Null;
                combatStatsLookup[entity] = combatStats;
                progress.TimeInPhase = 0f;
                return;
            }

            // Check if target is dead
            if (villagerFlagsLookup.HasComponent(targetEntity))
            {
                var targetFlags = villagerFlagsLookup[targetEntity];
                if (targetFlags.IsDead)
                {
                    // Target dead - clear and emit victory event
                    combatStats.CurrentTarget = Entity.Null;
                    combatStatsLookup[entity] = combatStats;
                    progress.TimeInPhase = 0f;

                    if (eventBuffers.HasBuffer(eventEntity))
                    {
                        var events = eventBuffers[eventEntity];
                        events.Add(new VillagerJobEvent
                        {
                            Tick = currentTick,
                            Villager = entity,
                            EventType = VillagerJobEventType.JobCompleted,
                            TicketId = ticket.TicketId
                        });
                    }
                    return;
                }
            }

            // Check distance to target
            var targetTransform = transformLookup[targetEntity];
            var distSq = math.distancesq(transform.Position, targetTransform.Position);

            // Too far to attack
            if (distSq > combatRangeSq)
            {
                progress.TimeInPhase += deltaTime;
                return;
            }

            // In range - calculate attack timing
            var attackInterval = combatStats.AttackSpeed > 0f ? 1f / combatStats.AttackSpeed : 1f;
            progress.TimeInPhase += deltaTime;

            // Check if enough time has passed for next attack
            if (progress.TimeInPhase < attackInterval)
            {
                return;
            }

            // Execute attack
            var damage = combatStats.AttackDamage;
            
            // Apply damage to target if it has needs (health)
            if (combatStatsLookup.HasComponent(targetEntity))
            {
                // Target is a combatant - apply damage via combat stats
                // Note: Actual damage application would go through a damage system
                // For now, we emit an event that can be processed by a damage system
            }

            // Reset attack timer
            progress.TimeInPhase = 0f;
            progress.LastUpdateTick = currentTick;
            ticket.LastProgressTick = currentTick;

            // Emit attack event
            if (eventBuffers.HasBuffer(eventEntity))
            {
                var events = eventBuffers[eventEntity];
                events.Add(new VillagerJobEvent
                {
                    Tick = currentTick,
                    Villager = entity,
                    EventType = VillagerJobEventType.JobProgress,
                    Amount = damage,
                    TicketId = ticket.TicketId
                });
            }

            // Grant combat XP
            GrantCombatXp(entity, damage, combatStatsLookup);
        }

        private static void GrantCombatXp(Entity villager, float damage, ComponentLookup<VillagerCombatStats> combatStatsLookup)
        {
            // Combat XP would typically be granted through a skill system
            // For now, this is a placeholder that could be expanded
            // to integrate with the skill system when combat skills are defined
        }

        // Helper methods

        private static float ResolveAgeYears(Entity entity, [ReadOnly] ComponentLookup<VillagerStats> statsLookup, uint currentTick, float deltaTime)
        {
            if (!statsLookup.HasComponent(entity))
            {
                return 25f;
            }

            var stats = statsLookup[entity];
            var livedTicks = currentTick >= stats.BirthTick ? currentTick - stats.BirthTick : 0u;
            var livedSeconds = livedTicks * deltaTime;
            const float SecondsPerSimYear = 600f;
            return math.max(1f, livedSeconds / SecondsPerSimYear);
        }

        private static void ResolveMindStats(Entity entity, [ReadOnly] ComponentLookup<VillagerAttributes> attributesLookup, out float intelligence, out float wisdom)
        {
            if (attributesLookup.HasComponent(entity))
            {
                var attributes = attributesLookup[entity];
                intelligence = attributes.Intelligence;
                wisdom = attributes.Wisdom;
            }
            else
            {
                intelligence = 50f;
                wisdom = 50f;
            }
        }

        private static FixedString64Bytes ResolveResourceId(BlobAssetReference<ResourceTypeIndexBlob> catalog, ushort resourceTypeIndex)
        {
            if (!catalog.IsCreated)
            {
                return default;
            }

            ref var blob = ref catalog.Value;
            if (resourceTypeIndex >= blob.Ids.Length)
            {
                return default;
            }

            return blob.Ids[resourceTypeIndex];
        }

        private static SkillId ResolveSkillId(VillagerJob.JobType jobType)
        {
            return jobType switch
            {
                VillagerJob.JobType.Hunter => SkillId.AnimalHandling,
                VillagerJob.JobType.Crafter => SkillId.Processing,
                _ => SkillId.HarvestBotany
            };
        }

        private static void GrantHarvestXp(Entity villager, SkillId skillId, float gatherAmount, ComponentLookup<SkillSet> skillSetLookup, [ReadOnly] SkillXpCurveConfig xpCurve)
        {
            if (!skillSetLookup.HasComponent(villager) || gatherAmount <= 0f)
            {
                return;
            }

            var skillSet = skillSetLookup[villager];
            var pool = ResolveXpPool(skillId);
            var scalar = xpCurve.GetScalar(pool);
            var adjusted = gatherAmount * scalar;
            skillSet.AddSkillXp(skillId, adjusted);
            switch (pool)
            {
                case XpPool.Physique:
                    skillSet.PhysiqueXp += adjusted;
                    break;
                case XpPool.Finesse:
                    skillSet.FinesseXp += adjusted;
                    break;
                case XpPool.Will:
                    skillSet.WillXp += adjusted;
                    break;
                default:
                    skillSet.GeneralXp += adjusted;
                    break;
            }
            skillSetLookup[villager] = skillSet;
        }

        private static XpPool ResolveXpPool(SkillId skillId)
        {
            return skillId switch
            {
                SkillId.AnimalHandling => XpPool.Will,
                SkillId.Processing => XpPool.Finesse,
                SkillId.Mining => XpPool.Physique,
                SkillId.HarvestBotany => XpPool.Physique,
                _ => XpPool.General
            };
        }

        private static float GetCarryAmount(DynamicBuffer<VillagerJobCarryItem> carry, ushort resourceTypeIndex)
        {
            var total = 0f;
            for (int i = 0; i < carry.Length; i++)
            {
                if (carry[i].ResourceTypeIndex == resourceTypeIndex)
                {
                    total += carry[i].Amount;
                }
            }
            return total;
        }

        private static bool TryLearnResourceLesson(
            ref VillagerKnowledge knowledge,
            in FixedString64Bytes lessonId,
            float skillLevel,
            float gatherAmount,
            float ageYears,
            float intelligence,
            float wisdom,
            DynamicBuffer<VillagerLessonShare> lessonShares,
            [ReadOnly] BlobAssetReference<KnowledgeLessonEffectBlob> lessonBlob)
        {
            if (lessonId.Length == 0 || gatherAmount <= 0f || !lessonBlob.IsCreated)
            {
                return false;
            }

            if (knowledge.FindLessonIndex(lessonId) < 0 && knowledge.Lessons.Length >= knowledge.Lessons.Capacity)
            {
                return false;
            }

            ref var lessonBlobValue = ref lessonBlob.Value;
            var previousProgress = knowledge.GetProgress(lessonId);
            var hasMetadata = KnowledgeLessonEffectUtility.TryGetLessonMetadata(ref lessonBlobValue, lessonId, out var metadata);
            var difficulty = hasMetadata && metadata.Difficulty > 0 ? metadata.Difficulty : (byte)25;

            var baseDelta = gatherAmount * 0.001f;
            baseDelta *= math.max(0.35f, skillLevel / 120f);
            baseDelta /= math.max(10f, difficulty);

            var delta = baseDelta * ComputeAgeLearningScalar(ageYears) * ComputeMindScalar(intelligence, wisdom);
            delta += ConsumeLessonShares(ref lessonShares, lessonId);

            if (hasMetadata)
            {
                delta = ApplyOppositionRules(ref knowledge, metadata, delta);
            }

            if (delta <= 0f)
            {
                return false;
            }

            knowledge.AddProgress(lessonId, delta, out var newProgress);
            return newProgress > previousProgress;
        }

        private static float ComputeAgeLearningScalar(float ageYears)
        {
            if (ageYears <= 12f)
            {
                return math.lerp(1.75f, 1.3f, math.saturate(ageYears / 12f));
            }
            if (ageYears <= 25f)
            {
                return math.lerp(1.3f, 1f, (ageYears - 12f) / 13f);
            }
            if (ageYears <= 45f)
            {
                return 1f;
            }
            if (ageYears <= 70f)
            {
                return math.lerp(1f, 0.85f, (ageYears - 45f) / 25f);
            }
            return 0.7f;
        }

        private static float ComputeMindScalar(float intelligence, float wisdom)
        {
            var combined = math.max(5f, (intelligence + wisdom) * 0.5f);
            return math.clamp(0.6f + combined / 200f, 0.6f, 1.8f);
        }

        private static float ConsumeLessonShares(ref DynamicBuffer<VillagerLessonShare> shares, in FixedString64Bytes lessonId)
        {
            if (!shares.IsCreated || shares.Length == 0)
            {
                return 0f;
            }

            float granted = 0f;
            for (int i = 0; i < shares.Length; i++)
            {
                if (!shares[i].LessonId.Equals(lessonId) || shares[i].Progress <= 0f)
                {
                    continue;
                }

                granted += shares[i].Progress;
                var entry = shares[i];
                entry.Progress = 0f;
                shares[i] = entry;
            }

            return granted;
        }

        private static float ApplyOppositionRules(ref VillagerKnowledge knowledge, in KnowledgeLessonMetadata metadata, float delta)
        {
            if (delta <= 0f || metadata.OppositeLessonId.Length == 0)
            {
                return delta;
            }

            var oppositeProgress = knowledge.GetProgress(metadata.OppositeLessonId);
            if (oppositeProgress > 0f)
            {
                var penalty = delta * 0.33f;
                knowledge.AddProgress(metadata.OppositeLessonId, -penalty, out _);
            }

            if ((metadata.Flags & KnowledgeLessonFlags.AllowParallelOpposites) == 0 && oppositeProgress < 1f)
            {
                delta *= 0.4f;
            }

            return delta;
        }
    }
}


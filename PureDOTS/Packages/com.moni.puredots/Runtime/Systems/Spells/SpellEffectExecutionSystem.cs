using PureDOTS.Runtime.Buffs;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spells;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Spells
{
    /// <summary>
    /// Processes SpellCastEvent buffer and applies spell effects (damage, healing, buffs, etc.).
    /// Runs after combat systems to apply spell effects.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    // Removed invalid UpdateAfter: CombatSystemGroup runs under PhysicsSystemGroup.
    public partial struct SpellEffectExecutionSystem : ISystem
    {
        private BufferLookup<ActiveBuff> _activeBuffLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VillagerNeeds> _needsLookup;
        
        // Instance fields for Burst-compatible FixedString patterns (initialized in OnCreate)
        private FixedString64Bytes _healthId;
        private FixedString64Bytes _energyId;
        private FixedString64Bytes _hungerId;
        private FixedString64Bytes _shieldDefaultId;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _activeBuffLookup = state.GetBufferLookup<ActiveBuff>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _needsLookup = state.GetComponentLookup<VillagerNeeds>(false);
            
            // Initialize FixedString patterns (OnCreate is not Burst-compiled)
            _healthId = new FixedString64Bytes("health");
            _energyId = new FixedString64Bytes("energy");
            _hungerId = new FixedString64Bytes("hunger");
            _shieldDefaultId = new FixedString64Bytes("shield_default");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Get spell catalog
            if (!SystemAPI.TryGetSingleton<SpellCatalogRef>(out var spellCatalogRef) ||
                !spellCatalogRef.Blob.IsCreated)
            {
                return;
            }

            ref var spellCatalog = ref spellCatalogRef.Blob.Value;

            _activeBuffLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _needsLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new ProcessSpellEffectsJob
            {
                SpellCatalog = spellCatalog,
                CurrentTick = currentTick,
                Ecb = ecb,
                ActiveBuffLookup = _activeBuffLookup,
                TransformLookup = _transformLookup,
                NeedsLookup = _needsLookup,
                HealthId = _healthId,
                EnergyId = _energyId,
                HungerId = _hungerId,
                ShieldDefaultId = _shieldDefaultId
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ProcessSpellEffectsJob : IJobEntity
        {
            [ReadOnly]
            public SpellDefinitionBlob SpellCatalog;

            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [NativeDisableParallelForRestriction] public BufferLookup<ActiveBuff> ActiveBuffLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> TransformLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<VillagerNeeds> NeedsLookup;
            [ReadOnly] public FixedString64Bytes HealthId;
            [ReadOnly] public FixedString64Bytes EnergyId;
            [ReadOnly] public FixedString64Bytes HungerId;
            [ReadOnly] public FixedString64Bytes ShieldDefaultId;

            void Execute(
                Entity casterEntity,
                [EntityIndexInQuery] int entityInQueryIndex,
                DynamicBuffer<SpellCastEvent> castEvents,
                ref DynamicBuffer<DamageEvent> damageEvents,
                ref DynamicBuffer<HealEvent> healEvents,
                ref DynamicBuffer<BuffApplicationRequest> buffRequests)
            {
                for (int i = 0; i < castEvents.Length; i++)
                {
                    var castEvent = castEvents[i];

                    if (castEvent.Result != SpellCastResult.Success)
                    {
                        continue; // Skip failed casts
                    }

                    // Find spell definition
                    var spellIndex = -1;
                    for (int j = 0; j < SpellCatalog.Spells.Length; j++)
                    {
                        if (SpellCatalog.Spells[j].SpellId.Equals(castEvent.SpellId))
                        {
                            spellIndex = j;
                            break;
                        }
                    }

                    if (spellIndex == -1)
                    {
                        continue;
                    }
                    ref var spellEntry = ref SpellCatalog.Spells[spellIndex];

                    // Apply each effect
                    for (int j = 0; j < spellEntry.Effects.Length; j++)
                    {
                        var effect = spellEntry.Effects[j];
                        float effectiveValue = effect.BaseValue * (1f + effect.ScalingFactor * castEvent.EffectiveStrength);

                        ApplySpellEffect(
                            casterEntity,
                            entityInQueryIndex,
                            castEvent,
                            ref spellEntry,
                            effect,
                            effectiveValue,
                            ref damageEvents,
                            ref healEvents,
                            ref buffRequests,
                            Ecb);
                    }
                }

                // Clear processed events
                castEvents.Clear();
            }

            [BurstCompile]
            private void ApplySpellEffect(
                Entity casterEntity,
                int entityInQueryIndex,
                SpellCastEvent castEvent,
                ref SpellEntry spellEntry,
                SpellEffect effect,
                float effectiveValue,
                ref DynamicBuffer<DamageEvent> damageEvents,
                ref DynamicBuffer<HealEvent> healEvents,
                ref DynamicBuffer<BuffApplicationRequest> buffRequests,
                EntityCommandBuffer.ParallelWriter ecb)
            {
                Entity targetEntity = castEvent.TargetEntity;
                float3 targetPosition = castEvent.TargetPosition;

                switch (effect.Type)
                {
                    case SpellEffectType.Damage:
                        ApplyDamageEffect(casterEntity, targetEntity, ref spellEntry, effectiveValue, ecb, entityInQueryIndex);
                        break;

                    case SpellEffectType.Heal:
                        ApplyHealEffect(casterEntity, targetEntity, effectiveValue, ecb, entityInQueryIndex);
                        break;

                    case SpellEffectType.ApplyBuff:
                    case SpellEffectType.ApplyDebuff:
                        ApplyBuffEffect(casterEntity, targetEntity, effect, ref buffRequests);
                        break;

                    case SpellEffectType.Dispel:
                        ApplyDispelEffect(targetEntity, effect, (int)effectiveValue);
                        break;

                    case SpellEffectType.Shield:
                        ApplyShieldEffect(casterEntity, targetEntity, effect, effectiveValue, ref buffRequests);
                        break;

                    case SpellEffectType.Summon:
                        ApplySummonEffect(casterEntity, targetPosition, effect, ecb, entityInQueryIndex);
                        break;

                    case SpellEffectType.Teleport:
                        ApplyTeleportEffect(targetEntity, targetPosition);
                        break;

                    case SpellEffectType.ResourceGrant:
                        ApplyResourceGrantEffect(targetEntity, effect, effectiveValue);
                        break;

                    case SpellEffectType.StatModify:
                        ApplyStatModifyEffect(casterEntity, targetEntity, effect, effectiveValue, ref buffRequests);
                        break;
                }
            }

            private void ApplyDamageEffect(Entity casterEntity, Entity targetEntity, ref SpellEntry spellEntry, float effectiveValue, EntityCommandBuffer.ParallelWriter ecb, int entityInQueryIndex)
            {
                if (targetEntity == Entity.Null)
                {
                    return;
                }

                // Determine damage type from spell school
                var damageType = SpellSchoolToDamageType(spellEntry.School);

                // Add damage event via ECB since we can't access target buffers directly in parallel
                ecb.AppendToBuffer(entityInQueryIndex, targetEntity, new DamageEvent
                {
                    SourceEntity = casterEntity,
                    TargetEntity = targetEntity,
                    RawDamage = effectiveValue,
                    Type = damageType,
                    Tick = CurrentTick,
                    Flags = DamageFlags.None
                });
            }

            private static DamageType SpellSchoolToDamageType(SpellSchool school)
            {
                return school switch
                {
                    SpellSchool.Elemental => DamageType.Fire, // Elemental defaults to fire
                    SpellSchool.Nature => DamageType.Poison, // Nature uses poison
                    SpellSchool.Divine => DamageType.True, // Divine damage bypasses armor
                    SpellSchool.Light => DamageType.Fire, // Light uses fire damage
                    SpellSchool.Shadow => DamageType.Cold, // Shadow uses cold damage
                    SpellSchool.Psionic => DamageType.True, // Psionic bypasses armor
                    _ => DamageType.Physical
                };
            }

            private void ApplyHealEffect(Entity casterEntity, Entity targetEntity, float effectiveValue, EntityCommandBuffer.ParallelWriter ecb, int entityInQueryIndex)
            {
                if (targetEntity == Entity.Null)
                {
                    return;
                }

                ecb.AppendToBuffer(entityInQueryIndex, targetEntity, new HealEvent
                {
                    SourceEntity = casterEntity,
                    TargetEntity = targetEntity,
                    Amount = effectiveValue,
                    Tick = CurrentTick
                });
            }

            private static void ApplyBuffEffect(Entity casterEntity, Entity targetEntity, SpellEffect effect, ref DynamicBuffer<BuffApplicationRequest> buffRequests)
            {
                if (targetEntity == Entity.Null)
                {
                    return;
                }

                buffRequests.Add(new BuffApplicationRequest
                {
                    BuffId = effect.BuffId,
                    SourceEntity = casterEntity,
                    DurationOverride = effect.Duration > 0f ? effect.Duration : 0f,
                    StacksToApply = 1
                });
            }

            private void ApplyDispelEffect(Entity targetEntity, SpellEffect effect, int maxDispelCount)
            {
                if (targetEntity == Entity.Null || !ActiveBuffLookup.HasBuffer(targetEntity))
                {
                    return;
                }

                var activeBuffs = ActiveBuffLookup[targetEntity];
                var dispelledCount = 0;

                // Remove buffs matching criteria (dispel debuffs or all)
                // effect.BuffId can specify a category to dispel, or empty for all
                for (int i = activeBuffs.Length - 1; i >= 0 && dispelledCount < maxDispelCount; i--)
                {
                    var buff = activeBuffs[i];

                    // If BuffId is specified, only dispel matching buffs
                    if (effect.BuffId.Length > 0 && !buff.BuffId.Equals(effect.BuffId))
                    {
                        continue;
                    }

                    // Remove the buff
                    activeBuffs.RemoveAtSwapBack(i);
                    dispelledCount++;
                }
            }

            private void ApplyShieldEffect(Entity casterEntity, Entity targetEntity, SpellEffect effect, float effectiveValue, ref DynamicBuffer<BuffApplicationRequest> buffRequests)
            {
                if (targetEntity == Entity.Null)
                {
                    return;
                }

                // Shield is implemented as a special buff with absorption value
                // The buff system will handle the shield logic
                buffRequests.Add(new BuffApplicationRequest
                {
                    BuffId = effect.BuffId.Length > 0 ? effect.BuffId : ShieldDefaultId,
                    SourceEntity = casterEntity,
                    DurationOverride = effect.Duration > 0f ? effect.Duration : 10f,
                    StacksToApply = (byte)math.clamp((int)effectiveValue / 10, 1, 255) // Shield strength as stacks
                });
            }

            private void ApplySummonEffect(Entity casterEntity, float3 targetPosition, SpellEffect effect, EntityCommandBuffer.ParallelWriter ecb, int entityInQueryIndex)
            {
                // Create a summon request that will be processed by a dedicated summon system
                // This avoids instantiating prefabs directly in the job
                var summonRequest = ecb.CreateEntity(entityInQueryIndex);
                ecb.AddComponent(entityInQueryIndex, summonRequest, new SummonRequest
                {
                    SummonerId = casterEntity,
                    SummonTypeId = effect.BuffId, // Reuse BuffId for summon type
                    Position = targetPosition,
                    Duration = effect.Duration,
                    RequestTick = CurrentTick
                });
            }

            private void ApplyTeleportEffect(Entity targetEntity, float3 targetPosition)
            {
                if (targetEntity == Entity.Null || !TransformLookup.HasComponent(targetEntity))
                {
                    return;
                }

                var transform = TransformLookup[targetEntity];
                transform.Position = targetPosition;
                TransformLookup[targetEntity] = transform;
            }

            private void ApplyResourceGrantEffect(Entity targetEntity, SpellEffect effect, float effectiveValue)
            {
                if (targetEntity == Entity.Null)
                {
                    return;
                }

                // For now, resource grants are applied directly to VillagerNeeds if available
                // A more complete implementation would use a resource inventory system
                if (NeedsLookup.HasComponent(targetEntity))
                {
                    var needs = NeedsLookup[targetEntity];
                    
                    // effect.BuffId can specify resource type: "health", "energy", "hunger"
                    if (effect.BuffId.Equals(HealthId))
                    {
                        needs.Health = math.min(100f, needs.Health + effectiveValue);
                    }
                    else if (effect.BuffId.Equals(EnergyId))
                    {
                        needs.Energy = math.min(100f, needs.Energy + effectiveValue);
                    }
                    else if (effect.BuffId.Equals(HungerId))
                    {
                        // Reduce hunger (lower is better)
                        needs.Hunger = (byte)math.clamp(needs.Hunger - (int)effectiveValue, 0, 100);
                    }
                    
                    NeedsLookup[targetEntity] = needs;
                }
            }

            private static void ApplyStatModifyEffect(Entity casterEntity, Entity targetEntity, SpellEffect effect, float effectiveValue, ref DynamicBuffer<BuffApplicationRequest> buffRequests)
            {
                if (targetEntity == Entity.Null)
                {
                    return;
                }

                // Stat modifications are implemented as temporary buffs
                // The buff system handles stat modifiers through buff effects
                buffRequests.Add(new BuffApplicationRequest
                {
                    BuffId = effect.BuffId,
                    SourceEntity = casterEntity,
                    DurationOverride = effect.Duration > 0f ? effect.Duration : 30f, // Default 30 second duration
                    StacksToApply = (byte)math.clamp((int)effectiveValue, 1, 255)
                });
            }
        }
    }

    /// <summary>
    /// Request to summon an entity. Processed by a dedicated summon system.
    /// </summary>
    public struct SummonRequest : IComponentData
    {
        public Entity SummonerId;
        public FixedString64Bytes SummonTypeId;
        public float3 Position;
        public float Duration;
        public uint RequestTick;
    }
}


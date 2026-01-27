using PureDOTS.Runtime.Buffs;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Buffs
{
    /// <summary>
    /// Processes buff duration, expiry, and periodic effects.
    /// Runs after BuffApplicationSystem in GameplaySystemGroup.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(BuffApplicationSystem))]
    public partial struct BuffTickSystem : ISystem
    {
        private ComponentLookup<VillagerNeeds> _needsLookup;
        private BufferLookup<BuffImmunity> _immunityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _needsLookup = state.GetComponentLookup<VillagerNeeds>(false);
            _immunityLookup = state.GetBufferLookup<BuffImmunity>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = timeState.FixedDeltaTime * timeState.CurrentSpeedMultiplier;
            var currentTick = timeState.Tick;

            // Get buff catalog for periodic effects
            if (!SystemAPI.TryGetSingleton<BuffCatalogRef>(out var catalogRef) ||
                !catalogRef.Blob.IsCreated)
            {
                return;
            }

            _needsLookup.Update(ref state);
            _immunityLookup.Update(ref state);

            // Process all entities with active buffs
            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new ProcessBuffTicksJob
            {
                Catalog = catalogRef.Blob,
                DeltaTime = deltaTime,
                CurrentTick = currentTick,
                Ecb = ecb,
                NeedsLookup = _needsLookup,
                ImmunityLookup = _immunityLookup
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct ProcessBuffTicksJob : IJobEntity
        {
            [ReadOnly]
            public BlobAssetReference<BuffDefinitionBlob> Catalog;

            public float DeltaTime;
            public uint CurrentTick;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [NativeDisableParallelForRestriction] public ComponentLookup<VillagerNeeds> NeedsLookup;
            [ReadOnly] public BufferLookup<BuffImmunity> ImmunityLookup;

            void Execute(
                Entity entity,
                [EntityIndexInQuery] int entityInQueryIndex,
                ref DynamicBuffer<ActiveBuff> activeBuffs)
            {
                ref var catalog = ref Catalog.Value;

                // Process buffs in reverse order so we can safely remove expired ones
                for (int i = activeBuffs.Length - 1; i >= 0; i--)
                {
                    var buff = activeBuffs[i];

                    // Find buff definition
                    int buffIndex = -1;
                    for (int j = 0; j < catalog.Buffs.Length; j++)
                    {
                        if (catalog.Buffs[j].BuffId.Equals(buff.BuffId))
                        {
                            buffIndex = j;
                            break;
                        }
                    }

                    if (buffIndex < 0)
                    {
                        // Buff definition not found, remove it
                        activeBuffs.RemoveAtSwapBack(i);
                        continue;
                    }

                    ref var buffDef = ref catalog.Buffs[buffIndex];

                    // Check immunity
                    if (IsImmuneTo(entity, buff.BuffId))
                    {
                        activeBuffs.RemoveAtSwapBack(i);
                        continue;
                    }

                    // Update duration (if not permanent)
                    if (buffDef.BaseDuration > 0f)
                    {
                        buff.RemainingDuration -= DeltaTime;
                        if (buff.RemainingDuration <= 0f)
                        {
                            // Buff expired - emit event via ECB
                            Ecb.AppendToBuffer(entityInQueryIndex, entity, new BuffEvent
                            {
                                BuffId = buff.BuffId,
                                EventType = BuffEventType.Expired,
                                Tick = CurrentTick
                            });
                            activeBuffs.RemoveAtSwapBack(i);
                            continue;
                        }
                    }

                    // Handle stacking behavior - refresh duration if same buff applied again
                    // This is handled in BuffApplicationSystem, but we check for over-stacking here
                    if (buffDef.MaxStacks > 0 && buff.CurrentStacks > buffDef.MaxStacks)
                    {
                        buff.CurrentStacks = buffDef.MaxStacks;
                    }

                    // Process periodic effects
                    if (buffDef.TickInterval > 0f && buffDef.PeriodicEffects.Length > 0)
                    {
                        buff.TimeSinceLastTick += DeltaTime;
                        if (buff.TimeSinceLastTick >= buffDef.TickInterval)
                        {
                            // Trigger periodic effects
                            int tickCount = (int)(buff.TimeSinceLastTick / buffDef.TickInterval);
                            buff.TimeSinceLastTick -= tickCount * buffDef.TickInterval;

                            // Apply periodic effects (scaled by stacks)
                            for (int p = 0; p < buffDef.PeriodicEffects.Length; p++)
                            {
                                var periodic = buffDef.PeriodicEffects[p];
                                float effectValue = periodic.Value * buff.CurrentStacks * tickCount;

                                ApplyPeriodicEffect(entity, entityInQueryIndex, periodic.Type, effectValue, buff.SourceEntity);
                            }
                        }
                    }

                    activeBuffs[i] = buff;
                }
            }

            private bool IsImmuneTo(Entity entity, FixedString64Bytes buffId)
            {
                if (!ImmunityLookup.HasBuffer(entity))
                {
                    return false;
                }

                var immunities = ImmunityLookup[entity];
                for (int i = 0; i < immunities.Length; i++)
                {
                    if (immunities[i].BuffId.Equals(buffId) && immunities[i].ExpirationTick > CurrentTick)
                    {
                        return true;
                    }
                }
                return false;
            }

            private void ApplyPeriodicEffect(Entity entity, int entityInQueryIndex, PeriodicEffectType type, float value, Entity sourceEntity)
            {
                switch (type)
                {
                    case PeriodicEffectType.Damage:
                        // Emit damage event for damage processing system
                        Ecb.AppendToBuffer(entityInQueryIndex, entity, new DamageEvent
                        {
                            SourceEntity = sourceEntity,
                            TargetEntity = entity,
                            RawDamage = value,
                            Type = DamageType.Poison, // DoT is typically poison/true damage
                            Tick = CurrentTick,
                            Flags = DamageFlags.DOT
                        });
                        break;

                    case PeriodicEffectType.Heal:
                        // Emit heal event for healing processing system
                        Ecb.AppendToBuffer(entityInQueryIndex, entity, new HealEvent
                        {
                            SourceEntity = sourceEntity,
                            TargetEntity = entity,
                            Amount = value,
                            Tick = CurrentTick
                        });
                        break;

                    case PeriodicEffectType.Mana:
                        // Mana restoration - could be handled by a mana component
                        // For now, emit a generic resource event
                        break;

                    case PeriodicEffectType.Stamina:
                        // Stamina/Energy restoration - apply to VillagerNeeds if available
                        if (NeedsLookup.HasComponent(entity))
                        {
                            var needs = NeedsLookup[entity];
                            needs.Energy = math.min(100f, needs.Energy + value);
                            NeedsLookup[entity] = needs;
                        }
                        break;

                    case PeriodicEffectType.ResourceGrant:
                        // Resource grants would typically go through an inventory system
                        // For now, we apply to hunger reduction (food resources)
                        if (NeedsLookup.HasComponent(entity))
                        {
                            var needs = NeedsLookup[entity];
                            needs.Hunger = (byte)math.clamp(needs.Hunger - (int)value, 0, 100);
                            NeedsLookup[entity] = needs;
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Buff immunity entry - prevents specific buffs from being applied.
    /// </summary>
    public struct BuffImmunity : IBufferElementData
    {
        public FixedString64Bytes BuffId;
        public uint ExpirationTick; // 0 = permanent immunity
    }

    /// <summary>
    /// Event emitted when buff state changes.
    /// </summary>
    public struct BuffEvent : IBufferElementData
    {
        public FixedString64Bytes BuffId;
        public BuffEventType EventType;
        public uint Tick;
    }

    /// <summary>
    /// Type of buff event.
    /// </summary>
    public enum BuffEventType : byte
    {
        Applied = 0,
        Refreshed = 1,
        Stacked = 2,
        Expired = 3,
        Dispelled = 4
    }
}


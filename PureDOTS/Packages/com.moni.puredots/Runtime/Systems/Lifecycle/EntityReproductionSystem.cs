using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Lifecycle;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Villagers;
using Unity.Transforms;

namespace PureDOTS.Systems.Lifecycle
{
    /// <summary>
    /// Handles entity reproduction - processes pregnant entities and spawns children with proper parent relations.
    /// Creates mother-child EntityRelation entries so Family/Dynasty birth handlers can function correctly.
    /// Runs BEFORE FamilyTreeUpdateSystem to ensure parent relations exist when birth handlers query for them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Family.FamilyTreeUpdateSystem))]
    public partial struct EntityReproductionSystem : ISystem
    {
        private int _nextVillagerId;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _nextVillagerId = 1;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;

            // Build offspring prefab lookup if registry exists
            var hasRegistry = SystemAPI.TryGetSingletonBuffer<OffspringPrefabEntry>(out var registryBuffer);
            var capacity = math.max(1, hasRegistry ? registryBuffer.Length : 1);
            NativeHashMap<FixedString64Bytes, Entity> offspringRegistry =
                new NativeHashMap<FixedString64Bytes, Entity>(capacity, Allocator.TempJob);
            if (hasRegistry)
            {
                for (int i = 0; i < registryBuffer.Length; i++)
                {
                    var entry = registryBuffer[i];
                    if (entry.Prefab != Entity.Null && entry.OffspringTypeId.Length > 0 && !offspringRegistry.ContainsKey(entry.OffspringTypeId))
                    {
                        offspringRegistry.Add(entry.OffspringTypeId, entry.Prefab);
                    }
                }
            }

            // Use EntityManager directly since we need immediate access to created entities
            // for adding relations in the same frame (single-threaded job allows this)
            var job = new ProcessPregnanciesJob
            {
                EntityManager = state.EntityManager,
                CurrentTick = timeState.Tick,
                OffspringRegistry = offspringRegistry,
                HasOffspringRegistry = (byte)(hasRegistry ? 1 : 0),
                NextVillagerId = _nextVillagerId
            };
            job.Run();

            _nextVillagerId = job.NextVillagerId;

            offspringRegistry.Dispose();
        }

        // Note: Not Burst-compiled because EntityManager operations are not Burst-compatible
        partial struct ProcessPregnanciesJob : IJobEntity
        {
            public EntityManager EntityManager;
            public uint CurrentTick;
            [ReadOnly] public NativeHashMap<FixedString64Bytes, Entity> OffspringRegistry;
            public byte HasOffspringRegistry;
            public int NextVillagerId;

            void Execute(
                Entity motherEntity,
                ref ReproductionState reproduction,
                in OffspringConfig config,
                in LifecycleState lifecycle)
            {
                // Only process if pregnant
                if (reproduction.IsPregnant == 0)
                    return;

                // Check if gestation period has elapsed
                // Use LastReproductionTick as pregnancy start time (when IsPregnant was set to 1)
                float gestationElapsed = CurrentTick - reproduction.LastReproductionTick;
                if (gestationElapsed < config.GestationTicks)
                    return;

                // Check if can still reproduce (offspring limit)
                if (reproduction.OffspringCount >= reproduction.MaxOffspring)
                {
                    reproduction.IsPregnant = 0;
                    return;
                }

                // Determine number of offspring (random between MinOffspring and MaxOffspring)
                uint seed = (uint)(motherEntity.Index * 31337 + CurrentTick);
                var rng = new Unity.Mathematics.Random(seed);
                byte offspringCount = (byte)math.clamp(
                    rng.NextInt(config.MinOffspring, config.MaxOffspring + 1),
                    1,
                    reproduction.MaxOffspring - reproduction.OffspringCount);

                // Spawn each offspring
                for (byte i = 0; i < offspringCount; i++)
                {
                    Entity childEntity = SpawnChild(motherEntity, config, CurrentTick, seed + i);

                    // Create bidirectional parent-child relations immediately
                    // (using EntityManager directly since we need immediate access)
                    CreateParentChildRelations(motherEntity, childEntity, CurrentTick);
                }

                // Update reproduction state
                reproduction.IsPregnant = 0;
                reproduction.OffspringCount = (byte)(reproduction.OffspringCount + offspringCount);
                reproduction.LastReproductionTick = CurrentTick;
            }

            Entity SpawnChild(Entity motherEntity, in OffspringConfig config, uint currentTick, uint seed)
            {
                Entity childEntity;

                if (TryInstantiateOffspringPrefab(config.OffspringTypeId, out var prefabEntity))
                {
                    childEntity = prefabEntity;
                }
                else
                {
                    // Create child entity directly (not via ECB) so we can add relations immediately
                    // This is acceptable for reproduction since it's rare and single-threaded
                    childEntity = EntityManager.CreateEntity();
                }

                InitializeNewbornEntity(childEntity, motherEntity, currentTick);

                return childEntity;
            }

            void CreateParentChildRelations(Entity mother, Entity child, uint currentTick)
            {
                // Ensure mother has EntityRelation buffer
                if (!EntityManager.HasBuffer<EntityRelation>(mother))
                {
                    EntityManager.AddBuffer<EntityRelation>(mother);
                }

                // Create Child relation on mother (mother -> child)
                var motherRelations = EntityManager.GetBuffer<EntityRelation>(mother);
                bool hasChildRelation = false;
                for (int i = 0; i < motherRelations.Length; i++)
                {
                    if (motherRelations[i].OtherEntity == child && motherRelations[i].Type == RelationType.Child)
                    {
                        hasChildRelation = true;
                        break;
                    }
                }

                if (!hasChildRelation)
                {
                    motherRelations.Add(new EntityRelation
                    {
                        OtherEntity = child,
                        Type = RelationType.Child,
                        Intensity = 80, // High positive for parent-child
                        InteractionCount = 0,
                        FirstMetTick = currentTick,
                        LastInteractionTick = currentTick,
                        Trust = 90,
                        Familiarity = 100,
                        Respect = 50,
                        Fear = 0
                    });
                }

                // Create Parent relation on child (child -> mother)
                // Critical: This must exist for birth handlers to find the parent
                var childRelations = EntityManager.GetBuffer<EntityRelation>(child);
                bool hasParentRelation = false;
                for (int i = 0; i < childRelations.Length; i++)
                {
                    if (childRelations[i].OtherEntity == mother && childRelations[i].Type == RelationType.Parent)
                    {
                        hasParentRelation = true;
                        break;
                    }
                }

                if (!hasParentRelation)
                {
                    childRelations.Add(new EntityRelation
                    {
                        OtherEntity = mother,
                        Type = RelationType.Parent,
                        Intensity = 80, // High positive for parent-child
                        InteractionCount = 0,
                        FirstMetTick = currentTick,
                        LastInteractionTick = currentTick,
                        Trust = 90,
                        Familiarity = 100,
                        Respect = 70,
                        Fear = 0
                    });
                }
            }

            bool TryInstantiateOffspringPrefab(in FixedString64Bytes typeId, out Entity childEntity)
            {
                childEntity = Entity.Null;
                if (HasOffspringRegistry == 0 || typeId.Length == 0 || !OffspringRegistry.IsCreated)
                {
                    return false;
                }

                if (OffspringRegistry.TryGetValue(typeId, out var prefab) && prefab != Entity.Null)
                {
                    childEntity = EntityManager.Instantiate(prefab);
                    return true;
                }

                return false;
            }

            void InitializeNewbornEntity(Entity childEntity, Entity motherEntity, uint currentTick)
            {
                EnsureLifecycleState(childEntity, currentTick);
                CopyParentTransform(childEntity, motherEntity);
                EnsureRelationBuffer(childEntity);

                // Assign villager ID (inherit faction from mother if available)
                var factionId = 0;
                if (EntityManager.HasComponent<VillagerId>(motherEntity))
                {
                    var parentId = EntityManager.GetComponentData<VillagerId>(motherEntity);
                    factionId = parentId.FactionId;
                }

                var villagerId = new VillagerId
                {
                    Value = NextVillagerId++,
                    FactionId = factionId
                };
                AddOrSetComponent(childEntity, villagerId);

                EnsureVillagerStats(childEntity, currentTick);
                EnsureVillagerNeeds(childEntity, motherEntity);
                EnsureVillagerBelief(childEntity, motherEntity, currentTick);
                EnsureVillagerAlignment(childEntity, motherEntity, currentTick);
                EnsureVillagerBehavior(childEntity, motherEntity, currentTick);
                EnsureVillagerInitiative(childEntity, currentTick);
                EnsureVillagerFlags(childEntity);
            }

            void EnsureLifecycleState(Entity childEntity, uint currentTick)
            {
                var lifecycle = new LifecycleState
                {
                    CurrentStage = LifecycleStage.Nascent,
                    Type = LifecycleType.Linear,
                    StageProgress = 0f,
                    TotalAge = 0f,
                    StageEnteredTick = currentTick,
                    BirthTick = currentTick,
                    StageCount = 0,
                    CanAdvance = 1,
                    IsFrozen = 0
                };

                AddOrSetComponent(childEntity, lifecycle);
            }

            void CopyParentTransform(Entity childEntity, Entity motherEntity)
            {
                float3 position = float3.zero;
                if (EntityManager.HasComponent<LocalTransform>(motherEntity))
                {
                    var motherTransform = EntityManager.GetComponentData<LocalTransform>(motherEntity);
                    position = motherTransform.Position;
                }

                AddOrSetComponent(childEntity, LocalTransform.FromPositionRotationScale(
                    position,
                    quaternion.identity,
                    1f));
            }

            void EnsureRelationBuffer(Entity entity)
            {
                if (EntityManager.HasBuffer<EntityRelation>(entity))
                {
                    var buffer = EntityManager.GetBuffer<EntityRelation>(entity);
                    buffer.Clear();
                }
                else
                {
                    EntityManager.AddBuffer<EntityRelation>(entity);
                }
            }

            void EnsureVillagerStats(Entity childEntity, uint currentTick)
            {
                VillagerStats stats = default;
                if (EntityManager.HasComponent<VillagerStats>(childEntity))
                {
                    stats = EntityManager.GetComponentData<VillagerStats>(childEntity);
                }

                stats.BirthTick = currentTick;
                stats.DeathTick = 0;
                stats.TotalWorkDone = 0f;
                stats.TotalResourcesGathered = 0f;
                stats.EnemiesKilled = 0;
                stats.BuildingsConstructed = 0;
                stats.DistanceTraveled = 0f;

                AddOrSetComponent(childEntity, stats);
            }

            void EnsureVillagerNeeds(Entity childEntity, Entity motherEntity)
            {
                VillagerNeeds needs = default;
                var hasSource = false;
                if (EntityManager.HasComponent<VillagerNeeds>(motherEntity))
                {
                    needs = EntityManager.GetComponentData<VillagerNeeds>(motherEntity);
                    hasSource = true;
                }
                else if (EntityManager.HasComponent<VillagerNeeds>(childEntity))
                {
                    needs = EntityManager.GetComponentData<VillagerNeeds>(childEntity);
                    hasSource = true;
                }

                if (!hasSource)
                {
                    needs.MaxHealth = 100f;
                    needs.Health = 100f;
                    needs.GeneralHealth = 80;
                    needs.Temperature = 20f;
                }

                needs.SetHunger(80f);
                needs.SetEnergy(70f);
                needs.SetMorale(70f);
                needs.GeneralHealth = 80;
                needs.Health = math.clamp(needs.Health, 50f, needs.MaxHealth > 0f ? needs.MaxHealth : 100f);

                AddOrSetComponent(childEntity, needs);
            }

            void EnsureVillagerBelief(Entity childEntity, Entity motherEntity, uint currentTick)
            {
                VillagerBeliefOptimized belief = default;
                if (EntityManager.HasComponent<VillagerBeliefOptimized>(motherEntity))
                {
                    belief = EntityManager.GetComponentData<VillagerBeliefOptimized>(motherEntity);
                    belief.LastUpdateTick = (ushort)math.min(currentTick, ushort.MaxValue);
                }
                else if (EntityManager.HasComponent<VillagerBeliefOptimized>(childEntity))
                {
                    belief = EntityManager.GetComponentData<VillagerBeliefOptimized>(childEntity);
                }
                else
                {
                    belief.PrimaryDeityIndex = 0;
                    belief.Faith = 200;
                    belief.WorshipProgress = 0;
                    belief.Flags = VillagerBeliefFlags.None;
                    belief.LastUpdateTick = (ushort)math.min(currentTick, ushort.MaxValue);
                }

                AddOrSetComponent(childEntity, belief);
            }

            void EnsureVillagerAlignment(Entity childEntity, Entity motherEntity, uint currentTick)
            {
                VillagerAlignment alignment = default;
                if (EntityManager.HasComponent<VillagerAlignment>(motherEntity))
                {
                    alignment = EntityManager.GetComponentData<VillagerAlignment>(motherEntity);
                }
                else if (EntityManager.HasComponent<VillagerAlignment>(childEntity))
                {
                    alignment = EntityManager.GetComponentData<VillagerAlignment>(childEntity);
                }
                else
                {
                    alignment.MoralAxis = 20;
                    alignment.OrderAxis = 10;
                    alignment.PurityAxis = 15;
                    alignment.AlignmentStrength = 0.25f;
                }

                alignment.LastShiftTick = currentTick;
                AddOrSetComponent(childEntity, alignment);
            }

            void EnsureVillagerBehavior(Entity childEntity, Entity motherEntity, uint currentTick)
            {
                VillagerBehavior behavior = default;
                if (EntityManager.HasComponent<VillagerBehavior>(motherEntity))
                {
                    behavior = EntityManager.GetComponentData<VillagerBehavior>(motherEntity);
                }
                else if (EntityManager.HasComponent<VillagerBehavior>(childEntity))
                {
                    behavior = EntityManager.GetComponentData<VillagerBehavior>(childEntity);
                }
                else
                {
                    behavior.VengefulScore = 0;
                    behavior.BoldScore = 0;
                }

                behavior.LastMajorActionTick = currentTick;
                AddOrSetComponent(childEntity, behavior);
            }

            void EnsureVillagerInitiative(Entity childEntity, uint currentTick)
            {
                VillagerInitiativeState initiative = default;
                if (EntityManager.HasComponent<VillagerInitiativeState>(childEntity))
                {
                    initiative = EntityManager.GetComponentData<VillagerInitiativeState>(childEntity);
                }

                initiative.CurrentInitiative = 0.2f;
                initiative.NextActionTick = currentTick + 2000;
                initiative.PendingAction = default;

                AddOrSetComponent(childEntity, initiative);
            }

            void EnsureVillagerFlags(Entity childEntity)
            {
                VillagerFlags flags = default;
                if (EntityManager.HasComponent<VillagerFlags>(childEntity))
                {
                    flags = EntityManager.GetComponentData<VillagerFlags>(childEntity);
                }

                flags.IsDead = false;
                flags.IsIdle = true;
                flags.IsWorking = false;
                flags.IsFleeing = false;

                AddOrSetComponent(childEntity, flags);
            }

            void AddOrSetComponent<T>(Entity entity, T value) where T : unmanaged, IComponentData
            {
                if (EntityManager.HasComponent<T>(entity))
                {
                    EntityManager.SetComponentData(entity, value);
                }
                else
                {
                    EntityManager.AddComponentData(entity, value);
                }
            }
        }
    }
}


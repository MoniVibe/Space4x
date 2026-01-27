using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Consumes queued vegetation spawn commands and instantiates offspring deterministically.
    /// Runs after reproduction to materialize queued entities while remaining rewind-safe.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VegetationSystemGroup))]
    [UpdateAfter(typeof(VegetationReproductionSystem))]
    public partial struct VegetationSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<VegetationSpeciesLookup>();
            state.RequireForUpdate<VegetationSpawnCommandQueue>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var spawnQueueEntity = SystemAPI.GetSingletonEntity<VegetationSpawnCommandQueue>();
            var commands = state.EntityManager.GetBuffer<VegetationSpawnCommand>(spawnQueueEntity);
            if (commands.Length == 0)
            {
                return;
            }

            var speciesLookup = SystemAPI.GetSingleton<VegetationSpeciesLookup>();
            if (!speciesLookup.CatalogBlob.IsCreated)
            {
                return;
            }

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            var commandArray = commands.AsNativeArray();

            for (int i = 0; i < commandArray.Length; i++)
            {
                var command = commandArray[i];
                if (command.SpeciesIndex >= speciesLookup.CatalogBlob.Value.Species.Length)
                {
                    continue;
                }

                ref var species = ref speciesLookup.CatalogBlob.Value.Species[command.SpeciesIndex];

                var newEntity = ecb.CreateEntity();

                var position = command.Position;
                var localTransform = LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f);
                ecb.AddComponent(newEntity, localTransform);

                var childId = (int)math.hash(new uint4(command.ParentId, command.SequenceId, command.IssuedTick, 0x9E3779B9u));

                ecb.AddComponent(newEntity, new VegetationId
                {
                    Value = childId,
                    SpeciesType = (byte)math.min(255, command.SpeciesIndex)
                });

                ecb.AddComponent(newEntity, new VegetationSpeciesIndex
                {
                    Value = command.SpeciesIndex
                });

                ecb.AddComponent(newEntity, new VegetationRandomState
                {
                    GrowthRandomIndex = 0,
                    ReproductionRandomIndex = 0,
                    LootRandomIndex = 0
                });

                ecb.AddComponent(newEntity, new VegetationLifecycle
                {
                    CurrentStage = VegetationLifecycle.LifecycleStage.Seedling,
                    GrowthProgress = 0f,
                    StageTimer = 0f,
                    TotalAge = 0f,
                    GrowthRate = species.BaseGrowthRate
                });

                var reproductionCooldown = species.ReproductionCooldown > 0f ? species.ReproductionCooldown : 1f;
                ecb.AddComponent(newEntity, new VegetationReproduction
                {
                    ReproductionTimer = 0f,
                    ReproductionCooldown = reproductionCooldown,
                    SpreadRange = species.SpreadRadius,
                    SpreadChance = 1f,
                    MaxOffspringRadius = species.GridCellPadding,
                    ActiveOffspring = 0,
                    SpawnSequence = 0
                });

                ecb.AddComponent(newEntity, new VegetationHealth
                {
                    Health = species.MaxHealth,
                    MaxHealth = species.MaxHealth,
                    WaterLevel = species.DesiredMinWater,
                    LightLevel = species.DesiredMinLight,
                    SoilQuality = species.DesiredMinSoilQuality,
                    Temperature = 20f
                });

                ecb.AddComponent(newEntity, new VegetationEnvironmentState
                {
                    Water = species.DesiredMinWater,
                    Light = species.DesiredMinLight,
                    Soil = species.DesiredMinSoilQuality,
                    Pollution = 0f,
                    Wind = 0f,
                    LastSampleTick = timeState.Tick
                });

                var resourceType = command.ResourceTypeId;

                ecb.AddComponent(newEntity, new VegetationProduction
                {
                    ResourceTypeId = resourceType,
                    ProductionRate = 0f,
                    MaxProductionCapacity = species.MaxYieldPerCycle,
                    CurrentProduction = 0f,
                    LastHarvestTime = 0f,
                    HarvestCooldown = species.HarvestCooldown
                });

                ecb.AddComponent(newEntity, new VegetationConsumption
                {
                    WaterConsumptionRate = 0f,
                    NutrientConsumptionRate = 0f,
                    EnergyProductionRate = 0f
                });

                ecb.AddComponent(newEntity, new VegetationSeasonal
                {
                    CurrentSeason = VegetationSeasonal.SeasonType.Spring,
                    SeasonMultiplier = 1f,
                    FrostResistance = 0.5f,
                    DroughtResistance = 0.5f
                });

                ecb.AddComponent(newEntity, new VegetationParent
                {
                    Value = command.Parent
                });

                ecb.AddComponent<VegetationDecayableTag>(newEntity);
                ecb.AddComponent<RewindableTag>(newEntity);

                ecb.AddComponent(newEntity, new VegetationMatureTag());
                ecb.SetComponentEnabled<VegetationMatureTag>(newEntity, false);

                ecb.AddComponent(newEntity, new VegetationReadyToHarvestTag());
                ecb.SetComponentEnabled<VegetationReadyToHarvestTag>(newEntity, false);

                ecb.AddComponent(newEntity, new VegetationDyingTag());
                ecb.SetComponentEnabled<VegetationDyingTag>(newEntity, false);

                ecb.AddComponent(newEntity, new VegetationStressedTag());
                ecb.SetComponentEnabled<VegetationStressedTag>(newEntity, false);

                ecb.AddBuffer<VegetationSeedDrop>(newEntity);
                ecb.AddBuffer<VegetationHistoryEvent>(newEntity);
                ecb.AppendToBuffer(newEntity, new VegetationHistoryEvent
                {
                    Type = VegetationHistoryEvent.EventType.Planted,
                    EventTick = timeState.Tick,
                    Value = 0f
                });
            }

            commands.Clear();
        }
    }
}

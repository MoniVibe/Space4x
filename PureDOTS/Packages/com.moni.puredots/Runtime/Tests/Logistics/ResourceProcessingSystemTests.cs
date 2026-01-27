#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Tests.Logistics
{
    public class ResourceProcessingSystemTests
    {
        [DisableAutoCreation]
        private sealed partial class ProcessingWrapperSystem : SystemBase
        {
            private ResourceProcessingSystem _system;

            protected override void OnCreate()
            {
                base.OnCreate();
                _system = new ResourceProcessingSystem();
                _system.OnCreate(ref CheckedStateRef);
            }

            protected override void OnUpdate()
            {
                _system.OnUpdate(ref CheckedStateRef);
            }
        }

        private World _world;
        private EntityManager _entityManager;
        private ProcessingWrapperSystem _system;
        private BlobAssetReference<ResourceRecipeSetBlob> _recipeSet;
        private BlobAssetReference<ResourceTypeIndexBlob> _resourceCatalog;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ResourceProcessingSystemTests");
            _entityManager = _world.EntityManager;
            World.DefaultGameObjectInjectionWorld = _world;
            _system = _world.GetOrCreateSystemManaged<ProcessingWrapperSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated)
            {
                if (_recipeSet.IsCreated)
                {
                    _recipeSet.Dispose();
                }

                if (_resourceCatalog.IsCreated)
                {
                    _resourceCatalog.Dispose();
                }

                _world.Dispose();
                if (World.DefaultGameObjectInjectionWorld == _world)
                {
                    World.DefaultGameObjectInjectionWorld = null;
                }
            }
        }

        [Test]
        public void CannotConsumeReservedInventory()
        {
            CreateSingletons(RewindMode.Record, tick: 10, fixedDeltaTime: 0.1f);
            CreateResourceTypeIndex("ore", "ingot");
            CreateRecipeSet("recipe", "ore", 3, "ingot", 1);

            var processor = CreateProcessor(autoRun: true, totalStored: 10f);
            AddInventoryItem(processor, "ore", amount: 10f, reserved: 8f);
            AddCapacity(processor, "ingot", maxCapacity: 100f);

            _system.Update();

            var state = _entityManager.GetComponentData<ResourceProcessorState>(processor);
            Assert.AreEqual(0, state.RecipeId.Length);

            var items = _entityManager.GetBuffer<StorehouseInventoryItem>(processor);
            Assert.AreEqual(10f, items[0].Amount, 0.001f);
            Assert.AreEqual(8f, items[0].Reserved, 0.001f);
        }

        [Test]
        public void OutputCapacityBlocksStart()
        {
            CreateSingletons(RewindMode.Record, tick: 10, fixedDeltaTime: 0.1f);
            CreateResourceTypeIndex("ore", "ingot");
            CreateRecipeSet("recipe", "ore", 2, "ingot", 1);

            var processor = CreateProcessor(autoRun: true, totalStored: 6f);
            AddInventoryItem(processor, "ore", amount: 5f, reserved: 0f);
            AddInventoryItem(processor, "ingot", amount: 1f, reserved: 0f);
            AddCapacity(processor, "ingot", maxCapacity: 1f);

            _system.Update();

            var state = _entityManager.GetComponentData<ResourceProcessorState>(processor);
            Assert.AreEqual(0, state.RecipeId.Length);

            var items = _entityManager.GetBuffer<StorehouseInventoryItem>(processor);
            Assert.AreEqual(5f, items[0].Amount, 0.001f);
        }

        [Test]
        public void ProcessingDoesNotMutateDuringPlayback()
        {
            CreateSingletons(RewindMode.Playback, tick: 10, fixedDeltaTime: 0.1f);
            CreateResourceTypeIndex("ore", "ingot");
            CreateRecipeSet("recipe", "ore", 2, "ingot", 1);

            var processor = CreateProcessor(autoRun: true, totalStored: 5f);
            AddInventoryItem(processor, "ore", amount: 5f, reserved: 0f);
            AddCapacity(processor, "ingot", maxCapacity: 10f);

            _system.Update();

            var state = _entityManager.GetComponentData<ResourceProcessorState>(processor);
            Assert.AreEqual(0, state.RecipeId.Length);

            var items = _entityManager.GetBuffer<StorehouseInventoryItem>(processor);
            Assert.AreEqual(5f, items[0].Amount, 0.001f);
        }

        private void CreateSingletons(RewindMode mode, uint tick, float fixedDeltaTime)
        {
            var rewindEntity = _entityManager.CreateEntity(typeof(RewindState));
            _entityManager.SetComponentData(rewindEntity, new RewindState
            {
                Mode = mode
            });

            var tickEntity = _entityManager.CreateEntity(typeof(TickTimeState));
            _entityManager.SetComponentData(tickEntity, new TickTimeState
            {
                Tick = tick,
                FixedDeltaTime = fixedDeltaTime,
                CurrentSpeedMultiplier = 1f,
                TargetTick = tick,
                IsPaused = false,
                IsPlaying = true,
                WorldSeconds = tick * fixedDeltaTime
            });
        }

        private void CreateRecipeSet(string recipeId, string inputId, int inputAmount, string outputId, int outputAmount)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ResourceRecipeSetBlob>();
            builder.Allocate(ref root.Families, 0);
            var recipes = builder.Allocate(ref root.Recipes, 1);
            ref var recipe = ref recipes[0];
            recipe.Id = new FixedString64Bytes(recipeId);
            recipe.Kind = ResourceRecipeKind.Refinement;
            recipe.FacilityTag = default;
            recipe.OutputResourceId = new FixedString64Bytes(outputId);
            recipe.OutputAmount = outputAmount;
            recipe.ProcessSeconds = 0f;
            var ingredients = builder.Allocate(ref recipe.Ingredients, 1);
            ingredients[0] = new ResourceIngredientBlob
            {
                ResourceId = new FixedString64Bytes(inputId),
                Amount = inputAmount
            };

            _recipeSet = builder.CreateBlobAssetReference<ResourceRecipeSetBlob>(Allocator.Persistent);
            var entity = _entityManager.CreateEntity(typeof(ResourceRecipeSet));
            _entityManager.SetComponentData(entity, new ResourceRecipeSet { Value = _recipeSet });
            builder.Dispose();
        }

        private void CreateResourceTypeIndex(params string[] resourceIds)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ResourceTypeIndexBlob>();
            var ids = builder.Allocate(ref root.Ids, resourceIds.Length);
            var displayNames = builder.Allocate(ref root.DisplayNames, resourceIds.Length);
            var colors = builder.Allocate(ref root.Colors, resourceIds.Length);

            for (int i = 0; i < resourceIds.Length; i++)
            {
                var resourceId = new FixedString64Bytes(resourceIds[i]);
                ids[i] = resourceId;
                builder.AllocateString(ref displayNames[i], resourceIds[i]);
                colors[i] = new Color32(0, 0, 0, 0);
            }

            _resourceCatalog = builder.CreateBlobAssetReference<ResourceTypeIndexBlob>(Allocator.Persistent);
            var entity = _entityManager.CreateEntity(typeof(ResourceTypeIndex));
            _entityManager.SetComponentData(entity, new ResourceTypeIndex { Catalog = _resourceCatalog });
            builder.Dispose();
        }

        private Entity CreateProcessor(bool autoRun, float totalStored)
        {
            var entity = _entityManager.CreateEntity(typeof(ResourceProcessorConfig), typeof(ResourceProcessorState), typeof(StorehouseInventory));
            _entityManager.SetComponentData(entity, new ResourceProcessorConfig
            {
                FacilityTag = default,
                AutoRun = (byte)(autoRun ? 1 : 0)
            });
            _entityManager.SetComponentData(entity, new StorehouseInventory
            {
                TotalStored = totalStored,
                TotalCapacity = 100f,
                ItemTypeCount = 0
            });

            _entityManager.AddBuffer<ResourceProcessorQueue>(entity);
            _entityManager.AddBuffer<StorehouseInventoryItem>(entity);
            _entityManager.AddBuffer<StorehouseCapacityElement>(entity);
            _entityManager.AddBuffer<StorehouseReservationItem>(entity);
            return entity;
        }

        private void AddInventoryItem(Entity entity, string resourceId, float amount, float reserved)
        {
            var items = _entityManager.GetBuffer<StorehouseInventoryItem>(entity);
            items.Add(new StorehouseInventoryItem
            {
                ResourceTypeId = new FixedString64Bytes(resourceId),
                Amount = amount,
                Reserved = reserved,
                TierId = (byte)ResourceQualityTier.Unknown,
                AverageQuality = 0
            });
        }

        private void AddCapacity(Entity entity, string resourceId, float maxCapacity)
        {
            var capacities = _entityManager.GetBuffer<StorehouseCapacityElement>(entity);
            capacities.Add(new StorehouseCapacityElement
            {
                ResourceTypeId = new FixedString64Bytes(resourceId),
                MaxCapacity = maxCapacity
            });
        }
    }
}
#endif

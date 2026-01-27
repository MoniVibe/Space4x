using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Contracts;
using Unity.Entities;

namespace PureDOTS.Tests.Contracts
{
    public sealed class ContractTestWorld
    {
        private readonly System.Collections.Generic.List<SystemHandle> _systems;

        public ContractTestWorld(string name)
        {
            World = new World(name);
            World.DefaultGameObjectInjectionWorld = World;
            EntityManager = World.EntityManager;
            _systems = new System.Collections.Generic.List<SystemHandle>();

            CreateSingletons();
        }

        public World World { get; }
        public EntityManager EntityManager { get; }

        public uint CurrentTick
        {
            get
            {
                return EntityManager.CreateEntityQuery(typeof(TimeState)).GetSingleton<TimeState>().Tick;
            }
        }

        public void AddSystem<T>() where T : unmanaged, ISystem
        {
            var handle = World.GetOrCreateSystem<T>();
            _systems.Add(handle);
        }

        public void StepTicks(int count)
        {
            for (int i = 0; i < count; i++)
            {
                AdvanceTick();
                for (int systemIndex = 0; systemIndex < _systems.Count; systemIndex++)
                {
                    _systems[systemIndex].Update(World.Unmanaged);
                }
            }
        }

        public void Dispose()
        {
            if (World.IsCreated)
            {
                if (World.DefaultGameObjectInjectionWorld == World)
                {
                    World.DefaultGameObjectInjectionWorld = null;
                }

                World.Dispose();
            }
        }

        private void CreateSingletons()
        {
            var timeEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(timeEntity, new TimeState
            {
                Tick = 0,
                DeltaTime = 1f / 60f
            });

            var rewindEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(rewindEntity, new RewindState
            {
                Mode = RewindMode.Record
            });

            var harnessEntity = EntityManager.CreateEntity();
            EntityManager.AddComponent<ContractHarnessEnabled>(harnessEntity);

            var violationEntity = EntityManager.CreateEntity(typeof(ContractViolationStream), typeof(ContractViolationRingState));
            EntityManager.SetComponentData(violationEntity, new ContractViolationRingState
            {
                WriteIndex = 0,
                Capacity = 128
            });
            var ringBuffer = EntityManager.AddBuffer<ContractViolationEvent>(violationEntity);
            ringBuffer.ResizeUninitialized(128);
        }

        private void AdvanceTick()
        {
            var timeQuery = EntityManager.CreateEntityQuery(typeof(TimeState));
            var timeState = timeQuery.GetSingleton<TimeState>();
            timeState.Tick += 1;
            timeQuery.SetSingleton(timeState);
        }
    }
}

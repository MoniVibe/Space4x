#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using Unity.Core;
using Unity.Entities;

namespace Space4X.Tests.TestHarness
{
    /// <summary>
    /// Minimal harness for driving ISystem-based tests with a fixed-step pipeline.
    /// </summary>
    public sealed class ISystemTestHarness : IDisposable
    {
        public readonly World World;
        public readonly InitializationSystemGroup Init;
        public readonly SimulationSystemGroup Sim;
        public readonly FixedStepSimulationSystemGroup Fixed;
        public readonly PresentationSystemGroup Pres;

        public ISystemTestHarness(float fixedDelta = 1f / 60f)
        {
            World = new World("ISystemTestWorld");
            Init = World.GetOrCreateSystemManaged<InitializationSystemGroup>();
            Sim = World.GetOrCreateSystemManaged<SimulationSystemGroup>();
            Fixed = World.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            Pres = World.GetOrCreateSystemManaged<PresentationSystemGroup>();

            // Default pipeline: run fixed step as part of both init and sim updates for predictable stepping.
            Init.AddSystemToUpdateList(Fixed);
            Sim.AddSystemToUpdateList(Fixed);

            World.EntityManager.WorldUnmanaged.Time = new TimeData(0, fixedDelta);
        }

        public SystemHandle Add<TSystem>() where TSystem : unmanaged, ISystem
        {
            var handle = World.GetOrCreateSystem<TSystem>();
            Fixed.AddSystemToUpdateList(handle);
            return handle;
        }

        public void Step(int ticks = 1)
        {
            for (int i = 0; i < ticks; i++)
            {
                World.Update();
            }
        }

        public void Dispose()
        {
            if (World.IsCreated)
            {
                World.Dispose();
            }
        }
    }
}
#endif

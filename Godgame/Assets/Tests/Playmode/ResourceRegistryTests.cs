using NUnit.Framework;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Tests
{
    public class ResourceRegistryTests
    {
        private World _world;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Registry World");
            _em = _world.EntityManager;
            _world.CreateSystemManaged<ResourceRegistrySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void Registry_Populates_With_ResourceSites()
        {
            var res = _em.CreateEntity(typeof(ResourceSourceConfig), typeof(ResourceSourceState), typeof(ResourceTypeId), typeof(LocalTransform));
            _em.SetComponentData(res, new ResourceSourceConfig());
            _em.SetComponentData(res, new ResourceSourceState { UnitsRemaining = 10f });
            _em.SetComponentData(res, new ResourceTypeId { Value = new FixedString64Bytes("wood") });
            _em.SetComponentData(res, LocalTransform.FromPosition(new float3(1, 0, 2)));

            var sys = _world.GetExistingSystemManaged<ResourceRegistrySystem>();
            sys.Update(_world.Unmanaged);

            Assert.IsTrue(_em.CreateEntityQuery(ComponentType.ReadOnly<ResourceRegistryTag>()).TryGetSingletonEntity(out var reg));
            var buf = _em.GetBuffer<ResourceRegistryEntry>(reg);
            Assert.GreaterOrEqual(buf.Length, 1);
            Assert.AreEqual(res, buf[0].Entity);
        }
    }
}



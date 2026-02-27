#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using NUnit.Framework;
using PureDOTS.Input;
using PureDOTS.Runtime.Core;
using Space4X.Systems;
using Space4X.UI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Space4X.Tests
{
    public sealed class Space4XRtsSelectionPolicyTests
    {
        private GameObject _cameraObject;
        private bool _restoreRendering;

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_restoreRendering)
            {
                RuntimeMode.ForceRenderingEnabled(false, "RtsSelectionPolicyTestCleanup");
                _restoreRendering = false;
            }
#endif
            Space4XControlModeState.ResetToDefaultForRun();
            if (_cameraObject != null)
            {
                UnityObject.DestroyImmediate(_cameraObject);
                _cameraObject = null;
            }
        }

        [Test]
        public void SelectionBox_PrefersOwned_WhenOwnedAndForeignAreInside()
        {
            using var world = new World("Space4XRtsSelectionPolicyOwnedFirst");
            var entityManager = world.EntityManager;
            EnableRenderingForTest();

            var camera = CreateMainCamera();
            Space4XControlModeState.SetMode(Space4XControlMode.Rts);

            var inputEntity = entityManager.CreateEntity(typeof(RtsInputSingletonTag));
            var boxBuffer = entityManager.AddBuffer<SelectionBoxEvent>(inputEntity);

            var owned = CreateSelectable(entityManager, new float3(-1f, 0f, 0f), 0);
            var foreign = CreateSelectable(entityManager, new float3(1f, 0f, 0f), 1);

            var ownedScreen = camera.WorldToScreenPoint(new Vector3(-1f, 0f, 0f));
            var foreignScreen = camera.WorldToScreenPoint(new Vector3(1f, 0f, 0f));
            boxBuffer.Add(BuildBoxEvent(ownedScreen, foreignScreen, SelectionBoxMode.Replace));

            var system = world.GetOrCreateSystem<Space4XRtsSelectionBoxPolicySystem>();
            system.Update(world.Unmanaged);

            Assert.IsTrue(entityManager.HasComponent<SelectedTag>(owned), "Owned entity should be selected.");
            Assert.IsFalse(entityManager.HasComponent<SelectedTag>(foreign), "Foreign entity should be excluded when owned is present.");
        }

        [Test]
        public void SelectionBox_SelectsForeign_WhenNoOwnedInside()
        {
            using var world = new World("Space4XRtsSelectionPolicyForeignFallback");
            var entityManager = world.EntityManager;
            EnableRenderingForTest();

            var camera = CreateMainCamera();
            Space4XControlModeState.SetMode(Space4XControlMode.Rts);

            var inputEntity = entityManager.CreateEntity(typeof(RtsInputSingletonTag));
            var boxBuffer = entityManager.AddBuffer<SelectionBoxEvent>(inputEntity);

            var ownedOutside = CreateSelectable(entityManager, new float3(-20f, 0f, 0f), 0);
            var foreignInside = CreateSelectable(entityManager, new float3(1f, 0f, 0f), 1);

            var foreignScreen = camera.WorldToScreenPoint(new Vector3(1f, 0f, 0f));
            boxBuffer.Add(BuildBoxEvent(foreignScreen, foreignScreen, SelectionBoxMode.Replace, marginPixels: 20f));

            var system = world.GetOrCreateSystem<Space4XRtsSelectionBoxPolicySystem>();
            system.Update(world.Unmanaged);

            Assert.IsFalse(entityManager.HasComponent<SelectedTag>(ownedOutside), "Owned entity outside box should stay unselected.");
            Assert.IsTrue(entityManager.HasComponent<SelectedTag>(foreignInside), "Foreign entity should be selected when no owned entities are inside.");
        }

        private void EnableRenderingForTest()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _restoreRendering = RuntimeMode.IsRenderingEnabled == false;
            if (_restoreRendering)
            {
                RuntimeMode.ForceRenderingEnabled(true, "RtsSelectionPolicyTest");
            }
#endif
        }

        private UnityEngine.Camera CreateMainCamera()
        {
            _cameraObject = new GameObject("RtsSelectionPolicyTestCamera");
            _cameraObject.tag = "MainCamera";
            var camera = _cameraObject.AddComponent<UnityEngine.Camera>();
            camera.transform.position = new Vector3(0f, 8f, -15f);
            camera.transform.LookAt(Vector3.zero);
            camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            return camera;
        }

        private static Entity CreateSelectable(EntityManager entityManager, float3 position, byte ownerId)
        {
            var entity = entityManager.CreateEntity(typeof(SelectableTag), typeof(SelectionOwner), typeof(LocalTransform));
            entityManager.SetComponentData(entity, new SelectionOwner { PlayerId = ownerId });
            entityManager.SetComponentData(entity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            return entity;
        }

        private static SelectionBoxEvent BuildBoxEvent(Vector3 pointA, Vector3 pointB, SelectionBoxMode mode, float marginPixels = 8f)
        {
            float minX = Mathf.Min(pointA.x, pointB.x) - marginPixels;
            float maxX = Mathf.Max(pointA.x, pointB.x) + marginPixels;
            float minY = Mathf.Min(pointA.y, pointB.y) - marginPixels;
            float maxY = Mathf.Max(pointA.y, pointB.y) + marginPixels;

            return new SelectionBoxEvent
            {
                ScreenMin = new float2(minX, minY),
                ScreenMax = new float2(maxX, maxY),
                Mode = mode,
                PlayerId = 0
            };
        }
    }
}
#endif

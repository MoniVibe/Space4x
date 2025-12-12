using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Shared.Demo
{
    public struct DemoRenderReady : IComponentData {}

    #if SPACE4X_LEGACY_RENDER
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SharedDemoRenderBootstrap : SystemBase
    {
        Entity _lib;
        RenderMeshArray _rma;

        protected override void OnCreate()
        {
            if (SystemAPI.TryGetSingletonEntity<DemoRenderReady>(out _)) { Enabled = false; return; }

            // URP Lit + instancing
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            mat.SetColor("_BaseColor", Color.white);

            // Simple cube mesh
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(tmp);

            _rma = new RenderMeshArray(new[] { mat }, new[] { mesh });

            _lib = EntityManager.CreateEntity();
            EntityManager.AddSharedComponentManaged(_lib, _rma);
            EntityManager.AddComponent<DemoRenderReady>(_lib);

            Debug.Log("[SharedDemoRenderBootstrap] RenderMeshArray ready.");
        }

        protected override void OnUpdate() {}
    }
    #endif

    public static class DemoRenderUtil
    {
        public static void MakeRenderable(EntityManager em, Entity e, float4 rgba, int mat = 0, int mesh = 0)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<DemoRenderReady>());
            if (q.IsEmptyIgnoreFilter) { Debug.LogWarning("[DemoRenderUtil] Missing DemoRenderReady singleton."); return; }
            var lib = q.GetSingletonEntity();
            var rma = em.GetSharedComponentManaged<RenderMeshArray>(lib);

            if (!em.HasComponent<LocalTransform>(e))
                em.AddComponentData(e, LocalTransform.Identity);

            var desc = new RenderMeshDescription(
                shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows: true);

            RenderMeshUtility.AddComponents(
                e, 
                em, 
                desc, 
                rma, 
                MaterialMeshInfo.FromRenderMeshArrayIndices(mat, mesh));

            em.AddComponentData(e, new URPMaterialPropertyBaseColor { Value = rgba });
        }

        public static void MakeRenderable(EntityManager em, Entity e, float3 position, float3 scale, float4 rgba, int mat = 0, int mesh = 0)
        {
            // Apply transform first so the render util doesn't leave identity at origin.
            var lt = LocalTransform.FromPositionRotationScale(
                position,
                quaternion.identity,
                scale.x);

            if (em.HasComponent<LocalTransform>(e))
                em.SetComponentData(e, lt);
            else
                em.AddComponentData(e, lt);

            MakeRenderable(em, e, rgba, mat, mesh);
        }
    }
}

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Rendering;
using Space4X.Registry;

public class FocusFirstRenderable : MonoBehaviour
{
    public float distance = 80f;
    public float height = 40f;

    void LateUpdate()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (!world.IsCreated) return;

        var em = world.EntityManager;

        using var gameplayQuery = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<MaterialMeshInfo>(),
                ComponentType.ReadOnly<LocalToWorld>()
            },
            Any = new[]
            {
                ComponentType.ReadOnly<Carrier>(),
                ComponentType.ReadOnly<MiningVessel>(),
                ComponentType.ReadOnly<Asteroid>()
            }
        });

        NativeArray<Entity> entities;
        if (gameplayQuery.IsEmptyIgnoreFilter)
        {
            using var fallbackQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<MaterialMeshInfo>(),
                ComponentType.ReadOnly<LocalToWorld>());
            entities = fallbackQuery.ToEntityArray(Allocator.Temp);
        }
        else
        {
            entities = gameplayQuery.ToEntityArray(Allocator.Temp);
        }

        try
        {
            if (entities.Length == 0) return;

            var cam = Camera.main;
            if (cam == null) return;

            var camPosition = cam.transform.position;
            float bestDistanceSq = float.MaxValue;
            float3 target = float3.zero;
            var foundTarget = false;

            for (int i = 0; i < entities.Length; i++)
            {
                var ltw = em.GetComponentData<LocalToWorld>(entities[i]);
                var position = ltw.Position;
                var distanceSq = math.distancesq(position, (float3)camPosition);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    target = position;
                    foundTarget = true;
                }
            }

            if (!foundTarget) return;

            Vector3 offset = new Vector3(0f, height, -distance);
            cam.transform.position = (Vector3)target + offset;
            cam.transform.LookAt((Vector3)target);
        }
        finally
        {
            entities.Dispose();
        }
    }
}

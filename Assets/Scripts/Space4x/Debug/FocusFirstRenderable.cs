using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Rendering;

public class FocusFirstRenderable : MonoBehaviour
{
    public float distance = 80f;
    public float height = 40f;

    void LateUpdate()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (!world.IsCreated) return;

        var em = world.EntityManager;

        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<MaterialMeshInfo>(),
            ComponentType.ReadOnly<LocalToWorld>()
        );

        using var entities = query.ToEntityArray(Allocator.Temp);
        if (entities.Length == 0) return;

        var e = entities[0];
        var ltw = em.GetComponentData<LocalToWorld>(e);
        float3 target = ltw.Position;

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 offset = new Vector3(0f, height, -distance);
        cam.transform.position = (Vector3)target + offset;
        cam.transform.LookAt((Vector3)target);
    }
}





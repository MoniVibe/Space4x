using Unity.Entities;
using UnityEngine;
using Space4X.Rendering;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class CheckRenderEntitiesSystem : SystemBase
{
    private float _timer;

    protected override void OnCreate()
    {
        Debug.Log("[CheckRenderEntitiesSystem] Created.");
    }

    protected override void OnUpdate()
    {
        var query = SystemAPI.QueryBuilder().WithAll<RenderKey>().Build();
        int count = query.CalculateEntityCount();
        Debug.Log($"[CheckRenderEntitiesSystem] RenderKey entities count: {count}");
        
        if (count > 0)
        {
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            Debug.Log($"[CheckRenderEntitiesSystem] First entity: {entities[0]}");
        }
    }
}

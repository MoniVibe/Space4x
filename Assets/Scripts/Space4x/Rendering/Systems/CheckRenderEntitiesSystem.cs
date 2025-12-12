using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using Space4X.Rendering;

namespace Space4X.Rendering.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CheckRenderEntitiesSystem : SystemBase
    {
        private EntityQuery _renderKeyQuery;
        private bool _logged;

        protected override void OnCreate()
        {
            Debug.Log("[CheckRenderEntitiesSystem] Created.");
            _renderKeyQuery = SystemAPI.QueryBuilder()
                .WithAll<RenderKey, MaterialMeshInfo>()
                .Build();
        }

        protected override void OnUpdate()
        {
            if (_logged)
                return;

            int count = _renderKeyQuery.CalculateEntityCount();
            Debug.Log($"[CheckRenderEntitiesSystem] RenderKey+MaterialMeshInfo entities: {count}");

            if (count > 0)
            {
                using var entities = _renderKeyQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                Debug.Log($"[CheckRenderEntitiesSystem] First entity: {entities[0]}");
            }

            _logged = true;
        }
    }
}

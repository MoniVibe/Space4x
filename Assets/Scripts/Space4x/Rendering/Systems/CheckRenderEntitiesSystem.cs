using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using Space4X.Rendering;
using PureDOTS.Runtime.Core;

namespace Space4X.Rendering.Systems
{
    using Debug = UnityEngine.Debug;

    
    [UpdateInGroup(typeof(Space4XRenderSystemGroup))]
    [UpdateAfter(typeof(ApplyRenderCatalogSystem))]
    public partial class CheckRenderEntitiesSystem : SystemBase
    {
        private EntityQuery _renderKeyQuery;
        private bool _logged;

        protected override void OnCreate()
        {
            if (RuntimeMode.IsHeadless)
            {
                Enabled = false;
                return;
            }

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
            if (count > 0)
            {
                Debug.Log($"[CheckRenderEntitiesSystem] RenderKey+MaterialMeshInfo entities: {count}");
                using var entities = _renderKeyQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                Debug.Log($"[CheckRenderEntitiesSystem] First entity: {entities[0]}");
                _logged = true;
            }
        }
    }
}

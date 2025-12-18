using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Rendering.Systems
{
    using Debug = UnityEngine.Debug;

    
    [UpdateInGroup(typeof(Space4XRenderSystemGroup))]
    [UpdateAfter(typeof(ApplyRenderVariantSystem))]
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

            UnityDebug.Log("[CheckRenderEntitiesSystem] Created.");
            _renderKeyQuery = GetEntityQuery(
                ComponentType.ReadOnly<RenderKey>(),
                ComponentType.ReadOnly<MaterialMeshInfo>());
        }

        protected override void OnUpdate()
        {
            if (_logged)
                return;

            int count = _renderKeyQuery.CalculateEntityCount();
            if (count > 0)
            {
                UnityDebug.Log($"[CheckRenderEntitiesSystem] RenderKey+MaterialMeshInfo entities: {count}");
                using var entities = _renderKeyQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                UnityDebug.Log($"[CheckRenderEntitiesSystem] First entity: {entities[0]}");
                _logged = true;
            }
        }
    }
}

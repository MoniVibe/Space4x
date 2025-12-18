using PureDOTS.Input;
using PureDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Rendering.Systems
{
    /// <summary>
    /// Editor harness for toggling MeshPresenter ↔ DebugPresenter on a sample entity.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XPresenterToggleSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<RenderSemanticKey>(),
                    ComponentType.ReadOnly<MeshPresenter>(),
                    ComponentType.ReadOnly<DebugPresenter>()
                }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR
            if (!Hotkeys.F7Down())
                return;

            using var entities = _query.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
                return;

            var target = entities[0];
            bool meshEnabled = state.EntityManager.IsComponentEnabled<MeshPresenter>(target);
            bool debugEnabled = state.EntityManager.IsComponentEnabled<DebugPresenter>(target);

            if (meshEnabled || !debugEnabled)
            {
                state.EntityManager.SetComponentEnabled<MeshPresenter>(target, false);
                state.EntityManager.SetComponentEnabled<DebugPresenter>(target, true);
                UnityDebug.Log($"[Space4XPresenterToggleSystem] DebugPresenter enabled for entity {target.Index}.");
            }
            else
            {
                state.EntityManager.SetComponentEnabled<MeshPresenter>(target, true);
                state.EntityManager.SetComponentEnabled<DebugPresenter>(target, false);
                UnityDebug.Log($"[Space4XPresenterToggleSystem] MeshPresenter restored for entity {target.Index}.");
            }
#endif
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}

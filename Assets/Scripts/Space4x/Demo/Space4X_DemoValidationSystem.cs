using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Space4X.Rendering;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Demo
{
    /// <summary>
    /// Lightweight validation to ensure smoke/demo scenes spawn core singletons and renderable entities.
    /// Editor-only logging; runs once per world.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4X_DemoValidationSystem : ISystem
    {
        private bool _validated;

        public void OnCreate(ref SystemState state)
        {
            _validated = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_validated)
            {
                state.Enabled = false;
                return;
            }

#if UNITY_EDITOR
            Validate(ref state);
#endif

            _validated = true;
            state.Enabled = false;
        }

#if UNITY_EDITOR
        private void Validate(ref SystemState state)
        {
            var em = state.EntityManager;

            bool hasTime = state.GetEntityQuery(ComponentType.ReadOnly<TimeState>()).CalculateEntityCount() == 1;
            bool hasTick = state.GetEntityQuery(ComponentType.ReadOnly<TickTimeState>()).CalculateEntityCount() == 1;
            bool hasRewind = state.GetEntityQuery(ComponentType.ReadOnly<RewindState>()).CalculateEntityCount() == 1;
            bool hasSpatial = state.GetEntityQuery(ComponentType.ReadOnly<SpatialGridState>()).CalculateEntityCount() > 0;

            var renderQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<RenderKey>(),
                ComponentType.ReadOnly<LocalTransform>());
            int renderKeyCount = renderQuery.CalculateEntityCount();

            if (!hasTime || !hasTick || !hasRewind)
            {
                Debug.LogError("[Space4X DemoValidation] Missing time/rewind singletons (TimeState/TickTimeState/RewindState)");
            }

            if (!hasSpatial)
            {
                Debug.LogError("[Space4X DemoValidation] Missing SpatialGridState singleton");
            }

            if (renderKeyCount == 0)
            {
                Debug.LogError("[Space4X DemoValidation] No RenderKey entities found; check SubScene authoring & conversion");
            }
            else
            {
                Debug.Log($"[Space4X DemoValidation] RenderKey entities: {renderKeyCount}; Time:{hasTime}, Tick:{hasTick}, Rewind:{hasRewind}, Spatial:{hasSpatial}");
            }
        }
#endif
    }
}


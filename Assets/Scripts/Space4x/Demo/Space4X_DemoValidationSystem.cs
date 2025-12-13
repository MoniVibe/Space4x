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
        private EntityQuery _timeQuery;
        private EntityQuery _tickQuery;
        private EntityQuery _rewindQuery;
        private EntityQuery _spatialQuery;
        private EntityQuery _renderQuery;
        private EntityQuery _demoMarkerQuery;
        private bool _validated;

        public void OnCreate(ref SystemState state)
        {
            _timeQuery = state.GetEntityQuery(ComponentType.ReadOnly<TimeState>());
            _tickQuery = state.GetEntityQuery(ComponentType.ReadOnly<TickTimeState>());
            _rewindQuery = state.GetEntityQuery(ComponentType.ReadOnly<RewindState>());
            _spatialQuery = state.GetEntityQuery(ComponentType.ReadOnly<SpatialGridState>());
            _renderQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<RenderKey>(),
                ComponentType.ReadOnly<LocalTransform>());
            _demoMarkerQuery = state.GetEntityQuery(ComponentType.ReadOnly<Space4XDemoMarker>());
            _validated = false;
        }

        public void OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR
            if (_validated)
            {
                state.Enabled = false;
                return;
            }

            if (_demoMarkerQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            Validate();
            if (_validated)
            {
                state.Enabled = false;
            }
#else
            state.Enabled = false;
#endif
        }

#if UNITY_EDITOR
        private void Validate()
        {
            bool hasTime = !_timeQuery.IsEmptyIgnoreFilter;
            bool hasTick = !_tickQuery.IsEmptyIgnoreFilter;
            bool hasRewind = !_rewindQuery.IsEmptyIgnoreFilter;
            bool hasSpatial = !_spatialQuery.IsEmptyIgnoreFilter;
            int renderKeyCount = _renderQuery.CalculateEntityCount();

            bool success = true;

            if (!hasTime || !hasTick || !hasRewind)
            {
                Debug.LogError("[Space4X DemoValidation] Missing time/rewind singletons (TimeState/TickTimeState/RewindState)");
                success = false;
            }

            if (!hasSpatial)
            {
                Debug.LogError("[Space4X DemoValidation] Missing SpatialGridState singleton");
                success = false;
            }

            if (renderKeyCount == 0)
            {
                Debug.LogError("[Space4X DemoValidation] No RenderKey entities found; check SubScene authoring & conversion");
                success = false;
            }
            else
            {
                Debug.Log($"[Space4X DemoValidation] RenderKey entities: {renderKeyCount}; Time:{hasTime}, Tick:{hasTick}, Rewind:{hasRewind}, Spatial:{hasSpatial}");
            }

            if (success)
            {
                _validated = true;
            }
        }
#endif
    }
}









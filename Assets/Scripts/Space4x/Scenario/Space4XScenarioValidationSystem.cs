using PureDOTS.Rendering;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Space4X.Runtime;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Scenario
{
    /// <summary>
    /// Lightweight validation to ensure smoke/legacy scenes spawn core singletons and renderable entities.
    /// Editor-only logging; runs once per world.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XScenarioValidationSystem : ISystem
    {
        private EntityQuery _timeQuery;
        private EntityQuery _tickQuery;
        private EntityQuery _rewindQuery;
        private EntityQuery _spatialQuery;
        private EntityQuery _renderQuery;
        private EntityQuery _scenarioMarkerQuery;
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
            _scenarioMarkerQuery = state.GetEntityQuery(ComponentType.ReadOnly<Space4XScenarioMarker>());
            state.RequireForUpdate<Space4XScenarioMarker>();
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

            if (_scenarioMarkerQuery.IsEmptyIgnoreFilter)
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
                UnityDebug.LogError("[Space4X ScenarioValidation] Missing time/rewind singletons (TimeState/TickTimeState/RewindState)");
                success = false;
            }

            if (!hasSpatial)
            {
                UnityDebug.LogError("[Space4X ScenarioValidation] Missing SpatialGridState singleton");
                success = false;
            }

            if (renderKeyCount == 0)
            {
                UnityDebug.LogError("[Space4X ScenarioValidation] No RenderKey entities found; check SubScene authoring & conversion");
                success = false;
            }
            else
            {
                UnityDebug.Log($"[Space4X ScenarioValidation] RenderKey entities: {renderKeyCount}; Time:{hasTime}, Tick:{hasTick}, Rewind:{hasRewind}, Spatial:{hasSpatial}");
            }

            if (success)
            {
                _validated = true;
            }
        }
#endif
    }
}

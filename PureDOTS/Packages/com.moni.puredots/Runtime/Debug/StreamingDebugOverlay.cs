using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Streaming;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Debugging
{
    /// <summary>
    /// Lightweight OnGUI overlay for streaming statistics. Attach in scenes for quick telemetry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreamingDebugOverlay : MonoBehaviour
    {
        public bool show = true;
        public Vector2 position = new Vector2(12f, 12f);

        private World _world;
        private Entity _coordinator;
        private EntityManager _entityManager;
        private readonly List<string> _cooldownPreview = new List<string>(4);
        private const int CooldownPreviewLimit = 3;

        private void Awake()
        {
            TryResolveWorld();
        }

        private void Update()
        {
            if (!HasWorld())
            {
                TryResolveWorld();
            }
        }

        private void OnGUI()
        {
            if (!show || !HasEntityManager() || _coordinator == Entity.Null || !_entityManager.Exists(_coordinator))
            {
                return;
            }

            var stats = _entityManager.GetComponentData<StreamingStatistics>(_coordinator);
            var coordinator = _entityManager.GetComponentData<StreamingCoordinator>(_coordinator);
            uint currentTick = TryGetCurrentTick();

            string FormatTick(uint tick) => tick == StreamingStatistics.TickUnset ? "-" : tick.ToString();

            var rect = new Rect(position.x, position.y, 320f, 190f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("Streaming Stats");
            GUILayout.Label($"Tick: {currentTick}");
            GUILayout.Label($"Desired: {stats.DesiredCount}  Loaded: {stats.LoadedCount}");
            GUILayout.Label($"Loading: {stats.LoadingCount}  Unloading: {stats.UnloadingCount}");
            GUILayout.Label($"Queued Load/Unload: {stats.QueuedLoads}/{stats.QueuedUnloads} (Pending {stats.PendingCommands}, Peak {stats.PeakPendingCommands})");
            GUILayout.Label($"First Load Tick: {FormatTick(stats.FirstLoadTick)}  First Unload Tick: {FormatTick(stats.FirstUnloadTick)}");
            GUILayout.Label($"MaxConcurrent: {coordinator.MaxConcurrentLoads}  MaxTick L/U: {coordinator.MaxLoadsPerTick}/{coordinator.MaxUnloadsPerTick}");

            if (stats.ActiveCooldowns > 0)
            {
                CollectCooldownPreview(currentTick);
                GUILayout.Label($"Cooldowns Active: {stats.ActiveCooldowns}" +
                                (_cooldownPreview.Count > 0 ? $" ({string.Join(", ", _cooldownPreview)})" : string.Empty));
                if (GUILayout.Button("Clear Cooldowns"))
                {
                    ClearCooldowns();
                }
            }
            else
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUI.enabled = false;
                    GUILayout.Button("Clear Cooldowns");
                    GUI.enabled = true;
                }
            }

            GUILayout.EndArea();
        }

        private void TryResolveWorld()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (!HasWorld())
            {
                _entityManager = default;
                _coordinator = Entity.Null;
                return;
            }

            _entityManager = _world.EntityManager;
            _coordinator = ResolveCoordinatorEntity();
        }

        private uint TryGetCurrentTick()
        {
            if (!HasEntityManager())
            {
                return 0;
            }

            using var timeQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            if (timeQuery.IsEmptyIgnoreFilter)
            {
                return 0;
            }

            var timeState = timeQuery.GetSingleton<TimeState>();
            return timeState.Tick;
        }

        private void CollectCooldownPreview(uint currentTick)
        {
            _cooldownPreview.Clear();

            if (!HasEntityManager())
            {
                return;
            }

            using var query = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StreamingSectionDescriptor>(),
                    ComponentType.ReadOnly<StreamingSectionState>()
                }
            });

            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length && _cooldownPreview.Count < CooldownPreviewLimit; i++)
            {
                var entity = entities[i];
                var state = _entityManager.GetComponentData<StreamingSectionState>(entity);
                if (state.CooldownUntilTick <= currentTick)
                {
                    continue;
                }

                var descriptor = _entityManager.GetComponentData<StreamingSectionDescriptor>(entity);
                _cooldownPreview.Add(descriptor.Identifier.ToString());
            }
        }

        private void ClearCooldowns()
        {
            if (!HasEntityManager())
            {
                return;
            }

            using var query = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StreamingSectionDescriptor>(),
                    ComponentType.ReadWrite<StreamingSectionState>()
                }
            });

            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var state = _entityManager.GetComponentData<StreamingSectionState>(entity);
                if (state.CooldownUntilTick == 0)
                {
                    continue;
                }

                state.CooldownUntilTick = 0;
                if (state.Status == StreamingSectionStatus.Error)
                {
                    state.Status = StreamingSectionStatus.Unloaded;
                }
                _entityManager.SetComponentData(entity, state);
            }
        }

        private bool HasWorld()
        {
            return _world != null && _world.IsCreated;
        }

        private bool HasEntityManager()
        {
            return HasWorld();
        }

        private Entity ResolveCoordinatorEntity()
        {
            if (!HasEntityManager())
            {
                return Entity.Null;
            }

            using var coordinatorQuery = _entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StreamingCoordinator>(),
                    ComponentType.ReadOnly<StreamingStatistics>()
                }
            });

            return coordinatorQuery.IsEmptyIgnoreFilter
                ? Entity.Null
                : coordinatorQuery.GetSingletonEntity();
        }
    }
}

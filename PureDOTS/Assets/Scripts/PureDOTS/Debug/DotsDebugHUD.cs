using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Debugging
{
    /// <summary>
    /// Simple on-screen HUD that displays key DOTS singleton data for debugging.
    /// Attach this to any GameObject in a scene when runtime diagnostics are desired.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DotsDebugHUD : MonoBehaviour
    {
        public bool showTime = true;
        public bool showVillagerCounts = true;
        public bool showStorehouseTotals = true;
        public Vector2 padding = new Vector2(10f, 10f);

        private World _world;
        private EntityQuery _timeQuery;
        private EntityQuery _rewindQuery;
        private EntityQuery _villagerQuery;
        private EntityQuery _storehouseQuery;

        private void Awake()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                Debug.LogWarning("DotsDebugHUD did not find a DefaultGameObjectInjectionWorld.", this);
                enabled = false;
                return;
            }

            var entityManager = _world.EntityManager;
            _timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            _rewindQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            _villagerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VillagerId>());
            _storehouseQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<StorehouseInventory>());
        }

        private void OnGUI()
        {
            if (_world == null || !_world.IsCreated)
            {
                return;
            }

            var rect = new Rect(padding.x, padding.y, 300f, Screen.height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            if (showTime && !_timeQuery.IsEmptyIgnoreFilter)
            {
                var time = _timeQuery.GetSingleton<TimeState>();
                GUILayout.Label("Time State", HudStyles.BoldLabel);
                GUILayout.Label($"Tick: {time.Tick}");
                GUILayout.Label($"Fixed DT: {time.FixedDeltaTime:F4}");
                GUILayout.Label($"Speed: {time.CurrentSpeedMultiplier:F2}");
                GUILayout.Label(time.IsPaused ? "Paused" : "Running");
                GUILayout.Space(6f);
            }

            if (showTime && !_rewindQuery.IsEmptyIgnoreFilter)
            {
                var rewind = _rewindQuery.GetSingleton<RewindState>();
                GUILayout.Label("Rewind State", HudStyles.BoldLabel);
                GUILayout.Label($"Mode: {rewind.Mode}");
                GUILayout.Label($"Playback Tick: {rewind.PlaybackTick}");
                GUILayout.Space(6f);
            }

            if (showVillagerCounts)
            {
                GUILayout.Label("Villagers", HudStyles.BoldLabel);
                GUILayout.Label($"Count: {_villagerQuery.CalculateEntityCount()}");
                GUILayout.Space(6f);
            }

            if (showStorehouseTotals && !_storehouseQuery.IsEmptyIgnoreFilter)
            {
                var inventories = _storehouseQuery.ToComponentDataArray<StorehouseInventory>(Allocator.Temp);
                float totalStored = 0f;
                for (int i = 0; i < inventories.Length; i++)
                {
                    totalStored += inventories[i].TotalStored;
                }

                GUILayout.Label("Storehouses", HudStyles.BoldLabel);
                GUILayout.Label($"Count: {inventories.Length}");
                GUILayout.Label($"Total Stored: {totalStored:F1}");
                inventories.Dispose();
            }

            GUILayout.EndArea();
        }

#if UNITY_EDITOR
        private static class HudStyles
        {
            public static readonly GUIStyle BoldLabel = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };
        }
#else
        private static class HudStyles
        {
            public static readonly GUIStyle BoldLabel = GUI.skin.label;
        }
#endif
    }
}

using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using HandInteractionState = PureDOTS.Runtime.Components.HandState;

namespace PureDOTS.Debugging
{
    /// <summary>
    /// DEPRECATED: Runtime hybrid MonoBehaviour that queries ECS world directly.
    /// See Docs/DeprecationList.md for migration to pure DOTS debug flow.
    /// 
    /// This component violates the pure DOTS architecture by running simulation queries
    /// from GameObject/MonoBehaviour space. Use DebugDisplaySystem and DebugDisplayData
    /// singleton instead.
    /// </summary>
    [DisallowMultipleComponent]
    [System.Obsolete("This MonoBehaviour violates pure DOTS architecture. See Docs/DeprecationList.md for the replacement pattern using DebugDisplaySystem and DebugDisplayData singleton.")]
    public sealed class DotsDebugHUD : MonoBehaviour
    {
        public bool showTime = true;
        public bool showVillagerCounts = true;
        public bool showStorehouseTotals = true;
        public bool showHand = true;
        public Vector2 padding = new Vector2(10f, 10f);

        private World _world;
        private EntityQuery _tickTimeQuery;
        private EntityQuery _timeQuery;
        private EntityQuery _rewindQuery;
        private EntityQuery _villagerQuery;
        private EntityQuery _storehouseQuery;
        private DivineHandEventBridge _handBridge;
        private HandInteractionState _handState = HandInteractionState.Idle;
        private int _handAmount;
        private int _handCapacity;
        private ushort _handType = DivineHandConstants.NoResourceType;

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
            _tickTimeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TickTimeState>());
            _timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            _rewindQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>());
            _villagerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VillagerId>());
            _storehouseQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<StorehouseInventory>());

            AttachHandBridge();
        }

        private void OnEnable()
        {
            AttachHandBridge();
        }

        private void OnDisable()
        {
            DetachHandBridge();
        }

        private void OnDestroy()
        {
            DetachHandBridge();
        }

        private void OnGUI()
        {
            if (_world == null || !_world.IsCreated)
            {
                return;
            }

            var rect = new Rect(padding.x, padding.y, 300f, Screen.height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            if (showTime && !_tickTimeQuery.IsEmptyIgnoreFilter)
            {
                var time = _tickTimeQuery.GetSingleton<TickTimeState>();
                GUILayout.Label("Time State", HudStyles.BoldLabel);
                GUILayout.Label($"Tick: {time.Tick} / Target: {time.TargetTick}");
                GUILayout.Label($"Fixed DT: {time.FixedDeltaTime:F4}");
                GUILayout.Label($"Speed: {time.CurrentSpeedMultiplier:F2}");
                GUILayout.Label(time.IsPlaying ? (time.IsPaused ? "Paused" : "Playing") : "Stopped");
                GUILayout.Space(6f);
            }
            else if (showTime && !_timeQuery.IsEmptyIgnoreFilter)
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
                uint viewTick = 0;
                if (!_tickTimeQuery.IsEmptyIgnoreFilter)
                {
                    viewTick = _tickTimeQuery.GetSingleton<TickTimeState>().Tick;
                }
                else if (!_timeQuery.IsEmptyIgnoreFilter)
                {
                    viewTick = _timeQuery.GetSingleton<TimeState>().Tick;
                }
                GUILayout.Label("Rewind State", HudStyles.BoldLabel);
                GUILayout.Label($"Mode: {rewind.Mode}");
                GUILayout.Label($"Playback Tick: {viewTick}");
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

            if (showHand && _handBridge != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Divine Hand", HudStyles.BoldLabel);
                GUILayout.Label($"State: {_handState}");
                GUILayout.Label($"Held: {_handAmount}/{Mathf.Max(1, _handCapacity)}");
                GUILayout.Label($"Resource: {(_handType == DivineHandConstants.NoResourceType ? "None" : _handType.ToString())}");
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

        void AttachHandBridge()
        {
            if (_handBridge != null) return;
            _handBridge = FindObjectOfType<DivineHandEventBridge>();
            if (_handBridge == null) return;

            _handBridge.HandStateChanged += HandleHandStateChanged;
            _handBridge.HandAmountChanged += HandleHandAmountChanged;
            _handBridge.HandTypeChanged += HandleHandTypeChanged;
        }

        void DetachHandBridge()
        {
            if (_handBridge == null) return;
            _handBridge.HandStateChanged -= HandleHandStateChanged;
            _handBridge.HandAmountChanged -= HandleHandAmountChanged;
            _handBridge.HandTypeChanged -= HandleHandTypeChanged;
            _handBridge = null;
        }

        void HandleHandStateChanged(HandInteractionState from, HandInteractionState to)
        {
            _handState = to;
        }

        void HandleHandAmountChanged(int amount, int capacity)
        {
            _handAmount = amount;
            _handCapacity = capacity;
        }

        void HandleHandTypeChanged(ushort resourceTypeIndex)
        {
            _handType = resourceTypeIndex;
        }
    }
}

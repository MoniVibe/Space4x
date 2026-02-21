using PureDOTS.Input;
using PureDOTS.Runtime.Core;
using Space4X.UI;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Space4X.Input
{
    /// <summary>
    /// Adds classic RTS command ergonomics for mode 3:
    /// press A to prime attack-move, then left-click to issue.
    /// </summary>
    [DefaultExecutionOrder(-875)]
    [DisallowMultipleComponent]
    public sealed class Space4XRtsClassicCommandMono : MonoBehaviour
    {
        [SerializeField] private Key attackMoveKey = Key.A;
        [SerializeField] private int playerId = 0;
        [SerializeField] private bool requireRtsControlMode = true;

        private World _world;
        private EntityManager _entityManager;
        private EntityQuery _rtsInputQuery;
        private bool _queryReady;

        public static bool IsAttackMovePrimed { get; private set; }

        private void OnDisable()
        {
            DisposeQuery();
            _world = null;
            IsAttackMovePrimed = false;
        }

        private void Update()
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                IsAttackMovePrimed = false;
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }

            if (requireRtsControlMode && Space4XControlModeState.CurrentMode != Space4XControlMode.Rts)
            {
                IsAttackMovePrimed = false;
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            {
                IsAttackMovePrimed = false;
            }

            if (keyboard[attackMoveKey].wasPressedThisFrame && !IsPointerOverUi())
            {
                IsAttackMovePrimed = true;
            }

            if (!IsAttackMovePrimed || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            if (!TryGetInputEntity(out var inputEntity))
            {
                return;
            }

            if (!_entityManager.HasBuffer<RightClickEvent>(inputEntity))
            {
                _entityManager.AddBuffer<RightClickEvent>(inputEntity);
            }

            var events = _entityManager.GetBuffer<RightClickEvent>(inputEntity);
            var screenPos = mouse.position.ReadValue();
            var queue = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            events.Add(new RightClickEvent
            {
                ScreenPos = new float2(screenPos.x, screenPos.y),
                Queue = (byte)(queue ? 1 : 0),
                Ctrl = 1,
                PlayerId = (byte)math.clamp(playerId, 0, 255),
                HasWorldPoint = 0,
                WorldPoint = float3.zero,
                HasHitEntity = 0,
                HitEntity = Entity.Null
            });

            IsAttackMovePrimed = false;
        }

        private bool TryGetInputEntity(out Entity inputEntity)
        {
            inputEntity = Entity.Null;
            if (!TryEnsureQuery())
            {
                return false;
            }

            if (_rtsInputQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            inputEntity = _rtsInputQuery.GetSingletonEntity();
            return inputEntity != Entity.Null;
        }

        private bool TryEnsureQuery()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            if (_queryReady && world == _world)
            {
                return true;
            }

            DisposeQuery();
            _world = world;
            _entityManager = world.EntityManager;
            _rtsInputQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<RtsInputSingletonTag>());
            _queryReady = true;
            return true;
        }

        private void DisposeQuery()
        {
            if (_queryReady)
            {
                _rtsInputQuery.Dispose();
                _queryReady = false;
            }
        }

        private static bool IsPointerOverUi()
        {
            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }
    }
}

using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PureDOTS.Presentation.Runtime
{
    /// <summary>
    /// Simple legacy-input bridge that converts mouse/keyboard interactions into DOTS-friendly hand commands.
    /// Designed to keep the simulation deterministic while we defer any high-fidelity tooling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandInputBridge : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Key _primaryKey = Key.None;
        [SerializeField] private Key _secondaryKey = Key.None;
        [SerializeField] private int _initialCommandCapacity = 16;

        private EntityManager _entityManager;
        private Entity _handEntity;

        private void Awake()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("HandInputBridge could not find a DefaultGameObjectInjectionWorld. Disabling.", this);
                enabled = false;
                return;
            }

            _entityManager = world.EntityManager;
            EnsureHandSingleton();

            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (!EnsureHandSingleton())
            {
                return;
            }

            var buffer = _entityManager.GetBuffer<HandCommand>(_handEntity);

            Vector2 screen = GetScreenPosition();
            buffer.Add(new HandCommand
            {
                Type = HandCommand.CommandType.SetScreenPosition,
                Float2Param = new float2(screen.x, screen.y)
            });

            if (TryResolveWorld(screen, out var worldPosition))
            {
                buffer.Add(new HandCommand
                {
                    Type = HandCommand.CommandType.SetWorldPosition,
                    Float3Param = worldPosition
                });
            }

            if (PrimaryPressedThisFrame())
            {
                buffer.Add(new HandCommand { Type = HandCommand.CommandType.PrimaryDown });
            }
            if (PrimaryReleasedThisFrame())
            {
                buffer.Add(new HandCommand { Type = HandCommand.CommandType.PrimaryUp });
            }
            if (SecondaryPressedThisFrame())
            {
                buffer.Add(new HandCommand { Type = HandCommand.CommandType.SecondaryDown });
            }
            if (SecondaryReleasedThisFrame())
            {
                buffer.Add(new HandCommand { Type = HandCommand.CommandType.SecondaryUp });
            }
        }

        private bool EnsureHandSingleton()
        {
            if (!EntityManagerIsValid(_entityManager))
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    return false;
                }

                _entityManager = world.EntityManager;
            }

            if (!EntityManagerIsValid(_entityManager))
            {
                return false;
            }

            if (_handEntity != Entity.Null && _entityManager.Exists(_handEntity))
            {
                return true;
            }

            var query = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<HandSingletonTag>());
            if (query.TryGetSingletonEntity<HandSingletonTag>(out var entity))
            {
                _handEntity = entity;
                return true;
            }

            _handEntity = _entityManager.CreateEntity(typeof(HandSingletonTag), typeof(HandState));
            var buffer = _entityManager.AddBuffer<HandCommand>(_handEntity);
            buffer.EnsureCapacity(Mathf.Max(_initialCommandCapacity, 1));
            return true;
        }

        private static bool EntityManagerIsValid(EntityManager manager)
        {
            return manager.World != null && manager.World.IsCreated;
        }

        private bool PrimaryPressedThisFrame()
        {
            return Mouse.current?.leftButton.wasPressedThisFrame == true ||
                   IsKeyPressedThisFrame(_primaryKey);
        }

        private bool PrimaryReleasedThisFrame()
        {
            return Mouse.current?.leftButton.wasReleasedThisFrame == true ||
                   IsKeyReleasedThisFrame(_primaryKey);
        }

        private bool SecondaryPressedThisFrame()
        {
            return Mouse.current?.rightButton.wasPressedThisFrame == true ||
                   IsKeyPressedThisFrame(_secondaryKey);
        }

        private bool SecondaryReleasedThisFrame()
        {
            return Mouse.current?.rightButton.wasReleasedThisFrame == true ||
                   IsKeyReleasedThisFrame(_secondaryKey);
        }

        private Vector2 GetScreenPosition()
        {
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }

            return Vector2.zero;
        }

        private bool IsKeyPressedThisFrame(Key key)
        {
            if (key == Key.None)
            {
                return false;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private bool IsKeyReleasedThisFrame(Key key)
        {
            if (key == Key.None)
            {
                return false;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            var control = keyboard[key];
            return control != null && control.wasReleasedThisFrame;
        }

        private bool TryResolveWorld(Vector2 screen, out float3 worldPosition)
        {
            worldPosition = float3.zero;
            var camera = _camera != null ? _camera : Camera.main;
            if (camera == null)
            {
                return false;
            }

            var ray = camera.ScreenPointToRay(screen);
            if (math.abs(ray.direction.y) < 1e-5f)
            {
                return false;
            }

            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f)
            {
                t = 0f;
            }

            var hit = ray.origin + ray.direction * t;
            worldPosition = new float3(hit.x, hit.y, hit.z);
            return true;
        }
    }
}

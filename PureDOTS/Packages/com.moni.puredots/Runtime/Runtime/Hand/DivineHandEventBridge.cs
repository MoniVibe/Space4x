using System;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;
using HandInteractionState = PureDOTS.Runtime.Components.HandState;

namespace PureDOTS.Runtime.Hand
{
    [DisallowMultipleComponent]
    public sealed class DivineHandEventBridge : MonoBehaviour
    {
        [Serializable]
        public sealed class HandStateUnityEvent : UnityEvent<HandInteractionState, HandInteractionState> { }

        [Serializable]
        public sealed class HandTypeUnityEvent : UnityEvent<ushort> { }

        [Serializable]
        public sealed class HandAmountUnityEvent : UnityEvent<int, int> { }

        [Header("Events")]
        [SerializeField] HandStateUnityEvent onHandStateChanged;
        [SerializeField] HandTypeUnityEvent onHandTypeChanged;
        [SerializeField] HandAmountUnityEvent onHandAmountChanged;
        [SerializeField] bool logEvents;

        public event Action<HandInteractionState, HandInteractionState> HandStateChanged;
        public event Action<ushort> HandTypeChanged;
        public event Action<int, int> HandAmountChanged;

        World _world;
        EntityQuery _handQuery;
        bool _queryValid;

        void Awake()
        {
            AcquireWorld();
        }

        void OnEnable()
        {
            AcquireWorld();
        }

        void OnDisable()
        {
            DisposeQuery();
        }

        void OnDestroy()
        {
            DisposeQuery();
        }

        void LateUpdate()
        {
            DispatchEvents();
        }

        void AcquireWorld()
        {
            var newWorld = World.DefaultGameObjectInjectionWorld;
            if (newWorld == null || !newWorld.IsCreated)
            {
                DisposeQuery();
                _world = null;
                return;
            }

            if (_world == newWorld && _queryValid)
            {
                return;
            }

            DisposeQuery();

            _world = newWorld;
            _handQuery = _world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<DivineHandEvent>());
            _queryValid = true;
        }

        void DisposeQuery()
        {
            _queryValid = false;
            _handQuery = default;
        }

        void DispatchEvents()
        {
            if (_world == null || !_world.IsCreated || !_queryValid || _handQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entityManager = _world.EntityManager;
            using var entities = _handQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (!entityManager.HasBuffer<DivineHandEvent>(entity))
                {
                    continue;
                }

                var buffer = entityManager.GetBuffer<DivineHandEvent>(entity);
                for (int i = 0; i < buffer.Length; i++)
                {
                    var evt = buffer[i];
                    switch (evt.Type)
                    {
                        case DivineHandEventType.StateChanged:
                            HandStateChanged?.Invoke(evt.FromState, evt.ToState);
                            onHandStateChanged?.Invoke(evt.FromState, evt.ToState);
                            if (logEvents)
                            {
                                Debug.Log($"[HandEvent] State {evt.FromState} -> {evt.ToState}", this);
                            }
                            break;

                        case DivineHandEventType.TypeChanged:
                            HandTypeChanged?.Invoke(evt.ResourceTypeIndex);
                            onHandTypeChanged?.Invoke(evt.ResourceTypeIndex);
                            if (logEvents)
                            {
                                string label = evt.ResourceTypeIndex == DivineHandConstants.NoResourceType ? "None" : evt.ResourceTypeIndex.ToString();
                                Debug.Log($"[HandEvent] Resource Type => {label}", this);
                            }
                            break;

                        case DivineHandEventType.AmountChanged:
                            HandAmountChanged?.Invoke(evt.Amount, evt.Capacity);
                            onHandAmountChanged?.Invoke(evt.Amount, evt.Capacity);
                            if (logEvents)
                            {
                                Debug.Log($"[HandEvent] Amount {evt.Amount}/{evt.Capacity}", this);
                            }
                            break;
                    }
                }

                buffer.Clear();
            }
        }
    }
}

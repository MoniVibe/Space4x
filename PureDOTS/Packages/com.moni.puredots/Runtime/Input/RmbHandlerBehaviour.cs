using System;
using PureDOTS.Runtime.Camera;
using UnityEngine;

namespace PureDOTS.Input
{
    public abstract class RmbHandlerBehaviour : MonoBehaviour, IRmbHandler
    {
        [Header("Router")]
        [SerializeField] HandCameraInputRouter router;
        [SerializeField] int priority = 50;

        public int Priority => priority;

        public event Action<RmbContext, RmbPhase> RmbInvoked;

        protected virtual void Awake()
        {
            EnsureRouter();
        }

        protected virtual void OnEnable()
        {
            EnsureRouter();
            router?.RegisterHandler(this);
        }

        protected virtual void OnDisable()
        {
            router?.UnregisterHandler(this);
        }

        protected void EnsureRouter()
        {
            if (router != null) return;
            router = GetComponent<HandCameraInputRouter>();
            if (router != null) return;
            router = GetComponentInParent<HandCameraInputRouter>();
            if (router != null) return;
            router = FindFirstObjectByType<HandCameraInputRouter>();
            if (router == null)
            {
                Debug.LogWarning($"{GetType().Name} on {name} could not locate {nameof(HandCameraInputRouter)} in scene.", this);
            }
        }

        protected void Raise(RmbContext context, RmbPhase phase)
        {
            RmbInvoked?.Invoke(context, phase);
        }

        public abstract bool CanHandle(in RmbContext context);

        public abstract void OnRmb(in RmbContext context, RmbPhase phase);
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Config;
using PureDOTS.Runtime.Presentation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Seeds a sample presentation binding set based on runtime config to make graybox visuals available in scenarios and headless runs.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(PresentationBootstrapSystem))]
    public partial struct PresentationBindingSampleBootstrapSystem : ISystem
    {
        private FixedString64Bytes _lastApplied;
        private bool _dirty;
        private bool _shouldRun;
        private BlobAssetReference<PresentationBindingBlob> _activeBinding;

        public void OnCreate(ref SystemState state)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || PureDOTS.Runtime.Core.RuntimeMode.IsHeadless)
            {
                state.Enabled = false;
                return;
            }

            _shouldRun = ShouldRunInWorld(state.WorldUnmanaged);
            if (!_shouldRun)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<PresentationCommandQueue>();
            RuntimeConfigRegistry.Initialize();
            _dirty = false;
            _activeBinding = default;

            if (PresentationBindingConfigVars.BindingSample != null)
            {
                PresentationBindingConfigVars.BindingSample.ValueChanged += OnConfigChanged;
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            if (!_shouldRun)
            {
                return;
            }

            if (PresentationBindingConfigVars.BindingSample != null)
            {
                PresentationBindingConfigVars.BindingSample.ValueChanged -= OnConfigChanged;
            }

            if (SystemAPI.TryGetSingletonEntity<PresentationBindingReference>(out var bindingEntity))
            {
                var bindingRef = state.EntityManager.GetComponentData<PresentationBindingReference>(bindingEntity);
                DisposeActiveBinding(ref state, bindingEntity, ref bindingRef);
            }
            else
            {
                DisposeActiveBinding();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_shouldRun)
            {
                return;
            }

            if (!TryEnsureBindingEntity(ref state, out var bindingEntity))
            {
                return;
            }

            var bindingRef = state.EntityManager.GetComponentData<PresentationBindingReference>(bindingEntity);

            if (!bindingRef.Binding.IsCreated)
            {
                _dirty = true;
            }

            var desiredKey = ResolveDesiredKey();
            bool hasExternalBinding = bindingRef.Binding.IsCreated && _lastApplied.IsEmpty;

            if (hasExternalBinding && !_dirty)
            {
                return;
            }

            if (!_dirty && bindingRef.Binding.IsCreated && _lastApplied.Equals(desiredKey))
            {
                return;
            }

            if (!PresentationBindingSamples.TryBuild(desiredKey.ToString(), Allocator.Persistent, out var blob, out var appliedKey))
            {
                return;
            }

            DisposeActiveBinding(ref state, bindingEntity, ref bindingRef);

            bindingRef.Binding = blob;
            state.EntityManager.SetComponentData(bindingEntity, bindingRef);
            _activeBinding = blob;
            _lastApplied = appliedKey;
            _dirty = false;
        }

        private bool TryEnsureBindingEntity(ref SystemState state, out Entity bindingEntity)
        {
            if (SystemAPI.TryGetSingletonEntity<PresentationBindingReference>(out bindingEntity))
            {
                return true;
            }

            bindingEntity = state.EntityManager.CreateEntity(typeof(PresentationBindingReference));
            return true;
        }

        private static FixedString64Bytes ResolveDesiredKey()
        {
            var value = PresentationBindingConfigVars.BindingSample != null
                ? PresentationBindingConfigVars.BindingSample.Value
                : "graybox-minimal";

            return new FixedString64Bytes((value ?? string.Empty).ToLowerInvariant());
        }

        private void OnConfigChanged(RuntimeConfigVar _)
        {
            _dirty = true;
        }

        private void DisposeActiveBinding(ref SystemState state, Entity bindingEntity, ref PresentationBindingReference bindingRef)
        {
            DisposeActiveBinding();

            if (bindingRef.Binding.IsCreated && state.EntityManager.Exists(bindingEntity))
            {
                bindingRef.Binding = default;
                state.EntityManager.SetComponentData(bindingEntity, bindingRef);
            }
        }

        private void DisposeActiveBinding()
        {
            if (_activeBinding.IsCreated)
            {
                _activeBinding.Dispose();
                _activeBinding = default;
            }
        }

        private static bool ShouldRunInWorld(WorldUnmanaged world)
        {
            var flags = world.Flags;
            // Skip in production/game worlds to avoid leaking into the main runtime profile.
            return (flags & WorldFlags.Game) == 0 || (flags & WorldFlags.Editor) != 0;
        }
    }
}
#endif

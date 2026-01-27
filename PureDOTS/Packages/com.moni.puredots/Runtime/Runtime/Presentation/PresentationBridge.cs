using System.Collections.Generic;
using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Runtime.Presentation
{
    public struct PresentationBridgeStats
    {
        public int EffectsPlayed;
        public int CompanionsSpawned;
        public int Released;
        public int ReusedFromPool;
        public int FailedPlayback;
        public int FailedReleases;
        public int ActiveEffects;
        public int ActiveCompanions;
        public int PooledInstances;
    }

    public readonly struct PresentationBridgeHandle
    {
        public readonly int HandleId;
        public readonly PresentationKind Kind;
        public readonly PresentationStyleBlock Style;

        public PresentationBridgeHandle(int handleId, PresentationKind kind, in PresentationStyleBlock style)
        {
            HandleId = handleId;
            Kind = kind;
            Style = style;
        }

        public bool IsValid => HandleId > 0 && Kind != PresentationKind.None;
    }

    /// <summary>
    /// Hybrid-safe bridge that instantiates pooled placeholder GameObjects driven only by IDs.
    /// </summary>
    public sealed class PresentationBridge : MonoBehaviour
    {
        [SerializeField] bool hidePlaceholdersInHierarchy = true;

        private readonly Dictionary<int, PlaceholderInstance> _active = new();
        private readonly Dictionary<PoolKey, Stack<PlaceholderInstance>> _pool = new(PoolKeyComparer.Instance);
        private int _nextHandleId = 1;
        private PresentationBridgeStats _stats;

        public PresentationBridgeStats Stats => _stats;

        void Awake()
        {
            PresentationBridgeLocator.Register(this);
        }

        void OnDestroy()
        {
            PresentationBridgeLocator.Unregister(this);
            DisposeAll();
        }

        public void ResetState()
        {
            DisposeAll();
            _active.Clear();
            _pool.Clear();
            _stats = default;
            _nextHandleId = 1;
        }

        public PresentationBridgeHandle SpawnCompanion(PresentationKind kind, in PresentationStyleBlock style, in float3 position, in quaternion rotation)
        {
            return Acquire(kind, style, position, rotation, false);
        }

        public PresentationBridgeHandle PlayEffect(PresentationKind kind, in PresentationStyleBlock style, in float3 position, in quaternion rotation)
        {
            return Acquire(kind, style, position, rotation, true);
        }

        public bool ReleaseHandle(int handleId)
        {
            if (!_active.TryGetValue(handleId, out var instance))
            {
                _stats.FailedReleases++;
                return false;
            }

            if (instance.IsEffect)
            {
                _stats.ActiveEffects = math.max(0, _stats.ActiveEffects - 1);
            }
            else
            {
                _stats.ActiveCompanions = math.max(0, _stats.ActiveCompanions - 1);
            }

            _active.Remove(handleId);
            ReturnToPool(instance);
            _stats.Released++;
            return true;
        }

        public bool TryGetInstance(int handleId, out GameObject instance)
        {
            if (_active.TryGetValue(handleId, out var data) && data.GameObject != null)
            {
                instance = data.GameObject;
                return true;
            }

            instance = null;
            return false;
        }

        private PresentationBridgeHandle Acquire(PresentationKind kind, in PresentationStyleBlock style, in float3 position, in quaternion rotation, bool isEffect)
        {
            if (kind == PresentationKind.None)
            {
                _stats.FailedPlayback++;
                return default;
            }

            var resolvedStyle = style;
            resolvedStyle.Size = resolvedStyle.Size > 0f ? resolvedStyle.Size : 1f;
            resolvedStyle.Speed = resolvedStyle.Speed > 0f ? resolvedStyle.Speed : 1f;

            var styleToken = resolvedStyle.Style;
            var poolKey = new PoolKey(kind, styleToken, resolvedStyle.PaletteIndex, resolvedStyle.Size, resolvedStyle.Speed);

            if (!_pool.TryGetValue(poolKey, out var stack))
            {
                stack = new Stack<PlaceholderInstance>();
                _pool[poolKey] = stack;
            }

            PlaceholderInstance instance;
            if (stack.Count > 0)
            {
                instance = stack.Pop();
                _stats.ReusedFromPool++;
                if (_stats.PooledInstances > 0)
                {
                    _stats.PooledInstances--;
                }
            }
            else
            {
                instance = new PlaceholderInstance
                {
                    Kind = kind,
                    Style = resolvedStyle,
                    IsEffect = isEffect,
                    GameObject = CreatePlaceholder(kind, styleToken.ToString())
                };
            }

            if (instance.GameObject == null)
            {
                _stats.FailedPlayback++;
                return default;
            }

            instance.Kind = kind;
            instance.Style = resolvedStyle;
            instance.IsEffect = isEffect;
            var handleId = _nextHandleId++;
            instance.HandleId = handleId;
            _active[handleId] = instance;

            ApplyTransform(instance.GameObject.transform, position, rotation, resolvedStyle.Size);
            instance.GameObject.SetActive(true);
            instance.GameObject.hideFlags = hidePlaceholdersInHierarchy ? HideFlags.HideAndDontSave : HideFlags.None;

            if (isEffect)
            {
                _stats.EffectsPlayed++;
                _stats.ActiveEffects++;
            }
            else
            {
                _stats.CompanionsSpawned++;
                _stats.ActiveCompanions++;
            }

            return new PresentationBridgeHandle(handleId, kind, resolvedStyle);
        }

        private static void ApplyTransform(Transform target, in float3 position, in quaternion rotation, float size)
        {
            target.position = new Vector3(position.x, position.y, position.z);
            target.rotation = new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w);
            float clampedSize = math.max(0.01f, size <= 0f ? 1f : size);
            target.localScale = new Vector3(clampedSize, clampedSize, clampedSize);
        }

        private GameObject CreatePlaceholder(PresentationKind kind, string styleToken)
        {
            GameObject go;
            switch (kind)
            {
                case PresentationKind.Mesh:
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    if (go.TryGetComponent<Collider>(out var collider))
                    {
                        DestroyImmediate(collider);
                    }
                    break;
                case PresentationKind.Particle:
                    go = new GameObject($"ParticlePlaceholder_{styleToken}");
                    go.AddComponent<ParticleSystem>();
                    break;
                case PresentationKind.Vfx:
                    go = new GameObject($"VfxPlaceholder_{styleToken}");
                    go.AddComponent<PresentationVfxStub>();
                    break;
                case PresentationKind.Audio:
                    go = new GameObject($"AudioPlaceholder_{styleToken}");
                    go.AddComponent<AudioSource>();
                    break;
                case PresentationKind.Sfx:
                    go = new GameObject($"SfxPlaceholder_{styleToken}");
                    go.AddComponent<AudioSource>();
                    break;
                case PresentationKind.Ui:
                    go = new GameObject($"UiPlaceholder_{styleToken}");
                    go.AddComponent<RectTransform>();
                    break;
                default:
                    go = new GameObject($"PresentationPlaceholder_{styleToken}");
                    break;
            }

            go.name = $"{kind}_{styleToken}";
            return go;
        }

        private void ReturnToPool(in PlaceholderInstance instance)
        {
            if (instance.GameObject == null)
            {
                return;
            }

            instance.GameObject.SetActive(false);

            var key = new PoolKey(instance.Kind, instance.Style.Style, instance.Style.PaletteIndex, instance.Style.Size, instance.Style.Speed);
            if (!_pool.TryGetValue(key, out var stack))
            {
                stack = new Stack<PlaceholderInstance>();
                _pool[key] = stack;
            }

            stack.Push(instance);
            _stats.PooledInstances++;
        }

        private void DisposeAll()
        {
            foreach (var entry in _active.Values)
            {
                if (entry.GameObject != null)
                    DestroyImmediate(entry.GameObject);
            }

            foreach (var pool in _pool.Values)
            {
                foreach (var entry in pool)
                {
                    if (entry.GameObject != null)
                        DestroyImmediate(entry.GameObject);
                }
            }

            _stats.ActiveCompanions = 0;
            _stats.ActiveEffects = 0;
            _stats.PooledInstances = 0;
        }

        private struct PlaceholderInstance
        {
            public int HandleId;
            public PresentationKind Kind;
            public PresentationStyleBlock Style;
            public bool IsEffect;
            public GameObject GameObject;
        }

        private readonly struct PoolKey
        {
            public readonly PresentationKind Kind;
            public readonly FixedString64Bytes Style;
            public readonly byte PaletteIndex;
            public readonly uint SizeKey;
            public readonly uint SpeedKey;

            public PoolKey(PresentationKind kind, in FixedString64Bytes style, byte paletteIndex, float size, float speed)
            {
                Kind = kind;
                Style = style;
                PaletteIndex = paletteIndex;
                SizeKey = math.asuint(size);
                SpeedKey = math.asuint(speed);
            }
        }

        private sealed class PoolKeyComparer : IEqualityComparer<PoolKey>
        {
            public static readonly PoolKeyComparer Instance = new();

            public bool Equals(PoolKey x, PoolKey y)
            {
                return x.Kind == y.Kind
                       && x.PaletteIndex == y.PaletteIndex
                       && x.SizeKey == y.SizeKey
                       && x.SpeedKey == y.SpeedKey
                       && x.Style.Equals(y.Style);
            }

            public int GetHashCode(PoolKey obj)
            {
                unchecked
                {
                    int hash = (int)obj.Kind;
                    hash = (hash * 397) ^ obj.PaletteIndex;
                    hash = (hash * 397) ^ (int)obj.SizeKey;
                    hash = (hash * 397) ^ (int)obj.SpeedKey;
                    hash = (hash * 397) ^ obj.Style.GetHashCode();
                    return hash;
                }
            }
        }
    }

    public static class PresentationBridgeLocator
    {
        private static PresentationBridge _instance;

        public static void Register(PresentationBridge bridge)
        {
            _instance = bridge;
        }

        public static void Unregister(PresentationBridge bridge)
        {
            if (_instance == bridge)
            {
                _instance = null;
            }
        }

        public static PresentationBridge TryResolve()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = Object.FindFirstObjectByType<PresentationBridge>(FindObjectsInactive.Include);
            return _instance;
        }
    }

    public sealed class PresentationVfxStub : MonoBehaviour
    {
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using PureDOTS.Rendering;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UTime = UnityEngine.Time;

namespace Space4X.Diagnostics
{
    /// <summary>
    /// Lightweight runtime HUD for render preflight counts (F3 toggles visibility).
    /// Mirrors the core counters used by smoke diagnostics systems.
    /// </summary>
    [DefaultExecutionOrder(-9500)]
    [DisallowMultipleComponent]
    public sealed class Space4XRenderPreflightHud : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.5f;

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private Label _label;
        private bool _visible = true;
        private float _nextRefreshAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode || !RuntimeMode.IsRenderingEnabled)
                return;

            if (UnityEngine.Object.FindAnyObjectByType<Space4XRenderPreflightHud>() != null)
                return;

            var go = new GameObject("Space4X Render Preflight HUD");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XRenderPreflightHud>();
        }

        private void OnEnable()
        {
            EnsureUi();
            RefreshNow();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                _visible = !_visible;
                if (_label != null)
                {
                    _label.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            if (!_visible)
                return;

            if (UTime.unscaledTime < _nextRefreshAt)
                return;

            RefreshNow();
        }

        private void EnsureUi()
        {
            if (_document != null)
                return;

            _document = gameObject.GetComponent<UIDocument>();
            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }

            if (_document.panelSettings == null)
            {
                _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _document.panelSettings = _panelSettings;
            }

            var root = _document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;

            _label = new Label();
            _label.style.position = Position.Absolute;
            _label.style.top = 12f;
            _label.style.right = 12f;
            _label.style.width = 430f;
            _label.style.paddingLeft = 10f;
            _label.style.paddingRight = 10f;
            _label.style.paddingTop = 8f;
            _label.style.paddingBottom = 8f;
            _label.style.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 0.82f);
            _label.style.borderTopLeftRadius = 6f;
            _label.style.borderTopRightRadius = 6f;
            _label.style.borderBottomLeftRadius = 6f;
            _label.style.borderBottomRightRadius = 6f;
            _label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _label.style.fontSize = 12;
            _label.style.whiteSpace = WhiteSpace.Normal;
            _label.style.display = DisplayStyle.Flex;
            root.Add(_label);
        }

        private void RefreshNow()
        {
            _nextRefreshAt = UTime.unscaledTime + RefreshIntervalSeconds;

            if (_label == null)
                return;

            var scene = SceneManager.GetActiveScene();
            var sceneName = scene.IsValid() ? scene.name : "<none>";
            var world = World.DefaultGameObjectInjectionWorld;

            if (world == null || !world.IsCreated)
            {
                _label.style.color = new Color(1f, 0.76f, 0.30f, 1f);
                _label.text = $"Render Preflight [{sceneName}]\nWorld: unavailable\nPress F3 to hide";
                return;
            }

            var em = world.EntityManager;
            var catalogCount = Count<RenderPresentationCatalog>(em);
            var semanticCount = Count<RenderSemanticKey>(em);
            var variantCount = Count<RenderVariantKey>(em);
            var meshPresenterCount = Count<MeshPresenter>(em);
            var materialMeshCount = Count<MaterialMeshInfo>(em);
            var renderBoundsCount = Count<RenderBounds>(em);
            var renderFilterCount = CountByType(em, ComponentType.ReadOnly<RenderFilterSettings>());
            var localTransformCount = Count<LocalTransform>(em);
            var localToWorldCount = Count<LocalToWorld>(em);
            var carrierCount = Count<Carrier>(em);
            var miningCount = Count<MiningVessel>(em);
            var asteroidCount = Count<Asteroid>(em);

            var hasGameplay = carrierCount + miningCount + asteroidCount > 0;
            var renderReady = catalogCount > 0 && materialMeshCount > 0 && hasGameplay;
            _label.style.color = renderReady
                ? new Color(0.52f, 0.95f, 0.62f, 1f)
                : new Color(1f, 0.65f, 0.40f, 1f);

            _label.text =
                $"Render Preflight [{sceneName}] {(renderReady ? "OK" : "CHECK")}\n" +
                $"Catalog={catalogCount} Semantic={semanticCount} Variant={variantCount} MeshPresenter={meshPresenterCount}\n" +
                $"MaterialMeshInfo={materialMeshCount} RenderBounds={renderBoundsCount} RenderFilter={renderFilterCount}\n" +
                $"LocalTransform={localTransformCount} LocalToWorld={localToWorldCount}\n" +
                $"Carrier={carrierCount} MiningVessel={miningCount} Asteroid={asteroidCount}\n" +
                "Press F3 to hide";
        }

        private static int Count<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static int CountByType(EntityManager em, ComponentType componentType)
        {
            using var query = em.CreateEntityQuery(componentType);
            return query.CalculateEntityCount();
        }
    }
}
#endif

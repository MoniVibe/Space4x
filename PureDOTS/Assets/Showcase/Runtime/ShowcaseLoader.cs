using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PureDOTS.Showcase
{
    /// <summary>
    /// MonoBehaviour that loads the scenes defined in a <see cref="ShowcaseCatalog"/> additively.
    /// </summary>
    public sealed class ShowcaseLoader : MonoBehaviour
    {
        [SerializeField]
        private ShowcaseCatalog _catalog;

        [SerializeField]
        [Tooltip("Automatically begin loading when the loader's GameObject starts.")]
        private bool _loadOnStart = true;

        [SerializeField]
        [Tooltip("Write basic progress information to the console while loading.")]
        private bool _logProgress = true;

        private Coroutine _loadRoutine;

        private void Start()
        {
            if (_loadOnStart)
            {
                BeginLoading();
            }
        }

        /// <summary>
        /// Triggers additive loading of every scene in the catalog.
        /// </summary>
        public void BeginLoading()
        {
            if (_catalog == null)
            {
                Debug.LogWarning("[ShowcaseLoader] No catalog assigned; nothing to load.", this);
                return;
            }

            if (_loadRoutine != null)
            {
                Debug.LogWarning("[ShowcaseLoader] Load already in progress.", this);
                return;
            }

            _loadRoutine = StartCoroutine(LoadScenesRoutine());
        }

        private IEnumerator LoadScenesRoutine()
        {
            foreach (var entry in _catalog.Scenes)
            {
                if (string.IsNullOrEmpty(entry.ScenePath))
                {
                    continue;
                }

                var existingScene = SceneManager.GetSceneByPath(entry.ScenePath);
                if (existingScene.IsValid() && existingScene.isLoaded)
                {
                    if (_logProgress)
                    {
                        Debug.Log($"[ShowcaseLoader] Scene '{entry.DisplayName}' already loaded.");
                    }

                    continue;
                }

                if (_logProgress)
                {
                    Debug.Log($"[ShowcaseLoader] Loading '{entry.DisplayName}' ({entry.ScenePath})...");
                }

                var asyncOp = SceneManager.LoadSceneAsync(entry.ScenePath, LoadSceneMode.Additive);
                if (asyncOp == null)
                {
                    Debug.LogError($"[ShowcaseLoader] Failed to load scene '{entry.ScenePath}'. Is it added to Build Settings?", this);
                    continue;
                }

                while (!asyncOp.isDone)
                {
                    if (_logProgress)
                    {
                        Debug.Log($"[ShowcaseLoader] '{entry.DisplayName}' progress: {asyncOp.progress:P0}");
                    }

                    yield return null;
                }

                ApplySceneOffset(entry);

                if (_logProgress)
                {
                    Debug.Log($"[ShowcaseLoader] Scene '{entry.DisplayName}' loaded.");
                }

                yield return null;
            }

            _loadRoutine = null;
        }

        private void ApplySceneOffset(ShowcaseCatalog.SceneEntry entry)
        {
            var offset = entry.SceneOffset;
            if (offset == Vector3.zero)
            {
                return;
            }

            var scene = SceneManager.GetSceneByPath(entry.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (_logProgress)
                {
                    Debug.LogWarning($"[ShowcaseLoader] Unable to apply offset for '{entry.DisplayName}' because the scene is not loaded.", this);
                }

                return;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                var transform = root.transform;
                transform.position += offset;
            }

            if (_logProgress)
            {
                Debug.Log($"[ShowcaseLoader] Applied offset {offset} to scene '{entry.DisplayName}'.");
            }
        }
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PureDOTS.Showcase
{
    /// <summary>
    /// Scriptable catalog describing which additive scenes should be loaded to build the showcase.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ShowcaseCatalog",
        menuName = "PureDOTS/Showcase Catalog",
        order = 0)]
    public sealed class ShowcaseCatalog : ScriptableObject
    {
        [SerializeField]
        private List<SceneEntry> _scenes = new();

        /// <summary>
        /// List of additive scenes to load when the showcase boots.
        /// </summary>
        public IReadOnlyList<SceneEntry> Scenes => _scenes;

#if UNITY_EDITOR
        private void OnValidate()
        {
            for (int i = 0; i < _scenes.Count; i++)
            {
                _scenes[i].ApplySceneAssetPath();
            }
        }
#endif

        [Serializable]
        public struct SceneEntry
        {
            [SerializeField]
            [Tooltip("Optional friendly label for documentation/debugging.")]
            private string _displayName;

            [SerializeField]
            [Tooltip("Scene path used at runtime for additive loading."), HideInInspector]
            private string _scenePath;

            [SerializeField]
            [Tooltip("Offset applied to the scene root after it loads additively.")]
            private Vector3 _sceneOffset;

#if UNITY_EDITOR
            [SerializeField]
            [Tooltip("Reference to the scene asset loaded additively at runtime.")]
            private UnityEditor.SceneAsset _sceneAsset;
#endif

            /// <summary>
            /// Friendly label shown in logs.
            /// </summary>
            public string DisplayName => string.IsNullOrWhiteSpace(_displayName)
                ? (string.IsNullOrEmpty(_scenePath) ? string.Empty : Path.GetFileNameWithoutExtension(_scenePath))
                : _displayName;

            /// <summary>
            /// Asset database path to the scene.
            /// </summary>
            public string ScenePath => _scenePath;

            /// <summary>
            /// World-space translation applied to every root GameObject in the loaded scene.
            /// </summary>
            public Vector3 SceneOffset => _sceneOffset;

#if UNITY_EDITOR
            internal void ApplySceneAssetPath()
            {
                if (_sceneAsset == null)
                {
                    return;
                }

                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(_sceneAsset);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    _scenePath = assetPath;
                }
            }
#else
            internal void ApplySceneAssetPath() { }
#endif
        }
    }
}


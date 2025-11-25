using System;
using System.Collections.Generic;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for manual socket layout overrides.
    /// Allows designers to manually position specific sockets instead of using heuristics.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Socket Layout Override")]
    public sealed class SocketLayoutOverrideAuthoring : MonoBehaviour
    {
        [Serializable]
        public class SocketOverride
        {
            [Tooltip("Socket name (e.g., 'Socket_Weapon_M_01')")]
            public string socketName = string.Empty;
            [Tooltip("Manual position override (local space)")]
            public Vector3 position = Vector3.zero;
            [Tooltip("Manual rotation override (local space)")]
            public Vector3 rotation = Vector3.zero;
        }

        [Tooltip("Manual socket position overrides (empty = use heuristics)")]
        public List<SocketOverride> overrides = new List<SocketOverride>();

        /// <summary>
        /// Get override dictionary for socket layout heuristics.
        /// </summary>
        public Dictionary<string, Vector3> GetOverrideDictionary()
        {
            var dict = new Dictionary<string, Vector3>();
            foreach (var overrideEntry in overrides)
            {
                if (!string.IsNullOrWhiteSpace(overrideEntry.socketName))
                {
                    dict[overrideEntry.socketName] = overrideEntry.position;
                }
            }
            return dict;
        }
    }
}


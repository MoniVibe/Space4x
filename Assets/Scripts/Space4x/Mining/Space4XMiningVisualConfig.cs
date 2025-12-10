using System;
using UnityEngine;

namespace Space4X.Mining
{
    [CreateAssetMenu(fileName = "Space4XMiningVisualConfig", menuName = "Space4X/Mining/Mining Visual Config")]
    public class Space4XMiningVisualConfig : ScriptableObject
    {
        [Serializable]
        public struct MiningVisual
        {
            public string id;
            public GameObject prefab;
        }

        [SerializeField] private MiningVisual[] visuals = Array.Empty<MiningVisual>();

        public ReadOnlySpan<MiningVisual> Visuals => visuals.AsSpan();

        public bool TryGetPrefab(string id, out GameObject prefab)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                for (int i = 0; i < visuals.Length; i++)
                {
                    if (string.Equals(visuals[i].id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        prefab = visuals[i].prefab;
                        return prefab != null;
                    }
                }
            }

            prefab = null;
            return false;
        }
    }
}




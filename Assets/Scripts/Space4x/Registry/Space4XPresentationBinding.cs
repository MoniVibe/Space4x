using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// ScriptableObject for runtime prefab lookups. Maps entity IDs to prefab paths.
    /// </summary>
    [CreateAssetMenu(fileName = "Space4XPresentationBinding", menuName = "Space4X/Presentation Binding")]
    public class Space4XPresentationBinding : ScriptableObject
    {
        [Serializable]
        public class PrefabBinding
        {
            public string entityId;
            public string prefabPath;
            public EntityCategory category;
        }

        public enum EntityCategory : byte
        {
            Hull = 0,
            Module = 1,
            Station = 2,
            Resource = 3,
            Product = 4,
            Aggregate = 5,
            Effect = 6,
            Individual = 7,
            Weapon = 8,
            Projectile = 9,
            Turret = 10
        }

        [Header("Prefab Bindings")]
        public List<PrefabBinding> bindings = new List<PrefabBinding>();

        /// <summary>
        /// Get prefab path for an entity ID, or null if not found.
        /// </summary>
        public string GetPrefabPath(string entityId, EntityCategory category)
        {
            foreach (var binding in bindings)
            {
                if (binding.entityId == entityId && binding.category == category)
                {
                    return binding.prefabPath;
                }
            }
            return null;
        }

        /// <summary>
        /// Get all bindings for a category.
        /// </summary>
        public List<PrefabBinding> GetBindingsForCategory(EntityCategory category)
        {
            var result = new List<PrefabBinding>();
            foreach (var binding in bindings)
            {
                if (binding.category == category)
                {
                    result.Add(binding);
                }
            }
            return result;
        }

        /// <summary>
        /// Clear all bindings.
        /// </summary>
        public void Clear()
        {
            bindings.Clear();
        }

        /// <summary>
        /// Add or update a binding.
        /// </summary>
        public void SetBinding(string entityId, string prefabPath, EntityCategory category)
        {
            var existing = bindings.Find(b => b.entityId == entityId && b.category == category);
            if (existing != null)
            {
                existing.prefabPath = prefabPath;
            }
            else
            {
                bindings.Add(new PrefabBinding
                {
                    entityId = entityId,
                    prefabPath = prefabPath,
                    category = category
                });
            }
        }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Space4X.Presentation.Config
{
    [CreateAssetMenu(fileName = "Space4XPresentationBinding", menuName = "Space4X/Presentation/Presentation Binding")]
    public class Space4XPresentationBinding : ScriptableObject
    {
        [Serializable]
        public struct BindingEntry
        {
            public string entityId;
            public string prefabPath;
            public EntityCategory category;
        }

        public enum EntityCategory : byte
        {
            Hull,
            Module,
            Station,
            Resource,
            Product,
            Aggregate,
            Effect,
            Individual,
            Weapon,
            Projectile,
            Turret
        }

        [SerializeField] private List<BindingEntry> bindings = new List<BindingEntry>();

        public int Count => bindings?.Count ?? 0;

        public IReadOnlyList<BindingEntry> GetBindingsForCategory(EntityCategory category)
        {
            if (bindings == null || bindings.Count == 0)
            {
                return Array.Empty<BindingEntry>();
            }

            return bindings.Where(b => b.category == category).ToList();
        }

        public void SetBinding(string entityId, string prefabPath, EntityCategory category)
        {
            if (string.IsNullOrWhiteSpace(entityId) || string.IsNullOrWhiteSpace(prefabPath))
            {
                return;
            }

            if (bindings == null)
            {
                bindings = new List<BindingEntry>();
            }

            var entry = new BindingEntry
            {
                entityId = entityId,
                prefabPath = prefabPath.Replace('\\', '/'),
                category = category
            };

            var index = bindings.FindIndex(b =>
                string.Equals(b.entityId, entityId, StringComparison.OrdinalIgnoreCase) &&
                b.category == category);

            if (index >= 0)
            {
                bindings[index] = entry;
            }
            else
            {
                bindings.Add(entry);
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void Clear()
        {
            bindings?.Clear();
        }
    }
}


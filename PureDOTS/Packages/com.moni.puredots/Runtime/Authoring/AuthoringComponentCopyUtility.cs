using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Helper utilities inspired by Unity's AutoAuthoring sample that copy matching authoring fields into ECS components.
    /// </summary>
    internal static class AuthoringComponentCopyUtility
    {
        public delegate void ComponentPostProcessor<TAuthoring, TComponent>(TAuthoring authoring, ref TComponent component);

        private readonly struct TypePair : IEquatable<TypePair>
        {
            public readonly Type AuthoringType;
            public readonly Type ComponentType;

            public TypePair(Type authoringType, Type componentType)
            {
                AuthoringType = authoringType;
                ComponentType = componentType;
            }

            public bool Equals(TypePair other)
            {
                return AuthoringType == other.AuthoringType && ComponentType == other.ComponentType;
            }

            public override bool Equals(object obj)
            {
                return obj is TypePair other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (AuthoringType.GetHashCode() * 397) ^ ComponentType.GetHashCode();
                }
            }
        }

        private readonly struct FieldMapping
        {
            public readonly FieldInfo AuthoringField;
            public readonly FieldInfo ComponentField;

            public FieldMapping(FieldInfo authoringField, FieldInfo componentField)
            {
                AuthoringField = authoringField;
                ComponentField = componentField;
            }
        }

        private static readonly Dictionary<TypePair, FieldMapping[]> _fieldCache = new();

        /// <summary>
        /// Copies matching fields from the authoring component into a newly created ECS component instance.
        /// </summary>
        public static TComponent CreateComponent<TAuthoring, TComponent>(TAuthoring authoring,
            ComponentPostProcessor<TAuthoring, TComponent> postProcess = null)
            where TAuthoring : class
            where TComponent : struct
        {
            if (authoring == null)
            {
                throw new ArgumentNullException(nameof(authoring));
            }

            var component = default(TComponent);
            CopyFields(authoring, ref component);
            postProcess?.Invoke(authoring, ref component);
            return component;
        }

        /// <summary>
        /// Copies matching fields from the authoring component into the provided ECS component instance.
        /// </summary>
        public static void CopyFields<TAuthoring, TComponent>(TAuthoring authoring, ref TComponent component,
            ComponentPostProcessor<TAuthoring, TComponent> postProcess = null)
            where TAuthoring : class
            where TComponent : struct
        {
            if (authoring == null)
            {
                throw new ArgumentNullException(nameof(authoring));
            }

            var typePair = new TypePair(authoring.GetType(), typeof(TComponent));
            var mappings = GetMappings(typePair);

            if (mappings.Length > 0)
            {
                object boxedComponent = component;
                foreach (var mapping in mappings)
                {
                    var value = mapping.AuthoringField.GetValue(authoring);
                    mapping.ComponentField.SetValue(boxedComponent, value);
                }

                component = (TComponent)boxedComponent;
            }

            postProcess?.Invoke(authoring, ref component);
        }

        private static FieldMapping[] GetMappings(TypePair typePair)
        {
            lock (_fieldCache)
            {
                if (_fieldCache.TryGetValue(typePair, out var cached))
                {
                    return cached;
                }

                var mappings = BuildMappings(typePair.AuthoringType, typePair.ComponentType);
                _fieldCache[typePair] = mappings;
                return mappings;
            }
        }

        private static FieldMapping[] BuildMappings(Type authoringType, Type componentType)
        {
            const BindingFlags componentBinding = BindingFlags.Instance | BindingFlags.Public;
            const BindingFlags authoringBinding = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var componentFields = componentType.GetFields(componentBinding);
            var mappingList = new List<FieldMapping>(componentFields.Length);

            foreach (var componentField in componentFields)
            {
                if (componentField.IsStatic)
                {
                    continue;
                }

                var authoringField = authoringType.GetField(componentField.Name, authoringBinding)
                                   ?? authoringType.GetField(ToCamelCase(componentField.Name), authoringBinding)
                                   ?? authoringType.GetField("_" + ToCamelCase(componentField.Name), authoringBinding);

                if (authoringField == null || authoringField.IsStatic)
                {
                    continue;
                }

                if (Attribute.IsDefined(authoringField, typeof(NonSerializedAttribute)))
                {
                    continue;
                }

                bool isSerializable = authoringField.IsPublic || Attribute.IsDefined(authoringField, typeof(SerializeField));
                if (!isSerializable)
                {
                    continue;
                }

                if (!componentField.FieldType.IsAssignableFrom(authoringField.FieldType))
                {
                    continue;
                }

                mappingList.Add(new FieldMapping(authoringField, componentField));
            }

            return mappingList.ToArray();
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            if (char.IsLower(name[0]))
            {
                return name;
            }

            if (name.Length == 1)
            {
                return char.ToLowerInvariant(name[0]).ToString();
            }

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}


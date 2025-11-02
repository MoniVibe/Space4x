using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TypeLogger
{
    public static void LogTypesContaining(string fragment)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }

            foreach (var type in types)
            {
                if (type.FullName != null && type.FullName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log($"Type match: {type.FullName} ({assembly.FullName})");
                }
            }
        }
    }

    public static void LogTypeMembers(string fullTypeName)
    {
        var type = ResolveType(fullTypeName);
        if (type == null)
        {
            Debug.LogError($"Type '{fullTypeName}' not found.");
            return;
        }

        Debug.Log($"Members of {type.FullName} (DeclaredOnly):");
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Debug.Log($"Method: {method}");
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Debug.Log($"Field: {field.FieldType} {field.Name}");
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Debug.Log($"Property: {property.PropertyType} {property.Name}");
        }
    }

    private static Type ResolveType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }
            catch
            {
                // ignored
            }
        }

        return null;
    }
}

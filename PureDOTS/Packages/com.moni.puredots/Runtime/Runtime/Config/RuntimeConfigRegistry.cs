using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#nullable enable

namespace PureDOTS.Runtime.Config
{
    [Flags]
    public enum RuntimeConfigFlags
    {
        None = 0,
        Save = 1 << 0,
        Cheat = 1 << 1
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RuntimeConfigVarAttribute : Attribute
    {
        public RuntimeConfigVarAttribute(string name, string defaultValue = "")
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DefaultValue = defaultValue ?? string.Empty;
        }

        public string Name { get; }
        public string DefaultValue { get; }
        public RuntimeConfigFlags Flags { get; set; } = RuntimeConfigFlags.None;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class RuntimeConfigVar
    {
        private readonly string _defaultValue;
        private string _value;
        private bool _dirty;

        internal RuntimeConfigVar(string name, string description, string defaultValue, RuntimeConfigFlags flags)
        {
            Name = name;
            Description = description;
            Flags = flags;
            _defaultValue = defaultValue ?? string.Empty;
            _value = _defaultValue;
        }

        public string Name { get; }
        public string Description { get; }
        public RuntimeConfigFlags Flags { get; }
        public bool IsDirty => _dirty;

        public string Value
        {
            get => _value;
            set => SetValueInternal(value, markDirty: true, notify: true);
        }

        public bool BoolValue
        {
            get => ParseBool(_value);
            set => Value = value ? "1" : "0";
        }

        public int IntValue
        {
            get => int.TryParse(_value, out var result) ? result : 0;
            set => Value = value.ToString();
        }

        public float FloatValue
        {
            get => float.TryParse(_value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0f;
            set => Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public event Action<RuntimeConfigVar>? ValueChanged;

        public void ResetToDefault()
        {
            SetValueInternal(_defaultValue, markDirty: false, notify: true);
        }

        internal void SetValueFromStorage(string value)
        {
            SetValueInternal(value, markDirty: false, notify: true);
        }

        private void SetValueInternal(string value, bool markDirty, bool notify)
        {
            value ??= string.Empty;
            if (_value == value)
                return;

            _value = value;
            _dirty = markDirty;
            if (notify)
            {
                ValueChanged?.Invoke(this);
            }
        }

        private static bool ParseBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
                return false;

            return bool.TryParse(value, out var result) && result;
        }
    }

    public static class RuntimeConfigRegistry
    {
        private const string DefaultFileName = "puredots.cfg";

        private static readonly Dictionary<string, RuntimeConfigVar> s_configVars = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<FieldInfo> s_registeredFields = new();
        private static bool s_initialized;
        private static bool s_dirty;
        private static string? s_overrideStoragePath;
        private static string? s_defaultStoragePath;

        public static string StoragePath
        {
            get
            {
                if (!string.IsNullOrEmpty(s_overrideStoragePath))
                {
                    return s_overrideStoragePath!;
                }

                if (string.IsNullOrEmpty(s_defaultStoragePath))
                {
                    var dataPath = Application.dataPath;
                    var root = string.IsNullOrEmpty(dataPath)
                        ? Directory.GetCurrentDirectory()
                        : Path.GetFullPath(Path.Combine(dataPath, ".."));
                    s_defaultStoragePath = Path.Combine(root, "UserSettings", DefaultFileName);
                }

                return s_defaultStoragePath!;
            }
            set => s_overrideStoragePath = value;
        }

        public static void Initialize()
        {
            if (s_initialized)
                return;

#if UNITY_EDITOR
            // Skip heavy assembly scanning during domain reload/compilation to prevent editor freeze
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                UnityEngine.Debug.Log("[RuntimeConfigRegistry] Initialize skipped - editor is compiling/updating");
                return;
            }
#endif

            UnityEngine.Debug.Log("[RuntimeConfigRegistry] Initialize - scanning assemblies");
            ScanAssemblies();
            LoadFromDisk();
            s_initialized = true;
            UnityEngine.Debug.Log($"[RuntimeConfigRegistry] Initialize complete - registered {s_configVars.Count} config vars");
        }

        public static IEnumerable<RuntimeConfigVar> GetVariables()
        {
            Initialize();
            return s_configVars.Values.OrderBy(v => v.Name);
        }

        public static bool TryGetVar(string name, out RuntimeConfigVar configVar)
        {
            Initialize();
            return s_configVars.TryGetValue(name, out configVar!);
        }

        public static bool SetValue(string name, string value, out string message)
        {
            Initialize();

            if (!s_configVars.TryGetValue(name, out var configVar))
            {
                message = $"Unknown config var '{name}'.";
                return false;
            }

            configVar.Value = value;
            s_dirty = true;
            message = $"{configVar.Name} = {configVar.Value}";
            return true;
        }

        public static bool ResetValue(string name, out string message)
        {
            Initialize();

            if (!s_configVars.TryGetValue(name, out var configVar))
            {
                message = $"Unknown config var '{name}'.";
                return false;
            }

            configVar.ResetToDefault();
            s_dirty = true;
            message = $"{configVar.Name} reset to {configVar.Value}.";
            return true;
        }

        public static void ResetAll()
        {
            Initialize();
            foreach (var configVar in s_configVars.Values)
            {
                configVar.ResetToDefault();
            }
            s_dirty = true;
        }

        public static void Save()
        {
            Initialize();

            var path = StoragePath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var writer = new StreamWriter(path, false);
            foreach (var configVar in s_configVars.Values.OrderBy(v => v.Name))
            {
                if ((configVar.Flags & RuntimeConfigFlags.Save) == 0)
                    continue;

                writer.WriteLine($"{configVar.Name}={configVar.Value}");
            }

            s_dirty = false;
        }

        public static void SaveIfDirty()
        {
            if (!s_dirty)
                return;

            Save();
        }

        public static void ResetForTests()
        {
            foreach (var field in s_registeredFields)
            {
                try
                {
                    if (field.IsStatic)
                    {
                        field.SetValue(null, null);
                    }
                }
                catch
                {
                    // ignored: test cleanup best effort.
                }
            }

            s_registeredFields.Clear();
            s_configVars.Clear();
            s_initialized = false;
            s_dirty = false;
        }

        private static void ScanAssemblies()
        {
#if UNITY_EDITOR
            // Guard: Skip assembly scanning during domain reload/compilation to prevent editor freeze
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }
#endif
            if (Application.isBatchMode && !Application.isPlaying)
            {
                return;
            }

            s_configVars.Clear();
            s_registeredFields.Clear();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray()!;
                }
                catch (Exception)
                {
                    // Skip problematic assemblies during reload
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null)
                        continue;

                    try
                    {
                        foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (!field.IsDefined(typeof(RuntimeConfigVarAttribute), inherit: false))
                                continue;

                            if (!field.IsStatic)
                                continue;

                            if (field.FieldType != typeof(RuntimeConfigVar))
                                continue;

                            var attribute = field.GetCustomAttribute<RuntimeConfigVarAttribute>(false);
                            if (attribute == null)
                                continue;

                            if (string.IsNullOrWhiteSpace(attribute.Name))
                                continue;

                            if (s_configVars.ContainsKey(attribute.Name))
                                continue;

                            var configVar = new RuntimeConfigVar(attribute.Name, attribute.Description, attribute.DefaultValue, attribute.Flags);
                            s_configVars.Add(configVar.Name, configVar);
                            s_registeredFields.Add(field);
                            field.SetValue(null, configVar);
                        }
                    }
                    catch (Exception)
                    {
                        // Skip problematic types during reload
                        continue;
                    }
                }
            }
        }

        private static void LoadFromDisk()
        {
            var path = StoragePath;
            if (!File.Exists(path))
                return;

            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmed = line.Trim();
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                var name = trimmed.Substring(0, separatorIndex).Trim();
                var value = trimmed.Substring(separatorIndex + 1);

                if (!s_configVars.TryGetValue(name, out var configVar))
                    continue;

                configVar.SetValueFromStorage(value);
            }

            s_dirty = false;
        }
    }
}


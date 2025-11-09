using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Converts JSON values coming from MCP requests into the strongly-typed objects
    /// expected by Visual Effect Graph reflection APIs.
    /// </summary>
    internal static class VfxValueConverter
    {
        public static object ConvertTokenToType(JToken token, Type targetType, object currentValue = null)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return targetType != null && targetType.IsValueType
                    ? Activator.CreateInstance(targetType)
                    : null;
            }

            if (targetType == null || targetType == typeof(object))
            {
                if (currentValue != null)
                {
                    targetType = currentValue.GetType();
                }
            }

            if (targetType == null || targetType == typeof(object))
            {
                return token.ToObject<object>();
            }

            if (TryHandlePrimitive(token, ref targetType, out var primitiveResult))
            {
                return primitiveResult;
            }

            if (targetType.IsEnum)
            {
                return ConvertToEnum(token, targetType);
            }

            if (targetType == typeof(Vector2))
            {
                return ConvertToVector2(token);
            }

            if (targetType == typeof(Vector3))
            {
                return ConvertToVector3(token);
            }

            if (targetType == typeof(Vector4))
            {
                return ConvertToVector4(token);
            }

            if (targetType == typeof(Color))
            {
                return ConvertToColor(token);
            }

            if (targetType.FullName != null && targetType.FullName.StartsWith("UnityEditor.VFX.", StringComparison.Ordinal))
            {
                return ConvertToUnityVfxType(token, targetType, currentValue);
            }

            try
            {
                return token.ToObject(targetType);
            }
            catch
            {
                var intermediate = token.ToObject<object>();
                if (intermediate != null)
                {
                    try
                    {
                        return System.Convert.ChangeType(intermediate, targetType, CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (currentValue != null && targetType.IsInstanceOfType(currentValue))
            {
                return currentValue;
            }

            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        private static bool TryHandlePrimitive(JToken token, ref Type targetType, out object result)
        {
            result = null;

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                targetType = Nullable.GetUnderlyingType(targetType);
            }

            if (targetType == typeof(string))
            {
                result = token.Type == JTokenType.String
                    ? token.ToString()
                    : token.ToString(Newtonsoft.Json.Formatting.None);
                return true;
            }

            if (targetType == typeof(float))
            {
                result = token.Value<float>();
                return true;
            }

            if (targetType == typeof(double))
            {
                result = token.Value<double>();
                return true;
            }

            if (targetType == typeof(int))
            {
                result = token.Value<int>();
                return true;
            }

            if (targetType == typeof(uint))
            {
                result = token.Value<uint>();
                return true;
            }

            if (targetType == typeof(long))
            {
                result = token.Value<long>();
                return true;
            }

            if (targetType == typeof(ulong))
            {
                result = token.Value<ulong>();
                return true;
            }

            if (targetType == typeof(short))
            {
                result = token.Value<short>();
                return true;
            }

            if (targetType == typeof(ushort))
            {
                result = token.Value<ushort>();
                return true;
            }

            if (targetType == typeof(byte))
            {
                result = token.Value<byte>();
                return true;
            }

            if (targetType == typeof(sbyte))
            {
                result = token.Value<sbyte>();
                return true;
            }

            if (targetType == typeof(bool))
            {
                result = token.Value<bool>();
                return true;
            }

            return false;
        }

        private static object ConvertToEnum(JToken token, Type enumType)
        {
            if (token.Type == JTokenType.String)
            {
                return Enum.Parse(enumType, token.ToString(), ignoreCase: true);
            }

            var numeric = token.ToObject(Enum.GetUnderlyingType(enumType));
            return Enum.ToObject(enumType, numeric);
        }

        private static Vector2 ConvertToVector2(JToken token)
        {
            if (token is JObject obj)
            {
                return new Vector2(
                    GetFloat(obj, "x"),
                    GetFloat(obj, "y"));
            }

            if (token is JArray array)
            {
                return new Vector2(
                    array.Count > 0 ? array[0].Value<float>() : 0f,
                    array.Count > 1 ? array[1].Value<float>() : 0f);
            }

            if (token.Type == JTokenType.String)
            {
                var values = ParseFloats(token.ToString());
                return new Vector2(
                    values.Count > 0 ? values[0] : 0f,
                    values.Count > 1 ? values[1] : 0f);
            }

            var scalar = token.Value<float>();
            return new Vector2(scalar, scalar);
        }

        private static Vector3 ConvertToVector3(JToken token)
        {
            if (token is JObject obj)
            {
                if (TryGetCaseInsensitive(obj, "vector", out var vectorToken))
                {
                    return ConvertToVector3(vectorToken);
                }

                return new Vector3(
                    GetFloat(obj, "x"),
                    GetFloat(obj, "y"),
                    GetFloat(obj, "z"));
            }

            if (token is JArray array)
            {
                return new Vector3(
                    array.Count > 0 ? array[0].Value<float>() : 0f,
                    array.Count > 1 ? array[1].Value<float>() : 0f,
                    array.Count > 2 ? array[2].Value<float>() : 0f);
            }

            if (token.Type == JTokenType.String)
            {
                var values = ParseFloats(token.ToString());
                return new Vector3(
                    values.Count > 0 ? values[0] : 0f,
                    values.Count > 1 ? values[1] : 0f,
                    values.Count > 2 ? values[2] : 0f);
            }

            var scalar = token.Value<float>();
            return new Vector3(scalar, scalar, scalar);
        }

        private static Vector4 ConvertToVector4(JToken token)
        {
            if (token is JObject obj)
            {
                if (TryGetCaseInsensitive(obj, "vector", out var vectorToken))
                {
                    return ConvertToVector4(vectorToken);
                }

                return new Vector4(
                    GetFloat(obj, "x"),
                    GetFloat(obj, "y"),
                    GetFloat(obj, "z"),
                    GetFloat(obj, "w"));
            }

            if (token is JArray array)
            {
                return new Vector4(
                    array.Count > 0 ? array[0].Value<float>() : 0f,
                    array.Count > 1 ? array[1].Value<float>() : 0f,
                    array.Count > 2 ? array[2].Value<float>() : 0f,
                    array.Count > 3 ? array[3].Value<float>() : 0f);
            }

            if (token.Type == JTokenType.String)
            {
                var values = ParseFloats(token.ToString());
                return new Vector4(
                    values.Count > 0 ? values[0] : 0f,
                    values.Count > 1 ? values[1] : 0f,
                    values.Count > 2 ? values[2] : 0f,
                    values.Count > 3 ? values[3] : 0f);
            }

            var scalar = token.Value<float>();
            return new Vector4(scalar, scalar, scalar, scalar);
        }

        private static Color ConvertToColor(JToken token)
        {
            if (token is JObject obj)
            {
                return new Color(
                    GetFloat(obj, "r"),
                    GetFloat(obj, "g"),
                    GetFloat(obj, "b"),
                    obj.Properties().Any(p => string.Equals(p.Name, "a", StringComparison.OrdinalIgnoreCase))
                        ? GetFloat(obj, "a")
                        : 1f);
            }

            if (token is JArray array)
            {
                return new Color(
                    array.Count > 0 ? array[0].Value<float>() : 0f,
                    array.Count > 1 ? array[1].Value<float>() : 0f,
                    array.Count > 2 ? array[2].Value<float>() : 0f,
                    array.Count > 3 ? array[3].Value<float>() : 1f);
            }

            if (token.Type == JTokenType.String)
            {
                var text = token.ToString();
                if (ColorUtility.TryParseHtmlString(text, out var color))
                {
                    return color;
                }

                var values = ParseFloats(text);
                if (values.Count >= 3)
                {
                    return new Color(
                        values[0],
                        values[1],
                        values[2],
                        values.Count > 3 ? values[3] : 1f);
                }
            }

            return Color.white;
        }

        private static object ConvertToUnityVfxType(JToken token, Type targetType, object currentValue)
        {
            var instance = currentValue ?? Activator.CreateInstance(targetType);
            if (instance == null)
            {
                return null;
            }

            if (string.Equals(targetType.FullName, "UnityEditor.VFX.Circle", StringComparison.Ordinal))
            {
                ApplyCircle(instance, token);
                return instance;
            }

            if (token is JObject obj)
            {
                foreach (var property in targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!property.CanWrite)
                    {
                        continue;
                    }

                    if (!TryGetCaseInsensitive(obj, property.Name, out var childToken))
                    {
                        continue;
                    }

                    var existing = property.CanRead ? property.GetValue(instance) : null;
                    var converted = ConvertTokenToType(childToken, property.PropertyType, existing);
                    property.SetValue(instance, converted);
                }

                // Support nested "vector", "direction", "position" payloads when the token is flattened.
                if (TryGetCaseInsensitive(obj, "vector", out var vectorToken))
                {
                    SetIfPropertyExists(instance, "vector", ConvertToVector3(vectorToken));
                }
                if (TryGetCaseInsensitive(obj, "direction", out var directionToken))
                {
                    SetIfPropertyExists(instance, "direction", ConvertToVector3(directionToken));
                }
                if (TryGetCaseInsensitive(obj, "position", out var positionToken))
                {
                    SetIfPropertyExists(instance, "position", ConvertToVector3(positionToken));
                }
            }
            else
            {
                // Scalar fallbacks – apply to common properties.
                if (targetType.GetProperty("radius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
                {
                    if (float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var radius))
                    {
                        SetIfPropertyExists(instance, "radius", radius);
                    }
                }
                else if (targetType.GetProperty("vector", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
                {
                    SetIfPropertyExists(instance, "vector", ConvertToVector3(token));
                }
            }

            return instance;
        }

        private static void ApplyCircle(object instance, JToken token)
        {
            if (token is JObject obj)
            {
                if (TryGetCaseInsensitive(obj, "radius", out var radiusToken))
                {
                    SetIfPropertyExists(instance, "radius", radiusToken.Value<float>());
                }

                if (TryGetCaseInsensitive(obj, "center", out var centerToken))
                {
                    SetIfPropertyExists(instance, "center", ConvertToVector3(centerToken));
                }
            }
            else if (float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var radius))
            {
                SetIfPropertyExists(instance, "radius", radius);
            }
        }

        private static void SetIfPropertyExists(object instance, string propertyName, object value)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value);
            }
        }

        private static float GetFloat(JObject obj, string name)
        {
            if (TryGetCaseInsensitive(obj, name, out var token))
            {
                return token.Value<float>();
            }

            return 0f;
        }

        private static bool TryGetCaseInsensitive(JObject obj, string name, out JToken value)
        {
            value = null;
            if (obj.TryGetValue(name, StringComparison.Ordinal, out value))
            {
                return true;
            }

            foreach (var property in obj.Properties())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            return false;
        }

        private static List<float> ParseFloats(string text)
        {
            var matches = Regex.Matches(text, @"-?\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
            var result = new List<float>(matches.Count);

            foreach (Match match in matches)
            {
                if (float.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    result.Add(value);
                }
            }

            return result;
        }
    }
}



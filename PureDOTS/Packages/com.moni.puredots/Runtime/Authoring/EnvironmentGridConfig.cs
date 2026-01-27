using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Environment;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PureDOTS.Authoring
{
    [CreateAssetMenu(fileName = "EnvironmentGridConfig", menuName = "PureDOTS/Environment/Grid Config", order = 5)]
    public sealed class EnvironmentGridConfig : ScriptableObject
    {
        public const int LatestSchemaVersion = 1;
        private const bool BiomeGridRuntimeSupport = false;

        [SerializeField, HideInInspector]
        private int _schemaVersion = LatestSchemaVersion;

        [Header("Grid Settings")]
        [SerializeField] GridSettings _moisture = GridSettings.CreateDefault(new Vector2Int(256, 256), 5f);
        [SerializeField] GridSettings _temperature = GridSettings.CreateDefault(new Vector2Int(128, 128), 10f);
        [SerializeField] GridSettings _sunlight = GridSettings.CreateDefault(new Vector2Int(128, 128), 10f);
        [SerializeField] GridSettings _wind = GridSettings.CreateDefault(new Vector2Int(64, 64), 20f);
        [SerializeField] GridSettings _biome = GridSettings.CreateDefault(new Vector2Int(128, 128), 10f, enabled: false);

        [Header("Channel Identifiers")]
        [SerializeField] string _moistureChannelId = "moisture";
        [SerializeField] string _temperatureChannelId = "temperature";
        [SerializeField] string _sunlightChannelId = "sunlight";
        [SerializeField] string _windChannelId = "wind";
        [SerializeField] string _biomeChannelId = "biome";

        [Header("Moisture Defaults")]
        [SerializeField, Min(0f)] float _moistureDiffusion = 0.25f;
        [SerializeField, Min(0f)] float _moistureSeepage = 0.1f;

        [Header("Temperature Defaults")]
        [SerializeField] float _baseSeasonTemperature = 18f;
        [SerializeField, Min(0f)] float _timeOfDaySwing = 6f;
        [SerializeField, Min(0f)] float _seasonalSwing = 12f;

        [Header("Sunlight Defaults")]
        [SerializeField] Vector3 _sunDirection = new Vector3(0.25f, -0.9f, 0.35f);
        [SerializeField, Min(0f)] float _sunIntensity = 1f;

        [Header("Wind Defaults")]
        [SerializeField] Vector2 _globalWindDirection = new Vector2(0.7f, 0.5f);
        [SerializeField, Min(0f)] float _globalWindStrength = 8f;

        public GridSettings Moisture => _moisture;
        public GridSettings Temperature => _temperature;
        public GridSettings Sunlight => _sunlight;
        public GridSettings Wind => _wind;
        public GridSettings Biome => _biome;
        public int SchemaVersion => _schemaVersion;

        public string MoistureChannelId() => _moistureChannelId;
        public string TemperatureChannelId() => _temperatureChannelId;
        public string SunlightChannelId() => _sunlightChannelId;
        public string WindChannelId() => _windChannelId;
        public string BiomeChannelId() => _biomeChannelId;
        public Vector3 RawSunDirection() => _sunDirection;
        public Vector2 RawWindDirection() => _globalWindDirection;

        public EnvironmentGridConfigData ToComponent()
        {
            var biomeEnabled = _biome.Enabled && BiomeGridRuntimeSupport;

#if UNITY_EDITOR
            if (_biome.Enabled && !BiomeGridRuntimeSupport)
            {
                Debug.LogWarning("EnvironmentGridConfig: biome grid authoring is available for previews, but runtime support is disabled until BiomeDeterminationSystem ships. The biome channel will be ignored.", this);
            }
#endif

            return new EnvironmentGridConfigData
            {
                Moisture = _moisture.ToMetadata(),
                Temperature = _temperature.ToMetadata(),
                Sunlight = _sunlight.ToMetadata(),
                Wind = _wind.ToMetadata(),
                Biome = _biome.ToMetadata(),
                BiomeEnabled = biomeEnabled ? (byte)1 : (byte)0,
                MoistureChannelId = ToFixedString(_moistureChannelId, "moisture"),
                TemperatureChannelId = ToFixedString(_temperatureChannelId, "temperature"),
                SunlightChannelId = ToFixedString(_sunlightChannelId, "sunlight"),
                WindChannelId = ToFixedString(_windChannelId, "wind"),
                BiomeChannelId = ToFixedString(_biomeChannelId, "biome"),
                MoistureDiffusion = math.max(0f, _moistureDiffusion),
                MoistureSeepage = math.max(0f, _moistureSeepage),
                BaseSeasonTemperature = _baseSeasonTemperature,
                TimeOfDaySwing = math.max(0f, _timeOfDaySwing),
                SeasonalSwing = math.max(0f, _seasonalSwing),
                DefaultSunDirection = NormalizeSunDirection(_sunDirection),
                DefaultSunIntensity = math.max(0f, _sunIntensity),
                DefaultWindDirection = NormalizeWindDirection(_globalWindDirection),
                DefaultWindStrength = math.max(0f, _globalWindStrength)
            };
        }

#if UNITY_EDITOR
        internal void SetSchemaVersion(int value)
        {
            _schemaVersion = value;
        }

        private void OnValidate()
        {
            _moisture.Sanitize();
            _temperature.Sanitize();
            _sunlight.Sanitize();
            _wind.Sanitize();
            _biome.Sanitize();

            _moistureChannelId = SanitizeChannel(_moistureChannelId, "moisture");
            _temperatureChannelId = SanitizeChannel(_temperatureChannelId, "temperature");
            _sunlightChannelId = SanitizeChannel(_sunlightChannelId, "sunlight");
            _windChannelId = SanitizeChannel(_windChannelId, "wind");
            _biomeChannelId = SanitizeChannel(_biomeChannelId, "biome");
        }
#endif

        static FixedString64Bytes ToFixedString(string value, string fallback)
        {
            var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            FixedString64Bytes fixedString = text;
            return fixedString;
        }

        static string SanitizeChannel(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        static float3 NormalizeSunDirection(Vector3 direction)
        {
            var dir = (float3)direction;
            if (math.lengthsq(dir) < 1e-6f)
            {
                dir = new float3(0f, -1f, 0f);
            }
            return math.normalize(dir);
        }

        static float2 NormalizeWindDirection(Vector2 direction)
        {
            var dir = (float2)direction;
            if (math.lengthsq(dir) < 1e-6f)
            {
                dir = new float2(0f, 1f);
            }
            return math.normalize(dir);
        }

        [Serializable]
        public struct GridSettings
        {
            [SerializeField] Vector2Int _resolution;
            [SerializeField, Min(0.1f)] float _cellSize;
            [SerializeField] Vector3 _worldMin;
            [SerializeField] Vector3 _worldMax;
            [SerializeField] bool _enabled;

            public bool Enabled => _enabled;

            public static GridSettings CreateDefault(Vector2Int resolution, float cellSize, bool enabled = true)
            {
                return new GridSettings
                {
                    _resolution = new Vector2Int(math.max(1, resolution.x), math.max(1, resolution.y)),
                    _cellSize = math.max(0.1f, cellSize),
                    _worldMin = new Vector3(-512f, 0f, -512f),
                    _worldMax = new Vector3(512f, 256f, 512f),
                    _enabled = enabled
                };
            }

            public EnvironmentGridMetadata ToMetadata()
            {
                var min = (float3)_worldMin;
                var max = (float3)_worldMax;
                var safeCellSize = math.max(0.1f, _cellSize);
                max = math.max(max, min + new float3(safeCellSize, 0.01f, safeCellSize));

                var resolution = new int2(math.max(1, _resolution.x), math.max(1, _resolution.y));
                return EnvironmentGridMetadata.Create(min, max, safeCellSize, resolution);
            }

            public void Sanitize()
            {
                _resolution.x = math.max(1, _resolution.x);
                _resolution.y = math.max(1, _resolution.y);
                _cellSize = math.max(0.1f, _cellSize);

                if (_worldMax.x <= _worldMin.x)
                {
                    _worldMax.x = _worldMin.x + _cellSize;
                }

                if (_worldMax.y <= _worldMin.y)
                {
                    _worldMax.y = _worldMin.y + 0.01f;
                }

                if (_worldMax.z <= _worldMin.z)
                {
                    _worldMax.z = _worldMin.z + _cellSize;
                }
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class EnvironmentGridConfigAuthoring : MonoBehaviour
    {
        public EnvironmentGridConfig config;

#if UNITY_EDITOR
        [Header("Gizmo Preview")]
        [SerializeField] bool _drawGizmos = true;
        [SerializeField] bool _showLabels = true;
        [SerializeField] bool _drawMoisture = true;
        [SerializeField] bool _drawTemperature = true;
        [SerializeField] bool _drawSunlight = true;
        [SerializeField] bool _drawWind;
        [SerializeField] bool _drawBiome;

        static readonly Color k_MoistureColor = new Color(0.18f, 0.62f, 0.98f, 0.55f);
        static readonly Color k_TemperatureColor = new Color(0.92f, 0.38f, 0.24f, 0.55f);
        static readonly Color k_SunlightColor = new Color(1f, 0.85f, 0.25f, 0.55f);
        static readonly Color k_WindColor = new Color(0.25f, 0.92f, 0.78f, 0.55f);
        static readonly Color k_BiomeColor = new Color(0.32f, 0.82f, 0.36f, 0.55f);

        void OnDrawGizmosSelected()
        {
            if (!_drawGizmos || config == null)
            {
                return;
            }

            var data = config.ToComponent();

            DrawGridGizmo(_drawMoisture && config.Moisture.Enabled, in data.Moisture, k_MoistureColor, "Moisture");
            DrawGridGizmo(_drawTemperature && config.Temperature.Enabled, in data.Temperature, k_TemperatureColor, "Temperature");
            DrawGridGizmo(_drawSunlight && config.Sunlight.Enabled, in data.Sunlight, k_SunlightColor, "Sunlight");
            DrawGridGizmo(_drawWind && config.Wind.Enabled, in data.Wind, k_WindColor, "Wind");
            DrawGridGizmo(_drawBiome && config.Biome.Enabled, in data.Biome, k_BiomeColor, "Biome");
        }

        void DrawGridGizmo(bool enabled, in EnvironmentGridMetadata metadata, Color color, string label)
        {
            if (!enabled)
            {
                return;
            }

            var worldMin = metadata.WorldMin;
            var worldMax = metadata.WorldMax;
            var worldSize = worldMax - worldMin;
            if (worldSize.y < 0.5f)
            {
                worldSize.y = 0.5f;
            }

            var center = worldMin + worldSize * 0.5f;
            var solidColor = new Color(color.r, color.g, color.b, 0.15f);

            Gizmos.color = solidColor;
            Gizmos.DrawCube(ToVector3(center), ToVector3(worldSize));
            Gizmos.color = color;
            Gizmos.DrawWireCube(ToVector3(center), ToVector3(worldSize));

            if (_showLabels)
            {
                var labelPos = worldMax;
                labelPos.y += worldSize.y * 0.5f;
                Handles.color = color;
                Handles.Label(ToVector3(labelPos), $"{label}\n{metadata.Resolution.x}x{metadata.Resolution.y} @ {metadata.CellSize:0.##}m");
            }
        }

        static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
#endif
    }

    public sealed class EnvironmentGridConfigBaker : Baker<EnvironmentGridConfigAuthoring>
    {
        public override void Bake(EnvironmentGridConfigAuthoring authoring)
        {
            if (authoring.config == null)
            {
                Debug.LogWarning("EnvironmentGridConfigAuthoring has no EnvironmentGridConfig assigned.", authoring);
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, authoring.config.ToComponent());
        }
    }
}

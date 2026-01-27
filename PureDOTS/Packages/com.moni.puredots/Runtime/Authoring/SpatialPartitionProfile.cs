using PureDOTS.Runtime.Spatial;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    public enum SpatialProviderType : byte
    {
        UniformGrid = 0,
        HashedGrid = 1
    }

    [CreateAssetMenu(fileName = "SpatialPartitionProfile", menuName = "PureDOTS/Spatial Partition Profile", order = 20)]
    public sealed class SpatialPartitionProfile : ScriptableObject
    {
        public const int LatestSchemaVersion = 2;

        [SerializeField, HideInInspector]
        private int _schemaVersion = LatestSchemaVersion;

        [Header("Bounds")]
        [SerializeField] private Vector3 _center = Vector3.zero;
        [SerializeField] private Vector3 _extent = new Vector3(512f, 128f, 512f);

        [Header("Cell Settings")]
        [SerializeField] private float _cellSize = 8f;
        [SerializeField] private float _minCellSize = 1f;
        [SerializeField] private bool _overrideCellCounts;
        [SerializeField, Tooltip("Manual cell counts for X, Y, Z dimensions. Y dimension enables 3D spatial queries.")]
        private Vector3Int _manualCellCounts = new Vector3Int(64, 16, 64);
        [SerializeField, Tooltip("Legacy option for 2D games. Set to false for full 3D spatial partitioning (recommended for Space4X).")]
        private bool _lockYAxisToOne = false;

        [Header("Providers")]
        [SerializeField, Tooltip("Provider type name (e.g., 'HashedGrid', 'UniformGrid'). Uses enum for backward compatibility, auto-migrates to string.")]
        private SpatialProviderType _providerType = SpatialProviderType.HashedGrid;
        [SerializeField, Tooltip("Provider name string (overrides enum if set). Use this for custom providers.")]
        private string _providerName;
        [SerializeField] private uint _hashSeed = 0u;

        [Header("Rebuild Thresholds")]
        [SerializeField, Tooltip("Maximum dirty operations before forcing full rebuild (default: 1024)")]
        private int _maxDirtyOpsForPartialRebuild = 1024;
        [SerializeField, Tooltip("Maximum dirty ratio (0.0-1.0) before forcing full rebuild (default: 0.35)")]
        [Range(0f, 1f)]
        private float _maxDirtyRatioForPartialRebuild = 0.35f;
        [SerializeField, Tooltip("Minimum entry count required for partial rebuild logic (default: 100)")]
        [Min(0)]
        private int _minEntryCountForPartialRebuild = 100;

        [Header("Gizmos")]
        [SerializeField] private bool _drawGizmo = true;
        [SerializeField] private Color _gizmoColor = new Color(0f, 0.75f, 1f, 0.15f);

        public bool DrawGizmo => _drawGizmo;
        public Color GizmoColor => _gizmoColor;
        public int SchemaVersion => _schemaVersion;
        public Vector3 WorldMin => _center - _extent;
        public Vector3 WorldMax => _center + _extent;
        public Vector3 Center => _center;
        public Vector3 Extent => _extent;
        public float CellSize => Mathf.Max(_minCellSize, _cellSize);
        public float MinCellSize => _minCellSize;
        public bool OverrideCellCounts => _overrideCellCounts;
        public Vector3Int ManualCellCounts => _manualCellCounts;
        public bool LockYAxisToOne => _lockYAxisToOne;
        public SpatialProviderType Provider => _providerType;
        public string ProviderName
        {
            get
            {
                // Auto-migrate: if provider name is set, use it; otherwise convert enum to string
                if (!string.IsNullOrEmpty(_providerName))
                {
                    return _providerName;
                }
                return _providerType switch
                {
                    SpatialProviderType.HashedGrid => "HashedGrid",
                    SpatialProviderType.UniformGrid => "UniformGrid",
                    _ => "HashedGrid"
                };
            }
        }
        public uint HashSeed => _hashSeed;
        public int MaxDirtyOpsForPartialRebuild => Mathf.Max(0, _maxDirtyOpsForPartialRebuild);
        public float MaxDirtyRatioForPartialRebuild => Mathf.Clamp01(_maxDirtyRatioForPartialRebuild);
        public int MinEntryCountForPartialRebuild => Mathf.Max(0, _minEntryCountForPartialRebuild);

        private void OnValidate()
        {
            _extent.x = Mathf.Max(_minCellSize, _extent.x);
            _extent.y = Mathf.Max(_minCellSize, _extent.y);
            _extent.z = Mathf.Max(_minCellSize, _extent.z);
            _cellSize = Mathf.Max(_minCellSize, _cellSize);

            _manualCellCounts = SanitizeCellCounts(_manualCellCounts);

            if (_lockYAxisToOne)
            {
                _manualCellCounts.y = 1;
            }

            if (_schemaVersion != LatestSchemaVersion)
            {
                UpgradeSchema(_schemaVersion, LatestSchemaVersion);
            }
        }

        private void UpgradeSchema(int previousVersion, int targetVersion)
        {
            if (previousVersion < 1)
            {
                _minCellSize = 1f;
            }

            if (previousVersion < 2)
            {
                _providerType = SpatialProviderType.HashedGrid;
            }

            _schemaVersion = targetVersion;
        }

        public Bounds ToBounds()
        {
            return new Bounds(_center, _extent * 2f);
        }

        public SpatialGridConfig ToComponent()
        {
            var safeCellSize = math.max(_cellSize, _minCellSize);
            var counts = CalculateCellCounts((float3)(_extent * 2f), safeCellSize);
            var config = new SpatialGridConfig
            {
                WorldMin = (float3)WorldMin,
                WorldMax = (float3)WorldMax,
                CellSize = safeCellSize,
                CellCounts = counts,
                HashSeed = _hashSeed
            };

            // Try to resolve provider by name first (supports custom providers)
            // If not found, fall back to enum-based lookup for backward compatibility
            var providerName = ProviderName;
            var providerId = SpatialGridProviderIds.Hashed; // Default fallback

            // In runtime, we'll resolve provider ID from registry by name
            // For now, keep enum-based resolution for backward compatibility
            // The baker/bootstrapper will handle name-to-ID resolution
            switch (_providerType)
            {
                case SpatialProviderType.HashedGrid:
                    providerId = SpatialGridProviderIds.Hashed;
                    break;
                case SpatialProviderType.UniformGrid:
                    providerId = SpatialGridProviderIds.Uniform;
                    break;
                default:
                    providerId = SpatialGridProviderIds.Hashed;
                    break;
            }

            config.ProviderId = providerId;

            return config;
        }

        public void SetWorldBounds(Vector3 center, Vector3 extent)
        {
            _center = center;
            _extent = new Vector3(
                Mathf.Max(_minCellSize, Mathf.Abs(extent.x)),
                Mathf.Max(_minCellSize, Mathf.Abs(extent.y)),
                Mathf.Max(_minCellSize, Mathf.Abs(extent.z)));
        }

        public void SetCellSize(float value)
        {
            _cellSize = Mathf.Max(_minCellSize, value);
        }

        public void SetManualCellCounts(Vector3Int counts)
        {
            _manualCellCounts = SanitizeCellCounts(counts);
        }

        public void SetOverrideCellCounts(bool enabled)
        {
            _overrideCellCounts = enabled;
        }

        public void SetLockYAxisToOne(bool enabled)
        {
            _lockYAxisToOne = enabled;
        }

        public void SetProviderType(SpatialProviderType providerType)
        {
            _providerType = providerType;
        }

        public void SetMinCellSize(float value)
        {
            _minCellSize = Mathf.Max(0.001f, value);
            _cellSize = Mathf.Max(_minCellSize, _cellSize);
        }

        private int3 CalculateCellCounts(float3 extent, float safeCellSize)
        {
            if (_overrideCellCounts)
            {
                var counts = SanitizeCellCounts(_manualCellCounts);
                if (_lockYAxisToOne)
                {
                    counts.y = 1;
                }

                return new int3(counts.x, counts.y, counts.z);
            }

            var rawCounts = (int3)math.ceil(extent / safeCellSize);
            rawCounts = math.max(rawCounts, new int3(1, 1, 1));

            if (_lockYAxisToOne)
            {
                rawCounts.y = 1;
            }

            return rawCounts;
        }

        private Vector3Int SanitizeCellCounts(Vector3Int counts)
        {
            counts.x = Mathf.Max(1, counts.x);
            counts.y = Mathf.Max(1, counts.y);
            counts.z = Mathf.Max(1, counts.z);
            return counts;
        }
    }
}

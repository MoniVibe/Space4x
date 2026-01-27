using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Registry
{
    public enum RegistryResidency : byte
    {
        Unknown = 0,
        Runtime = 1,
        HybridBridge = 2,
        External = 3
    }

    public enum RegistryCategory : byte
    {
        Unknown = 0,
        Gameplay = 1,
        Presentation = 2,
        Meta = 3,
        Debug = 4
    }

    public struct RegistryContinuityMeta
    {
        public uint SchemaVersion;
        public RegistryResidency Residency;
        public RegistryCategory Category;

        public readonly RegistryContinuityMeta WithDefaultsIfUnset()
        {
            return new RegistryContinuityMeta
            {
                SchemaVersion = SchemaVersion == 0 ? 1u : SchemaVersion,
                Residency = Residency == RegistryResidency.Unknown ? RegistryResidency.Runtime : Residency,
                Category = Category == RegistryCategory.Unknown ? RegistryCategory.Gameplay : Category
            };
        }
    }

    public struct RegistryId : IEquatable<RegistryId>, IComparable<RegistryId>
    {
        public Hash128 Value;

        public readonly bool IsValid => Value.IsValid;

        public static RegistryId FromString(string raw)
        {
            if (!TryParse(raw, out var id, out _))
            {
                return default;
            }

            return id;
        }

        public static RegistryId FromFallback(in FixedString64Bytes label)
        {
            var str = label.ToString();
            return FromString(str);
        }

        public static RegistryId FromKind(RegistryKind kind, in FixedString64Bytes label)
        {
            var baseLabel = label.Length > 0 ? label.ToString() : kind.ToString();
            return FromString(baseLabel);
        }

        public static bool TryParse(string raw, out RegistryId id, out string sanitized)
        {
            sanitized = string.Empty;
            id = default;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var lower = raw.Trim().ToLowerInvariant();
            const int maxLength = 64;
            if (lower.Length > maxLength)
            {
                lower = lower.Substring(0, maxLength);
            }

            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(lower);
            var hashBytes = md5.ComputeHash(bytes);
            var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            var hash = new Hash128(hashHex);
            if (!hash.IsValid)
            {
                return false;
            }

            sanitized = lower;
            id = new RegistryId { Value = hash };
            return true;
        }

        public int CompareTo(RegistryId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(RegistryId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is RegistryId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    public struct RegistryTelemetryKey : IEquatable<RegistryTelemetryKey>
    {
        public FixedString64Bytes Key;
        public Hash128 Hash;

        public readonly bool IsValid => Hash.IsValid || Key.Length > 0;

        public static RegistryTelemetryKey FromString(string raw, RegistryId? registryId = null)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (registryId.HasValue && registryId.Value.IsValid)
                {
                    return new RegistryTelemetryKey
                    {
                        Key = new FixedString64Bytes("registry." + registryId.Value.Value.ToString()),
                        Hash = registryId.Value.Value
                    };
                }

                return default;
            }

            var trimmed = raw.Trim().ToLowerInvariant();
            if (trimmed.Length > 64)
            {
                trimmed = trimmed.Substring(0, 64);
            }

            if (!RegistryId.TryParse(trimmed, out var hashedKey, out var sanitized))
            {
                return default;
            }

            return new RegistryTelemetryKey
            {
                Key = new FixedString64Bytes(sanitized),
                Hash = hashedKey.Value
            };
        }

        public bool Equals(RegistryTelemetryKey other)
        {
            return Hash.Equals(other.Hash) && Key.Equals(other.Key);
        }

        public override bool Equals(object obj)
        {
            return obj is RegistryTelemetryKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }
    }

    public struct RegistryDefinition
    {
        public RegistryId Id;
        public FixedString64Bytes DisplayName;
        public ushort ArchetypeId;
        public RegistryTelemetryKey TelemetryKey;
        public RegistryContinuityMeta Continuity;
        public Hash128 HybridPrefabGuid;
    }

    public struct RegistryDefinitionBlob
    {
        public BlobArray<RegistryDefinition> Definitions;
    }

    public struct RegistryDefinitionCatalog : IComponentData
    {
        public BlobAssetReference<RegistryDefinitionBlob> Catalog;

        public readonly bool IsCreated => Catalog.IsCreated;
    }

    public static class RegistryDefinitionLookup
    {
        public static bool TryGetDefinition(this RegistryDefinitionCatalog catalog, RegistryId id, out RegistryDefinition definition)
        {
            definition = default;
            if (!catalog.IsCreated)
            {
                return false;
            }

            ref var blob = ref catalog.Catalog.Value;
            ref var definitions = ref blob.Definitions;
            for (var i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].Id.Equals(id))
                {
                    definition = definitions[i];
                    return true;
                }
            }

            return false;
        }
    }

    public struct RegistryContinuityValidationSummary
    {
        public int DuplicateIdCount;
        public int InvalidIdCount;
        public int VersionMismatchCount;
        public int ResidencyMismatchCount;

        public readonly bool IsValid =>
            DuplicateIdCount == 0 &&
            InvalidIdCount == 0 &&
            VersionMismatchCount == 0 &&
            ResidencyMismatchCount == 0;
    }

    public static class RegistryContinuityValidator
    {
        public static RegistryContinuityValidationSummary Validate(NativeArray<RegistryDefinition> definitions)
        {
            var summary = new RegistryContinuityValidationSummary();

            if (!definitions.IsCreated || definitions.Length == 0)
            {
                return summary;
            }

            var seenIds = new HashSet<RegistryId>();
            var versionByCategory = new Dictionary<RegistryCategory, uint>();
            var residencyByCategory = new Dictionary<RegistryCategory, RegistryResidency>();
            var versionMismatchCategories = new HashSet<RegistryCategory>();
            var residencyMismatchCategories = new HashSet<RegistryCategory>();

            for (var i = 0; i < definitions.Length; i++)
            {
                var def = definitions[i];
                if (!def.Id.IsValid)
                {
                    summary.InvalidIdCount++;
                }
                else if (!seenIds.Add(def.Id))
                {
                    summary.DuplicateIdCount++;
                }

                var continuity = def.Continuity.WithDefaultsIfUnset();
                var category = continuity.Category;
                if (category == RegistryCategory.Unknown)
                {
                    continue;
                }

                if (!versionByCategory.TryGetValue(category, out var version))
                {
                    versionByCategory[category] = continuity.SchemaVersion;
                }
                else if (version != continuity.SchemaVersion && versionMismatchCategories.Add(category))
                {
                    summary.VersionMismatchCount++;
                }

                if (!residencyByCategory.TryGetValue(category, out var residency))
                {
                    residencyByCategory[category] = continuity.Residency;
                }
                else if (residency != continuity.Residency && residencyMismatchCategories.Add(category))
                {
                    summary.ResidencyMismatchCount++;
                }
            }

            return summary;
        }
    }
}

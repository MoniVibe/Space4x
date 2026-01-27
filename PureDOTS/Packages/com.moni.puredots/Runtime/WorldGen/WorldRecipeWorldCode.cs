using System;
using System.IO;
using System.Text;

namespace PureDOTS.Runtime.WorldGen
{
    public static class WorldRecipeWorldCode
    {
        private static readonly byte[] s_magic = { (byte)'W', (byte)'G', (byte)'R', (byte)'1' };

        public static string Encode(WorldRecipeJson recipe)
        {
            var payload = EncodeToBytes(recipe);
            return WorldGenSchema.WorldRecipeWorldCodePrefix + ToBase64Url(payload);
        }

        public static bool TryDecode(string code, out WorldRecipeJson recipe, out string error)
        {
            recipe = default;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(code))
            {
                error = "World code is empty.";
                return false;
            }

            var trimmed = code.Trim();
            if (!trimmed.StartsWith(WorldGenSchema.WorldRecipeWorldCodePrefix, StringComparison.Ordinal))
            {
                error = $"World code missing prefix '{WorldGenSchema.WorldRecipeWorldCodePrefix}'.";
                return false;
            }

            var payloadText = trimmed.Substring(WorldGenSchema.WorldRecipeWorldCodePrefix.Length);
            if (!TryFromBase64Url(payloadText, out var payload, out error))
            {
                return false;
            }

            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

                var magic = reader.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != s_magic[0] || magic[1] != s_magic[1] || magic[2] != s_magic[2] || magic[3] != s_magic[3])
                {
                    error = "World code has invalid magic bytes.";
                    return false;
                }

                var formatVersion = reader.ReadUInt16();
                if (formatVersion != WorldGenSchema.WorldRecipeWorldCodeFormatVersion)
                {
                    error = $"Unsupported world code formatVersion {formatVersion}.";
                    return false;
                }

                recipe = new WorldRecipeJson
                {
                    schemaVersion = reader.ReadUInt32(),
                    worldSeed = reader.ReadUInt32(),
                    definitionsHash = ReadString(reader)
                };

                var stageCount = reader.ReadUInt16();
                recipe.stages = stageCount == 0 ? Array.Empty<WorldGenStageJson>() : new WorldGenStageJson[stageCount];

                for (int i = 0; i < stageCount; i++)
                {
                    var stage = new WorldGenStageJson
                    {
                        kind = ReadString(reader),
                        seedSalt = reader.ReadUInt32()
                    };

                    var paramCount = reader.ReadUInt16();
                    stage.parameters = paramCount == 0 ? Array.Empty<WorldGenParamJson>() : new WorldGenParamJson[paramCount];

                    for (int p = 0; p < paramCount; p++)
                    {
                        stage.parameters[p] = new WorldGenParamJson
                        {
                            key = ReadString(reader),
                            type = ReadString(reader),
                            floatValue = reader.ReadSingle(),
                            intValue = reader.ReadInt32(),
                            boolValue = reader.ReadBoolean(),
                            stringValue = ReadString(reader)
                        };
                    }

                    recipe.stages[i] = stage;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                recipe = default;
                return false;
            }

            if (!WorldRecipeMigration.TryMigrateInPlace(recipe, out error))
            {
                recipe = default;
                return false;
            }

            return true;
        }

        public static byte[] EncodeToBytes(WorldRecipeJson recipe)
        {
            if (recipe == null)
            {
                recipe = new WorldRecipeJson();
            }

            if (recipe.schemaVersion == 0)
            {
                recipe.schemaVersion = WorldGenSchema.WorldRecipeSchemaVersion;
            }

            using var ms = new MemoryStream(256);
            using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(s_magic);
                writer.Write(WorldGenSchema.WorldRecipeWorldCodeFormatVersion);

                writer.Write(recipe.schemaVersion);
                writer.Write(recipe.worldSeed);
                WriteString(writer, recipe.definitionsHash);

                var stages = recipe.stages ?? Array.Empty<WorldGenStageJson>();
                if (stages.Length > ushort.MaxValue)
                {
                    throw new InvalidOperationException($"Too many stages ({stages.Length}).");
                }

                writer.Write((ushort)stages.Length);
                for (int i = 0; i < stages.Length; i++)
                {
                    var stage = stages[i] ?? new WorldGenStageJson();
                    WriteString(writer, stage.kind);
                    writer.Write(stage.seedSalt);

                    var parameters = stage.parameters ?? Array.Empty<WorldGenParamJson>();
                    if (parameters.Length > ushort.MaxValue)
                    {
                        throw new InvalidOperationException($"Too many parameters ({parameters.Length}).");
                    }

                    writer.Write((ushort)parameters.Length);
                    for (int p = 0; p < parameters.Length; p++)
                    {
                        var param = parameters[p] ?? new WorldGenParamJson();
                        WriteString(writer, param.key);
                        WriteString(writer, param.type);
                        writer.Write(param.floatValue);
                        writer.Write(param.intValue);
                        writer.Write(param.boolValue);
                        WriteString(writer, param.stringValue);
                    }
                }
            }

            return ms.ToArray();
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException($"String is too large ({bytes.Length} bytes).");
            }

            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            var length = reader.ReadUInt16();
            if (length == 0)
            {
                return string.Empty;
            }

            var bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string ToBase64Url(byte[] bytes)
        {
            var base64 = Convert.ToBase64String(bytes ?? Array.Empty<byte>());
            return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static bool TryFromBase64Url(string base64Url, out byte[] bytes, out string error)
        {
            bytes = default;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(base64Url))
            {
                error = "Missing base64 payload.";
                return false;
            }

            var base64 = base64Url.Trim().Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
                case 0:
                    break;
                default:
                    error = "Invalid base64 payload length.";
                    return false;
            }

            try
            {
                bytes = Convert.FromBase64String(base64);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}


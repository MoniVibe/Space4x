using System;
using Unity.Collections;
using Unity.Entities;
using Hash128 = Unity.Entities.Hash128;

namespace PureDOTS.Runtime.WorldGen
{
    public static class WorldRecipeCompiler
    {
        public static bool TryCompile(
            WorldRecipeJson recipe,
            Allocator allocator,
            out WorldRecipeComponent compiled,
            out string error)
        {
            compiled = default;
            error = string.Empty;

            if (recipe == null)
            {
                error = "WorldRecipeJson is null.";
                return false;
            }

            if (!WorldRecipeMigration.TryMigrateInPlace(recipe, out error))
            {
                return false;
            }

            var stages = recipe.stages ?? Array.Empty<WorldGenStageJson>();

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<WorldRecipeBlob>();
            root.SchemaVersion = recipe.schemaVersion == 0 ? WorldGenSchema.WorldRecipeSchemaVersion : recipe.schemaVersion;
            root.WorldSeed = recipe.worldSeed;
            root.DefinitionsHash = ParseHash128(recipe.definitionsHash);

            var stageArray = builder.Allocate(ref root.Stages, stages.Length);
            for (int i = 0; i < stages.Length; i++)
            {
                var stageJson = stages[i] ?? new WorldGenStageJson();
                var kind = ParseStageKind(stageJson.kind);
                if (kind == WorldGenStageKind.Unknown)
                {
                    error = $"Unknown stage kind '{stageJson.kind ?? string.Empty}' at index {i}.";
                    return false;
                }

                stageArray[i].Kind = kind;
                stageArray[i].SeedSalt = stageJson.seedSalt;

                var paramJson = stageJson.parameters ?? Array.Empty<WorldGenParamJson>();
                var paramArray = builder.Allocate(ref stageArray[i].Parameters, paramJson.Length);
                for (int p = 0; p < paramJson.Length; p++)
                {
                    var param = paramJson[p] ?? new WorldGenParamJson();
                    if (string.IsNullOrWhiteSpace(param.key))
                    {
                        error = $"Missing parameter key at stage {i}, param {p}.";
                        return false;
                    }

                    var paramType = ParseParamType(param.type);
                    if (paramType == 0)
                    {
                        error = $"Unknown parameter type '{param.type ?? string.Empty}' for key '{param.key}' at stage {i}.";
                        return false;
                    }

                    paramArray[p] = new WorldGenParamBlob
                    {
                        Key = new FixedString64Bytes(param.key.Trim()),
                        Type = paramType,
                        FloatValue = param.floatValue,
                        IntValue = param.intValue,
                        BoolValue = (byte)(param.boolValue ? 1 : 0),
                        StringValue = new FixedString64Bytes((param.stringValue ?? string.Empty).Trim())
                    };
                }
            }

            var blob = builder.CreateBlobAssetReference<WorldRecipeBlob>(allocator);
            compiled = new WorldRecipeComponent
            {
                RecipeHash = WorldRecipeIo.ComputeRecipeHash(recipe),
                Recipe = blob
            };

            return true;
        }

        private static Hash128 ParseHash128(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return default;
            }

            var hash = new Hash128(raw.Trim());
            return hash.IsValid ? hash : default;
        }

        private static WorldGenStageKind ParseStageKind(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return WorldGenStageKind.Unknown;
            }

            var kind = raw.Trim().ToLowerInvariant();
            return kind switch
            {
                "surfacefields" => WorldGenStageKind.SurfaceFields,
                "surface_fields" => WorldGenStageKind.SurfaceFields,
                "surface-fields" => WorldGenStageKind.SurfaceFields,
                "topology" => WorldGenStageKind.Topology,
                "elevation" => WorldGenStageKind.Elevation,
                "climate" => WorldGenStageKind.Climate,
                "hydrology" => WorldGenStageKind.Hydrology,
                "biomes" => WorldGenStageKind.Biomes,
                "resources" => WorldGenStageKind.Resources,
                "ruins" => WorldGenStageKind.Ruins,
                _ => WorldGenStageKind.Unknown
            };
        }

        private static WorldGenParamType ParseParamType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return WorldGenParamType.Float;
            }

            var type = raw.Trim().ToLowerInvariant();
            return type switch
            {
                "float" => WorldGenParamType.Float,
                "int" => WorldGenParamType.Int,
                "bool" => WorldGenParamType.Bool,
                "string" => WorldGenParamType.FixedString64,
                "fixedstring" => WorldGenParamType.FixedString64,
                "fixedstring64" => WorldGenParamType.FixedString64,
                _ => 0
            };
        }
    }
}

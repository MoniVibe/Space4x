using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace PureDOTS.Runtime.WorldGen
{
    public static class WorldRecipeIo
    {
        public static bool TryFromJson(string json, out WorldRecipeJson recipe, out string error)
        {
            recipe = default;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Recipe JSON is empty.";
                return false;
            }

            try
            {
                recipe = JsonUtility.FromJson<WorldRecipeJson>(json);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (recipe == null)
            {
                error = "Failed to deserialize WorldRecipeJson.";
                return false;
            }

            if (!WorldRecipeMigration.TryMigrateInPlace(recipe, out error))
            {
                return false;
            }

            return true;
        }

        public static string ToJson(WorldRecipeJson recipe, bool prettyPrint = false)
        {
            if (recipe == null)
            {
                recipe = new WorldRecipeJson();
            }

            if (recipe.schemaVersion == 0)
            {
                recipe.schemaVersion = WorldGenSchema.WorldRecipeSchemaVersion;
            }

            return JsonUtility.ToJson(recipe, prettyPrint);
        }

        public static Hash128 ComputeRecipeHash(WorldRecipeJson recipe)
        {
            var payload = WorldRecipeWorldCode.EncodeToBytes(recipe);
            return ComputeStableHash(payload);
        }

        private static Hash128 ComputeStableHash(byte[] bytes)
        {
            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(bytes ?? Array.Empty<byte>());
            var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return new Hash128(hashHex);
        }
    }
}


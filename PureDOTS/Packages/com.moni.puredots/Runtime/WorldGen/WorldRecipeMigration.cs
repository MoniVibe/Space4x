namespace PureDOTS.Runtime.WorldGen
{
    public static class WorldRecipeMigration
    {
        public static bool TryMigrateInPlace(WorldRecipeJson recipe, out string error)
        {
            error = string.Empty;
            if (recipe == null)
            {
                error = "WorldRecipeJson is null.";
                return false;
            }

            if (recipe.schemaVersion == 0)
            {
                recipe.schemaVersion = 1;
            }

            if (recipe.schemaVersion > WorldGenSchema.WorldRecipeSchemaVersion)
            {
                error = $"Recipe schemaVersion {recipe.schemaVersion} is newer than supported {WorldGenSchema.WorldRecipeSchemaVersion}.";
                return false;
            }

            // No migrations yet; future versions should migrate forward here.
            recipe.schemaVersion = WorldGenSchema.WorldRecipeSchemaVersion;
            return true;
        }
    }
}


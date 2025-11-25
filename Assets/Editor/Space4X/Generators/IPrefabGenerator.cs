using Space4X.Editor;

namespace Space4X.Editor.Generators
{
    /// <summary>
    /// Interface for prefab generators that create thin prefabs from catalog data.
    /// </summary>
    public interface IPrefabGenerator
    {
        /// <summary>
        /// Generates or updates prefabs from catalog data.
        /// </summary>
        /// <param name="options">Generation options</param>
        /// <param name="result">Result accumulator</param>
        /// <returns>True if any assets were created or modified</returns>
        bool Generate(PrefabMakerOptions options, PrefabMaker.GenerationResult result);

        /// <summary>
        /// Validates generated prefabs against catalog data.
        /// </summary>
        /// <param name="report">Validation report accumulator</param>
        void Validate(PrefabMaker.ValidationReport report);
    }
}


using System.Collections;
using Digger.Modules.AdvancedOperations.Splines;
using Digger.Modules.AdvancedOperations.Splines.ProceduralGeneration;
using Digger.Modules.Core.Sources;
using Digger.Modules.Core.Sources.Operations;
using Digger.Modules.Runtime.Sources;
using UnityEngine;
using SplineWalker = Digger.Modules.AdvancedOperations.Sources.ModificationJobs.SplineWalker.SplineWalker;

namespace Digger
{
    /// <summary>
    /// Example script that demonstrates how to create a spline, generate a cave, and perform a spline operation at runtime.
    /// </summary>
    public class DiggerRuntimeSplineExample : MonoBehaviour
    {
        [Header("Spline Settings")]
        [Tooltip("Step size between operations along the spline")]
        public float step = 2f;

        [Tooltip("Minimum altitude variation for cave generation")]
        public float minY = -40f;

        [Tooltip("Maximum altitude variation for cave generation")]
        public float maxY = 40f;

        [Tooltip("Frequency of altitude variations")]
        public float altitudeVariationFrequency = 0.03f;

        [Tooltip("Frequency of horizontal variations")]
        public float horizontalVariationFrequency = 0.05f;

        [Tooltip("Distance between points in the spline")]
        public float splineStep = 4f;

        [Tooltip("Number of points in the spline")]
        public int stepCount = 100;

        [Header("Dig Operation Settings")]
        [Tooltip("Type of brush to use for the dig operation")]
        public BrushType brush = BrushType.Sphere;

        [Tooltip("Size of the brush")]
        public float brushSize = 5f;

        [Tooltip("Opacity/strength of the dig operation")]
        [Range(0f, 1f)]
        public float opacity = 0.8f;

        [Header("Player Settings")]
        [Tooltip("Reference to the player's transform. If null, will use Camera.main.transform")]
        public Transform playerTransform;

        [Tooltip("Key to press to create a new cave at player position")]
        public KeyCode createCaveKey = KeyCode.C;

        // SplineWalker instance that will be created once and reused
        private SplineWalker splineWalker;
        private bool isCreatingCave;

        private void Awake()
        {
            // Get all DiggerSystem instances
            var diggerSystems = FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);

            // Create the SplineWalker once
            splineWalker = new SplineWalker(diggerSystems);

            // If player transform is not set, use the main camera
            if (playerTransform == null && Camera.main != null)
            {
                playerTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            // Check if the create cave key is pressed and we're not already creating a cave
            if (Input.GetKeyDown(createCaveKey) && !isCreatingCave)
            {
                isCreatingCave = true;
                StartCoroutine(CreateRandomCave(playerTransform.position));
            }
        }

        /// <summary>
        /// Creates a new random cave at the specified position
        /// </summary>
        /// <param name="position">Starting position for the cave</param>
        public async Awaitable CreateRandomCave(Vector3 position)
        {
            // Randomize some parameters for variety
            float randomMinY = Random.Range(minY * 0.8f, minY * 1.2f);
            float randomMaxY = Random.Range(maxY * 0.8f, maxY * 1.2f);
            float randomAltFreq = Random.Range(altitudeVariationFrequency * 0.7f, altitudeVariationFrequency * 1.3f);
            float randomHorizFreq = Random.Range(horizontalVariationFrequency * 0.7f, horizontalVariationFrequency * 1.3f);
            int randomSeed1 = Random.Range(1, 10000);
            int randomSeed2 = Random.Range(1, 10000);
            int randomSeed3 = Random.Range(1, 10000);

            Debug.Log($"Creating new random cave at position {position}");
            await CreateSplineAndDigCaveCoroutine(position, randomMinY, randomMaxY, randomAltFreq, randomHorizFreq, randomSeed1, randomSeed2, randomSeed3);
            isCreatingCave = false;
        }

        /// <summary>
        /// Creates a new spline, generates a cave along it, and performs a dig operation along the spline.
        /// </summary>
        /// <param name="startPosition">The starting position for the spline</param>
        /// <param name="minYValue">Minimum altitude variation</param>
        /// <param name="maxYValue">Maximum altitude variation</param>
        /// <param name="altFreq">Altitude variation frequency</param>
        /// <param name="horizFreq">Horizontal variation frequency</param>
        /// <param name="seed1">Seed for X coordinate noise</param>
        /// <param name="seed2">Seed for Z coordinate noise</param>
        /// <param name="seed3">Seed for Y coordinate noise</param>
        /// <returns>Coroutine that performs the operation</returns>
        private async Awaitable CreateSplineAndDigCaveCoroutine(Vector3 startPosition, float minYValue, float maxYValue,
            float altFreq, float horizFreq, int seed1, int seed2, int seed3)
        {
            if (splineWalker == null)
            {
                Debug.LogError("SplineWalker is not initialized. Make sure Awake() has been called.");
                return;
            }

            // Create a new spline
            var splineGO = new GameObject("RuntimeSpline");
            var spline = splineGO.AddComponent<BezierSpline>();
            splineGO.transform.position = startPosition;

            // Configure spline parameters with randomized values
            spline.minY = minYValue;
            spline.maxY = maxYValue;
            spline.altitudeVariationFrequency = altFreq;
            spline.horizontalVariationFrequency = horizFreq;
            spline.step = splineStep;
            spline.stepCount = stepCount;
            spline.seed1 = seed1;
            spline.seed2 = seed2;
            spline.seed3 = seed3;

            // Generate the cave along the spline
            var generator = new CaveGenerator(
                spline.step,
                spline.stepCount,
                spline.minY,
                spline.maxY,
                spline.altitudeVariationFrequency,
                spline.horizontalVariationFrequency,
                spline.seed1,
                spline.seed2,
                spline.seed3
            );

            generator.GeneratePoints(startPosition, spline);

            // Create parameters for the dig operation
            var parameters = new ModificationParameters
            {
                Brush = brush,
                Action = ActionType.Dig,
                TextureIndex = 0, // Use the first texture
                Opacity = opacity,
                Size = new Vector3(brushSize, brushSize, brushSize)
            };

            // Use the existing SplineWalker to walk along the spline
            await splineWalker.WalkAlongSpline(spline, step, (position) =>
            {
                parameters.Position = position;
                return new BasicOperation { Params = parameters };
            }, true);

            Debug.Log("Cave creation completed along the spline!");
        }
    }
}
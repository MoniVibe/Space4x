using System;
using UnityEngine;
using UCamera = UnityEngine.Camera;
using UObject = UnityEngine.Object;

namespace Space4X.Presentation
{
    [DisallowMultipleComponent]
    public sealed class Space4XStarfield : MonoBehaviour
    {
        private const int MinStars = 300;
        private const int MaxStars = 5000;
        private const float InnerRadiusRatio = 0.35f;
        private const float NebulaCountRatio = 0.05f;
        private const int NebulaMinCount = 24;
        private const int NebulaMaxCount = 180;
        private const float NebulaClusterRadiusRatio = 0.25f;
        private const float NebulaSizeRatio = 0.03f;
        private const float NebulaParallaxScale = 0.4f;

        [Header("Target")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private UCamera targetCamera;
        [SerializeField] private bool disableInBatchMode = true;

        [Header("Field")]
        [SerializeField] private float fieldRadius = 900f;
        [SerializeField] private float parallaxFactor = 0.06f;

        [Header("Stars")]
        // Density is per million cubic units to keep inspector values reasonable.
        [SerializeField] private float starDensityPerMillion = 0.4f;
        [SerializeField] private float minStarSize = 0.6f;
        [SerializeField] private float maxStarSize = 2.2f;
        [SerializeField] private Color starTint = new Color(0.9f, 0.95f, 1f, 1f);
        [SerializeField] private AnimationCurve brightnessCurve = new AnimationCurve(
            new Keyframe(0f, 0.1f),
            new Keyframe(0.6f, 0.35f),
            new Keyframe(1f, 1f));

        [Header("Nebula")]
        [SerializeField] private float nebulaIntensity = 0.35f;
        [SerializeField] private Color nebulaTint = new Color(0.45f, 0.6f, 1f, 1f);

        [Header("Materials")]
        [SerializeField] private Material starMaterialTemplate;
        [SerializeField] private Material nebulaMaterialTemplate;

        [Header("Warp")]
        [SerializeField] private bool enableWarpStreaks = true;
        [SerializeField] private float warpSpeedThreshold = 60f;
        [SerializeField] private float warpMaxStretch = 4f;

        [Header("Random")]
        [SerializeField] private int seed = 1337;
        [SerializeField] private bool randomizeSeed;

        private Transform _followTransform;
        private Vector3 _lastCameraPosition;
        private bool _hasLastCameraPosition;

        private ParticleSystem _starSystem;
        private ParticleSystemRenderer _starRenderer;
        private ParticleSystem.Particle[] _starParticles;
        private Vector3[] _starBasePositions;

        private ParticleSystem _nebulaSystem;
        private ParticleSystemRenderer _nebulaRenderer;
        private ParticleSystem.Particle[] _nebulaParticles;
        private Vector3[] _nebulaBasePositions;

        private Texture2D _radialTexture;
        private Material _starMaterial;
        private Material _nebulaMaterial;

        private bool _needsRebuild = true;

        private void OnEnable()
        {
            if (disableInBatchMode && Application.isBatchMode)
            {
                enabled = false;
                return;
            }

            ResolveFollowTarget();
            _needsRebuild = true;
            RebuildIfNeeded();
        }

        private void OnValidate()
        {
            _needsRebuild = true;

            if (Application.isPlaying)
            {
                RebuildIfNeeded();
            }
        }

        private void LateUpdate()
        {
            if (_needsRebuild)
            {
                RebuildIfNeeded();
            }

            if (_followTransform == null || _starSystem == null)
            {
                return;
            }

            var cameraPosition = _followTransform.position;
            transform.position = cameraPosition;

            var cameraVelocity = CalculateCameraVelocity(cameraPosition);
            UpdateWarp(cameraVelocity);
            UpdateParticles(cameraPosition, cameraVelocity);
        }

        private void RebuildIfNeeded()
        {
            if (!_needsRebuild)
            {
                return;
            }

            ResolveFollowTarget();
            if (_followTransform == null)
            {
                return;
            }

            ValidateSettings();
            EnsureMaterials();
            if (_starMaterial == null)
            {
                return;
            }

            var innerRadius = fieldRadius * InnerRadiusRatio;
            var starCount = CalculateStarCount(innerRadius, fieldRadius);
            BuildStarLayer(starCount, innerRadius, fieldRadius);

            if (nebulaIntensity > 0.001f)
            {
                var nebulaCount = CalculateNebulaCount(starCount);
                BuildNebulaLayer(nebulaCount, fieldRadius);
            }
            else if (_nebulaSystem != null)
            {
                _nebulaSystem.gameObject.SetActive(false);
            }

            _needsRebuild = false;
        }

        private void ResolveFollowTarget()
        {
            if (followTarget != null)
            {
                _followTransform = followTarget;
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = UCamera.main;
                if (targetCamera == null)
                {
                    targetCamera = UObject.FindObjectOfType<UCamera>();
                }
            }

            _followTransform = targetCamera != null ? targetCamera.transform : null;
        }

        private void ValidateSettings()
        {
            fieldRadius = Mathf.Max(10f, fieldRadius);
            parallaxFactor = Mathf.Clamp01(parallaxFactor);
            starDensityPerMillion = Mathf.Max(0f, starDensityPerMillion);
            minStarSize = Mathf.Max(0.01f, minStarSize);
            maxStarSize = Mathf.Max(minStarSize, maxStarSize);
            nebulaIntensity = Mathf.Clamp01(nebulaIntensity);
            warpSpeedThreshold = Mathf.Max(0.1f, warpSpeedThreshold);
            warpMaxStretch = Mathf.Max(0f, warpMaxStretch);
        }

        private Vector3 CalculateCameraVelocity(Vector3 cameraPosition)
        {
            if (!_hasLastCameraPosition)
            {
                _lastCameraPosition = cameraPosition;
                _hasLastCameraPosition = true;
                return Vector3.zero;
            }

            float dt = Mathf.Max(0.001f, Time.deltaTime);
            var velocity = (cameraPosition - _lastCameraPosition) / dt;
            _lastCameraPosition = cameraPosition;
            return velocity;
        }

        private void UpdateWarp(Vector3 cameraVelocity)
        {
            if (_starRenderer == null)
            {
                return;
            }

            if (!enableWarpStreaks)
            {
                _starRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                _starRenderer.velocityScale = 0f;
                _starRenderer.lengthScale = 1f;
                return;
            }

            _starRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            _starRenderer.lengthScale = 1f;

            float speed = cameraVelocity.magnitude;
            float warpT = Mathf.Clamp01((speed - warpSpeedThreshold) / warpSpeedThreshold);
            _starRenderer.velocityScale = warpT * warpMaxStretch;
        }

        private void UpdateParticles(Vector3 cameraPosition, Vector3 cameraVelocity)
        {
            if (_starParticles == null || _starBasePositions == null)
            {
                return;
            }

            var parallaxOffset = -cameraPosition * parallaxFactor;
            var warpDirection = Vector3.zero;
            if (enableWarpStreaks && cameraVelocity.sqrMagnitude > 0.001f && _starRenderer.velocityScale > 0.001f)
            {
                warpDirection = -cameraVelocity.normalized;
            }

            for (int i = 0; i < _starParticles.Length; i++)
            {
                _starParticles[i].position = _starBasePositions[i] + parallaxOffset;
                _starParticles[i].velocity = warpDirection;
            }

            _starSystem.SetParticles(_starParticles, _starParticles.Length);

            if (_nebulaSystem == null || !_nebulaSystem.gameObject.activeSelf)
            {
                return;
            }

            var nebulaOffset = parallaxOffset * NebulaParallaxScale;
            for (int i = 0; i < _nebulaParticles.Length; i++)
            {
                _nebulaParticles[i].position = _nebulaBasePositions[i] + nebulaOffset;
            }

            _nebulaSystem.SetParticles(_nebulaParticles, _nebulaParticles.Length);
        }

        private int CalculateStarCount(float innerRadius, float outerRadius)
        {
            float innerCube = innerRadius * innerRadius * innerRadius;
            float outerCube = outerRadius * outerRadius * outerRadius;
            float volume = (4f / 3f) * Mathf.PI * (outerCube - innerCube);
            int count = Mathf.RoundToInt(starDensityPerMillion * volume * 1e-6f);
            return Mathf.Clamp(count, MinStars, MaxStars);
        }

        private int CalculateNebulaCount(int starCount)
        {
            int count = Mathf.RoundToInt(starCount * NebulaCountRatio);
            return Mathf.Clamp(count, NebulaMinCount, NebulaMaxCount);
        }

        private void BuildStarLayer(int count, float innerRadius, float outerRadius)
        {
            EnsureStarSystem(count);
            var rng = CreateRandom();
            var curve = GetBrightnessCurve();

            _starBasePositions = new Vector3[count];
            _starParticles = new ParticleSystem.Particle[count];

            for (int i = 0; i < count; i++)
            {
                var position = RandomPointInShell(ref rng, innerRadius, outerRadius);
                float brightnessT = (float)rng.NextDouble();
                float brightness = Mathf.Clamp01(curve.Evaluate(brightnessT));
                float size = Mathf.Lerp(minStarSize, maxStarSize, brightness);
                var color = new Color(starTint.r * brightness, starTint.g * brightness, starTint.b * brightness, brightness);

                _starBasePositions[i] = position;
                _starParticles[i] = new ParticleSystem.Particle
                {
                    position = position,
                    startColor = color,
                    startSize = size,
                    startLifetime = float.MaxValue,
                    remainingLifetime = float.MaxValue
                };
            }

            _starSystem.SetParticles(_starParticles, _starParticles.Length);
        }

        private void BuildNebulaLayer(int count, float outerRadius)
        {
            EnsureNebulaSystem(count);
            var rng = CreateRandom();

            _nebulaSystem.gameObject.SetActive(true);
            _nebulaBasePositions = new Vector3[count];
            _nebulaParticles = new ParticleSystem.Particle[count];

            var clusterRadius = outerRadius * NebulaClusterRadiusRatio;
            var clusterCenters = new Vector3[3];
            for (int i = 0; i < clusterCenters.Length; i++)
            {
                clusterCenters[i] = RandomPointInShell(ref rng, outerRadius * 0.5f, outerRadius * 0.9f);
            }

            float baseSize = Mathf.Max(2f, outerRadius * NebulaSizeRatio);
            for (int i = 0; i < count; i++)
            {
                int clusterIndex = rng.Next(0, clusterCenters.Length);
                var offset = RandomInsideSphere(ref rng) * clusterRadius;
                var position = clusterCenters[clusterIndex] + offset;
                float alpha = nebulaIntensity * Mathf.Lerp(0.2f, 0.6f, (float)rng.NextDouble());
                float size = baseSize * Mathf.Lerp(0.6f, 1.4f, (float)rng.NextDouble());
                var color = new Color(nebulaTint.r, nebulaTint.g, nebulaTint.b, alpha);

                _nebulaBasePositions[i] = position;
                _nebulaParticles[i] = new ParticleSystem.Particle
                {
                    position = position,
                    startColor = color,
                    startSize = size,
                    startLifetime = float.MaxValue,
                    remainingLifetime = float.MaxValue
                };
            }

            _nebulaSystem.SetParticles(_nebulaParticles, _nebulaParticles.Length);
        }

        private void EnsureStarSystem(int maxParticles)
        {
            if (_starSystem == null)
            {
                var starObject = new GameObject("Starfield_Stars");
                starObject.transform.SetParent(transform, false);
                _starSystem = starObject.AddComponent<ParticleSystem>();
            }

            if (_starRenderer == null && _starSystem != null)
            {
                _starRenderer = _starSystem.GetComponent<ParticleSystemRenderer>();
            }

            ConfigureParticleSystem(_starSystem, _starRenderer, maxParticles, _starMaterial, enableWarpStreaks);
        }

        private void EnsureNebulaSystem(int maxParticles)
        {
            if (_nebulaSystem == null)
            {
                var nebulaObject = new GameObject("Starfield_Nebula");
                nebulaObject.transform.SetParent(transform, false);
                _nebulaSystem = nebulaObject.AddComponent<ParticleSystem>();
            }

            if (_nebulaRenderer == null && _nebulaSystem != null)
            {
                _nebulaRenderer = _nebulaSystem.GetComponent<ParticleSystemRenderer>();
            }

            ConfigureParticleSystem(_nebulaSystem, _nebulaRenderer, maxParticles, _nebulaMaterial, false);
        }

        private static void ConfigureParticleSystem(ParticleSystem system, ParticleSystemRenderer renderer, int maxParticles, Material material, bool stretched)
        {
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.simulationSpeed = 0f;
            main.maxParticles = maxParticles;
            main.startLifetime = float.MaxValue;
            main.startSpeed = 0f;
            main.startSize = 1f;

            var emission = system.emission;
            emission.enabled = false;

            renderer.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = 1f;
            renderer.velocityScale = 0f;
            if (material != null)
            {
                renderer.material = material;
            }
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 1f;
        }

        private void EnsureMaterials()
        {
            if (_radialTexture == null)
            {
                _radialTexture = CreateRadialTexture(32, 2.4f);
            }

            if (_starMaterial == null)
            {
                _starMaterial = BuildMaterial(starMaterialTemplate, _radialTexture);
            }

            if (_nebulaMaterial == null)
            {
                var template = nebulaMaterialTemplate != null ? nebulaMaterialTemplate : starMaterialTemplate;
                _nebulaMaterial = BuildMaterial(template, _radialTexture);
            }

            if (_starMaterial != null && _nebulaMaterial != null)
            {
                _nebulaMaterial.renderQueue = _starMaterial.renderQueue - 10;
            }
        }

        private static Material BuildMaterial(Material template, Texture2D texture)
        {
            if (template == null)
            {
                return null;
            }

            var material = new Material(template)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            material.SetTexture(GetTextureProperty(material), texture);
            SetColorProperty(material, Color.white);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            return material;
        }

        private static string GetTextureProperty(Material material)
        {
            if (material.HasProperty("_BaseMap"))
            {
                return "_BaseMap";
            }

            if (material.HasProperty("_MainTex"))
            {
                return "_MainTex";
            }

            return "_BaseMap";
        }

        private static void SetColorProperty(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static Texture2D CreateRadialTexture(int size, float falloffPower)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDistance = center.magnitude;
            var color = new Color(1f, 1f, 1f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), falloffPower);
                    color.a = alpha;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        private System.Random CreateRandom()
        {
            int resolvedSeed = randomizeSeed ? global::System.Environment.TickCount : seed;
            return new System.Random(resolvedSeed);
        }

        private AnimationCurve GetBrightnessCurve()
        {
            if (brightnessCurve == null || brightnessCurve.length == 0)
            {
                brightnessCurve = new AnimationCurve(
                    new Keyframe(0f, 0.1f),
                    new Keyframe(0.6f, 0.35f),
                    new Keyframe(1f, 1f));
            }

            return brightnessCurve;
        }

        private static Vector3 RandomPointInShell(ref System.Random rng, float innerRadius, float outerRadius)
        {
            var direction = RandomUnitVector(ref rng);
            float innerCube = innerRadius * innerRadius * innerRadius;
            float outerCube = outerRadius * outerRadius * outerRadius;
            float t = (float)rng.NextDouble();
            float radius = Mathf.Pow(Mathf.Lerp(innerCube, outerCube, t), 1f / 3f);
            return direction * radius;
        }

        private static Vector3 RandomInsideSphere(ref System.Random rng)
        {
            var direction = RandomUnitVector(ref rng);
            float radius = Mathf.Pow((float)rng.NextDouble(), 1f / 3f);
            return direction * radius;
        }

        private static Vector3 RandomUnitVector(ref System.Random rng)
        {
            float z = (float)rng.NextDouble() * 2f - 1f;
            float theta = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), z);
        }
    }
}

using PureDOTS.Runtime.Environment;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Runtime.MonoBehaviours
{
    /// <summary>
    /// Mono bridge that updates Unity directional light and skybox from ECS LightingState.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightingPresentationBridge : MonoBehaviour
    {
        [Header("Lighting")]
        [Tooltip("Directional light to update (main sun light).")]
        public Light directionalLight;

        [Tooltip("Skybox material to update (optional).")]
        public Material skyboxMaterial;

        [Header("Settings")]
        [Tooltip("Maximum light intensity.")]
        public float maxIntensity = 1f;

        [Tooltip("Minimum ambient intensity.")]
        public float minAmbientIntensity = 0.2f;

        private World _world;
        private EntityQuery _lightingQuery;

        private void Start()
        {
            if (directionalLight == null)
            {
                directionalLight = GetComponent<Light>();
            }

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world != null && _world.IsCreated)
            {
                _lightingQuery = _world.EntityManager.CreateEntityQuery(typeof(LightingState));
            }
        }

        private void Update()
        {
            if (_world == null || !_world.IsCreated || _lightingQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var lightingState = _lightingQuery.GetSingleton<LightingState>();

            // Update directional light
            if (directionalLight != null)
            {
                // Convert sun angle to direction (sun moves in XZ plane, Y is up)
                float sunDirX = math.cos(lightingState.SunAngle);
                float sunDirY = math.sin(lightingState.SunAngle);
                float sunDirZ = 0f;

                directionalLight.transform.rotation = Quaternion.LookRotation(new Vector3(-sunDirX, -sunDirY, -sunDirZ));
                directionalLight.intensity = lightingState.SunIntensity * maxIntensity;
                directionalLight.color = new Color(lightingState.SunColor.x, lightingState.SunColor.y, lightingState.SunColor.z);

                // Update ambient intensity
                RenderSettings.ambientIntensity = minAmbientIntensity + lightingState.AmbientIntensity;
            }

            // Update skybox if provided
            if (skyboxMaterial != null)
            {
                // Simple skybox tint based on sun angle
                float skyTint = lightingState.SunIntensity;
                skyboxMaterial.SetFloat("_Exposure", 0.5f + skyTint * 0.5f);
            }
        }
    }
}


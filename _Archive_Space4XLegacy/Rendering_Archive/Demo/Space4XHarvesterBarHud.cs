using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Space4X.Registry;

namespace Space4X.Demo
{
    /// <summary>
    /// Draws Red Alert 2-style cargo bars above mining vessels in world space.
    /// Bars show cargo fill level and change color from green (empty) → yellow (half) → red (full).
    /// </summary>
    public class Space4XHarvesterBarHud : MonoBehaviour
    {
        private World _world;
        private EntityManager _em;
        private EntityQuery _minerQuery;
        private Camera _camera;

        [Header("Bar layout")]
        [Tooltip("Width of the cargo bar in screen pixels")]
        public float barWidth = 60f;
        
        [Tooltip("Height of the cargo bar in screen pixels")]
        public float barHeight = 6f;
        
        [Tooltip("Pixels above the unit on screen")]
        public float barOffset = 30f;

        private void Start()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                Debug.LogWarning("[Space4XHarvesterBarHud] No DefaultWorld found.");
                return;
            }

            _em = _world.EntityManager;

            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogWarning("[Space4XHarvesterBarHud] No Camera.main found.");
            }

            // Query for miners with MiningOrder and MinerUiData (lightweight UI data computed by ECS system)
            _minerQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<MiningOrder>(),
                ComponentType.ReadOnly<MinerUiData>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
        }

        private void OnGUI()
        {
            if (_world == null || !_world.IsCreated || _camera == null)
                return;

            if (_minerQuery.IsEmpty)
                return;

            using var miners = _minerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var uiData = _minerQuery.ToComponentDataArray<MinerUiData>(Unity.Collections.Allocator.Temp);
            using var transforms = _minerQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < miners.Length; i++)
            {
                var xf = transforms[i];
                var ui = uiData[i];

                // 1) World → screen
                float3 worldPos = xf.Position;
                var screenPos = _camera.WorldToScreenPoint(worldPos);

                // Behind camera? Skip.
                if (screenPos.z < 0f)
                    continue;

                // Convert to GUI coordinates (top-left origin)
                float guiX = screenPos.x;
                float guiY = Screen.height - screenPos.y - barOffset;

                // 2) Use precomputed fullness from MinerUiData (computed by ECS system, much cheaper)
                float fill = ui.Fullness01;

                // 3) Rects
                var bgRect = new Rect(guiX - barWidth * 0.5f, guiY, barWidth, barHeight);
                var fgRect = new Rect(bgRect.x, bgRect.y, barWidth * fill, barHeight);

                // 4) Draw background (black with transparency)
                var oldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

                // 5) Draw foreground (green → yellow → red based on cargo fullness)
                Color barColor;
                if (fill < 0.5f)
                {
                    // Empty to half: green → yellow
                    barColor = Color.Lerp(Color.green, Color.yellow, fill * 2f);
                }
                else
                {
                    // Half to full: yellow → red
                    barColor = Color.Lerp(Color.yellow, Color.red, (fill - 0.5f) * 2f);
                }
                GUI.color = barColor;
                GUI.DrawTexture(fgRect, Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
        }
    }
}


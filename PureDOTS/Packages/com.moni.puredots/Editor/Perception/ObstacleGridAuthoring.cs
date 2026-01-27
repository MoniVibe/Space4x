using PureDOTS.Runtime.Perception;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Editor.Perception
{
    /// <summary>
    /// Unity authoring component for marking GameObjects as obstacles.
    /// Adds ObstacleTag and ObstacleHeight (from collider bounds) at bake time.
    /// Allows manual height override.
    /// </summary>
    public class ObstacleGridAuthoring : MonoBehaviour
    {
        [Tooltip("Explicit height override. If 0, height is calculated from collider bounds.")]
        public float HeightOverride = 0f;

        [Tooltip("If true, calculates height from collider bounds (ignores HeightOverride).")]
        public bool UseColliderBounds = true;

        /// <summary>
        /// Baker for ObstacleGridAuthoring.
        /// </summary>
        public class ObstacleGridBaker : Baker<ObstacleGridAuthoring>
        {
            public override void Bake(ObstacleGridAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add ObstacleTag marker
                AddComponent<ObstacleTag>(entity);

                // Calculate or use override height
                float obstacleHeight = authoring.HeightOverride;
                if (authoring.UseColliderBounds)
                {
                    // Try to get height from collider bounds
                    var collider = authoring.GetComponent<Collider>();
                    if (collider != null)
                    {
                        var bounds = collider.bounds;
                        obstacleHeight = bounds.size.y;
                    }
                    else
                    {
                        // Fallback: use default height
                        obstacleHeight = 1f;
                    }
                }

                if (obstacleHeight > 0f)
                {
                    AddComponent(entity, new ObstacleHeight
                    {
                        Height = obstacleHeight
                    });
                }
            }
        }
    }
}




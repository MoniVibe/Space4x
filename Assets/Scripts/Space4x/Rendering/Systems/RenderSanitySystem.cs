using Unity.Entities;
using UnityEngine;
using Space4X.Rendering;

namespace Space4X.Rendering.Systems
{
    /// <summary>
    /// One-shot sanity check for RenderKey presence/visibility in the default world.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    public partial struct RenderSanitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = true;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!Application.isPlaying)
                return;

            var q = SystemAPI.QueryBuilder()
                .WithAll<RenderKey>()
                .Build();

            int count = q.CalculateEntityCount();

            if (count == 0)
            {
                Debug.LogError("[RenderSanitySystem] No RenderKey entities exist; nothing can render.");
            }
            else
            {
                Debug.Log($"[RenderSanitySystem] World '{state.WorldUnmanaged.Name}' has {count} RenderKey entities.");
            }

            // Log once.
            state.Enabled = false;
        }
    }
}

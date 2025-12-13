using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Space4X.Rendering;
using PureDOTS.Runtime.Core;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Space4X.Rendering.Systems
{
    using Debug = UnityEngine.Debug;

    
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
            if (RuntimeMode.IsHeadless)
            {
                state.Enabled = false;
                return;
            }

            var q = SystemAPI.QueryBuilder()
                .WithAll<RenderKey>()
                .Build();

            int count = q.CalculateEntityCount();

            if (count == 0)
            {
#if UNITY_EDITOR
                LogMissing(state.WorldUnmanaged.Name);
#endif
            }
            else
            {
#if UNITY_EDITOR
                LogCount(state.WorldUnmanaged.Name, count);
#endif
            }

            // Log once.
            state.Enabled = false;
        }

#if UNITY_EDITOR
        [BurstDiscard]
        static void LogMissing(FixedString128Bytes worldName)
        {
            Debug.LogError($"[RenderSanitySystem] No RenderKey entities exist; nothing can render (world '{worldName}').");
        }

        [BurstDiscard]
        static void LogCount(FixedString128Bytes worldName, int count)
        {
            Debug.Log($"[RenderSanitySystem] World '{worldName}' has {count} RenderKey entities.");
        }
#endif
    }
}

#endif

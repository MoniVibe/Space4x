#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for debug command singleton.
    /// Optional - only needed if you want to send debug commands from DOTS systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugCommandAuthoring : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Create debug command buffer on startup")]
        public bool createOnStartup = true;
    }

    public sealed class DebugCommandBaker : Baker<DebugCommandAuthoring>
    {
        public override void Bake(DebugCommandAuthoring authoring)
        {
            if (!authoring.createOnStartup)
            {
                return;
            }

            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddBuffer<DebugCommand>(entity);
            AddComponent<DebugCommandSingletonTag>(entity);
        }
    }
}
#endif





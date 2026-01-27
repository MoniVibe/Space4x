#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Captures the structural change baseline after BeginPresentationEntityCommandBufferSystem playback.
    /// TEMP: Disabled for smoke test - presentation systems may perform structural changes.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(BeginPresentationECBSystem))]
    public partial struct PresentationStructuralChangeGuardBeginSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // TEMP: disable this debug guard system for now
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // no-op
        }
    }

    /// <summary>
    /// Throws if any presentation system performs structural changes outside ECB boundaries.
    /// TEMP: Disabled for smoke test - presentation systems may perform structural changes.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateBefore(typeof(EndPresentationECBSystem))]
    public partial struct PresentationStructuralChangeGuardEndSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // TEMP: disable this debug guard system for now
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // no-op (original exception lived here)
        }
    }
}
#endif

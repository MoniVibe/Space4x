using Unity.Entities;

namespace PureDOTS.Runtime.Hand
{
    /// <summary>
    /// Controls pickup/hold/throw eligibility rules for the hand pipeline.
    /// </summary>
    public struct HandPickupPolicy : IComponentData
    {
        public byte AutoPickDynamicPhysics;
        public byte EnableWorldGrab;
        public byte DebugWorldGrabAny;
        public byte WorldGrabRequiresTag;
    }

    /// <summary>
    /// Allowlist tag for world-grab targets (planetoids, nebula volumes, etc.).
    /// </summary>
    public struct WorldManipulableTag : IComponentData { }

    /// <summary>
    /// Hard deny tag for pickup/throw, even in debug world-grab mode.
    /// </summary>
    public struct NeverPickableTag : IComponentData { }
}

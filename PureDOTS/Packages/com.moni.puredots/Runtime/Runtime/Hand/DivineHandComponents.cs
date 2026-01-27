using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    public struct DivineHandTag : IComponentData { }

    /// <summary>
    /// Tag component marking an entity as being held by a divine hand.
    /// </summary>
    public struct HandHeldTag : IComponentData
    {
        public Entity Holder;
    }

    /// <summary>
    /// [OBSOLETE] Legacy divine hand state component. Use PureDOTS.Runtime.Hand.HandState instead.
    /// Migration: Read from PureDOTS.Runtime.Hand.HandState for authoritative state.
    /// </summary>
    [System.Obsolete("Use PureDOTS.Runtime.Hand.HandState instead. This component is deprecated and will be removed in a future version.")]
    public struct DivineHandState : IComponentData
    {
        public float3 CursorPosition;
        public float3 CursorNormal;
        public Entity HoveredEntity;
        
        // Reintroduced fields used by Godgame miracle systems
        public Entity HeldEntity;
        public ushort HeldResourceTypeIndex;
        public float HeldAmount;
        
        // Throw mode and hand state
        public bool ThrowModeEnabled;
        public float3 HeldOffset;
        public float3 LastAimDirection;
        public float LastStrength;
    }

    /// <summary>
    /// Tag component marking an entity as pickable by the divine hand.
    /// </summary>
    public struct PickableTag : IComponentData { }

    /// <summary>
    /// Alias for PickableTag (plan uses "Pickable" name).
    /// </summary>
    public struct Pickable : IComponentData { }

    /// <summary>
    /// Hand-specific pickup tuning data.
    /// </summary>
    public struct HandPickable : IComponentData
    {
        public float Mass;
        public float MaxHoldDistance;
        public float ThrowImpulseMultiplier;
        public float FollowLerp;
    }

    /// <summary>
    /// Component marking an entity as a source for siphoning resources.
    /// </summary>
    public struct SiphonSource : IComponentData
    {
        public ushort ResourceTypeIndex;
        public float Amount;  // Or reference to aggregate container
        public float MinChunkSize;
        public float SiphonResistance;  // Optional rate modifier
    }

    /// <summary>
    /// Tag component marking an entity as a valid dump target for storehouses.
    /// </summary>
    public struct DumpTargetStorehouse : IComponentData { }

    /// <summary>
    /// Tag component marking an entity as a valid dump target for construction sites.
    /// </summary>
    public struct DumpTargetConstruction : IComponentData { }

    /// <summary>
    /// Tag component marking an entity as a valid dump target for ground.
    /// </summary>
    public struct DumpTargetGround : IComponentData { }

    /// <summary>
    /// Tag component marking an entity as a valid surface for casting miracles.
    /// </summary>
    public struct MiracleSurface : IComponentData { }
}

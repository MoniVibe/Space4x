using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Formation
{
    /// <summary>
    /// Types of formations.
    /// </summary>
    public enum FormationType : byte
    {
        None = 0,
        
        // Basic formations (1-9)
        Line = 1,
        Column = 2,
        Wedge = 3,
        Circle = 4,
        Square = 5,
        
        // Combat formations (10-19)
        Phalanx = 10,
        Skirmish = 11,
        Flanking = 12,
        Defensive = 13,
        Offensive = 14,
        
        // Fleet formations (20-29)
        Echelon = 20,
        Diamond = 21,
        Vanguard = 22,
        Rearguard = 23,
        Screen = 24,
        
        // Special formations (30-39)
        Ambush = 30,
        Escort = 31,
        Patrol = 32,
        Siege = 33,
        Scatter = 34,
        
        // Custom (40+)
        Custom = 40
    }

    /// <summary>
    /// Slot roles within a formation.
    /// </summary>
    public enum FormationSlotRole : byte
    {
        Any = 0,
        Leader = 1,
        Front = 2,
        Flank = 3,
        Rear = 4,
        Center = 5,
        Support = 6,
        Scout = 7,
        Reserve = 8
    }

    /// <summary>
    /// Formation state for a group entity.
    /// </summary>
    public struct FormationState : IComponentData
    {
        public FormationType Type;
        public float3 AnchorPosition;     // Formation center/leader position
        public quaternion AnchorRotation; // Formation facing
        public float Spacing;             // Distance between units
        public float Scale;               // Overall formation scale
        public byte MaxSlots;             // Maximum units in formation
        public byte FilledSlots;          // Currently filled slots
        public bool IsMoving;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Individual slot definition within a formation.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct FormationSlot : IBufferElementData
    {
        public byte SlotIndex;
        public float3 LocalOffset;        // Position relative to anchor
        public FormationSlotRole Role;
        public Entity AssignedEntity;     // Who's in this slot
        public byte Priority;             // Assignment priority (lower = better)
        public bool IsRequired;           // Must be filled for formation
    }

    /// <summary>
    /// Membership in a formation (on individual units).
    /// </summary>
    public struct FormationMember : IComponentData
    {
        public Entity FormationEntity;
        public byte SlotIndex;
        public float3 TargetPosition;     // World position to move to
        public float ArrivalThreshold;    // Distance considered "in position"
        public bool IsInPosition;
        public uint AssignedTick;
    }

    /// <summary>
    /// Request to change formation type.
    /// </summary>
    public struct ChangeFormationRequest : IComponentData
    {
        public Entity FormationEntity;
        public FormationType NewType;
        public float NewSpacing;
        public bool Immediate;            // Skip transition
    }

    /// <summary>
    /// Request to assign a unit to a formation.
    /// </summary>
    public struct FormationAssignRequest : IComponentData
    {
        public Entity FormationEntity;
        public Entity UnitEntity;
        public byte PreferredSlot;        // 255 = auto-assign
        public FormationSlotRole PreferredRole;
    }

    /// <summary>
    /// Event emitted when formation changes.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct FormationChangedEvent : IBufferElementData
    {
        public FormationType OldType;
        public FormationType NewType;
        public uint Tick;
    }

    /// <summary>
    /// Configuration for formation system.
    /// </summary>
    public struct FormationConfig : IComponentData
    {
        public float DefaultSpacing;      // Default unit spacing
        public float TransitionSpeed;     // How fast units move to new positions
        public float ArrivalThreshold;    // Distance to be "in position"
        public float UpdateInterval;      // Ticks between position updates
    }
}


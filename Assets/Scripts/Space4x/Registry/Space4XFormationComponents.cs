using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Role of a vessel within a formation.
    /// </summary>
    public enum FormationRole : byte
    {
        /// <summary>
        /// Formation leader - others follow relative to this position.
        /// </summary>
        Leader = 0,

        /// <summary>
        /// Escort wing - flanks the leader for protection.
        /// </summary>
        Escort = 1,

        /// <summary>
        /// Attack wing - positioned forward for offensive operations.
        /// </summary>
        Wing = 2,

        /// <summary>
        /// Rear guard - protects the formation's back.
        /// </summary>
        RearGuard = 3,

        /// <summary>
        /// Scout - positioned ahead for reconnaissance.
        /// </summary>
        Scout = 4,

        /// <summary>
        /// Support - positioned near leader for logistics/repair.
        /// </summary>
        Support = 5
    }

    /// <summary>
    /// Predefined formation shape types.
    /// </summary>
    public enum FormationShape : byte
    {
        /// <summary>
        /// V-shape pointing forward.
        /// </summary>
        Wedge = 0,

        /// <summary>
        /// Horizontal line.
        /// </summary>
        Line = 1,

        /// <summary>
        /// Units form a protective circle.
        /// </summary>
        Circle = 2,

        /// <summary>
        /// Compact defensive cluster.
        /// </summary>
        Cluster = 3,

        /// <summary>
        /// Staggered rows for coverage.
        /// </summary>
        Echelon = 4,

        /// <summary>
        /// Spread out for maximum coverage.
        /// </summary>
        Dispersed = 5
    }

    /// <summary>
    /// Defines a slot position within a formation template.
    /// </summary>
    public struct FormationSlot : IComponentData
    {
        /// <summary>
        /// Index of this slot in the formation (0 = leader).
        /// </summary>
        public byte SlotIndex;

        /// <summary>
        /// Role assigned to this slot.
        /// </summary>
        public FormationRole Role;

        /// <summary>
        /// Local offset from formation center.
        /// </summary>
        public float3 Offset;

        /// <summary>
        /// Preferred facing direction (relative to formation heading).
        /// </summary>
        public float3 FacingOffset;

        /// <summary>
        /// Priority for slot assignment (lower = more important).
        /// </summary>
        public byte Priority;

        public static FormationSlot Leader => new FormationSlot
        {
            SlotIndex = 0,
            Role = FormationRole.Leader,
            Offset = float3.zero,
            FacingOffset = new float3(0, 0, 1),
            Priority = 0
        };

        public static FormationSlot CreateEscort(byte index, float3 offset) => new FormationSlot
        {
            SlotIndex = index,
            Role = FormationRole.Escort,
            Offset = offset,
            FacingOffset = new float3(0, 0, 1),
            Priority = 1
        };

        public static FormationSlot CreateWing(byte index, float3 offset) => new FormationSlot
        {
            SlotIndex = index,
            Role = FormationRole.Wing,
            Offset = offset,
            FacingOffset = new float3(0, 0, 1),
            Priority = 2
        };
    }

    /// <summary>
    /// Links a vessel to a formation slot in a parent fleet.
    /// </summary>
    public struct FormationAssignment : IComponentData
    {
        /// <summary>
        /// Entity of the formation leader/parent.
        /// </summary>
        public Entity FormationLeader;

        /// <summary>
        /// Assigned slot index within the formation.
        /// </summary>
        public byte SlotIndex;

        /// <summary>
        /// Current offset being used (may differ from slot offset during transitions).
        /// </summary>
        public float3 CurrentOffset;

        /// <summary>
        /// Target world position based on formation.
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// How tightly to maintain formation (0 = loose, 1 = strict).
        /// </summary>
        public half FormationTightness;

        /// <summary>
        /// Tick when assignment was made.
        /// </summary>
        public uint AssignedTick;

        public static FormationAssignment Create(Entity leader, byte slotIndex, float3 offset, float tightness = 0.8f)
        {
            return new FormationAssignment
            {
                FormationLeader = leader,
                SlotIndex = slotIndex,
                CurrentOffset = offset,
                TargetPosition = float3.zero,
                FormationTightness = (half)math.clamp(tightness, 0f, 1f),
                AssignedTick = 0
            };
        }
    }

    /// <summary>
    /// Formation template defining all slots for a fleet/group.
    /// </summary>
    public struct FormationTemplate : IComponentData
    {
        /// <summary>
        /// Shape of the formation.
        /// </summary>
        public FormationShape Shape;

        /// <summary>
        /// Spacing between slots.
        /// </summary>
        public float Spacing;

        /// <summary>
        /// Maximum number of slots in this formation.
        /// </summary>
        public byte MaxSlots;

        /// <summary>
        /// Current heading direction (world space).
        /// </summary>
        public float3 Heading;

        public static FormationTemplate Wedge(float spacing = 10f, byte maxSlots = 7) => new FormationTemplate
        {
            Shape = FormationShape.Wedge,
            Spacing = spacing,
            MaxSlots = maxSlots,
            Heading = new float3(0, 0, 1)
        };

        public static FormationTemplate Line(float spacing = 8f, byte maxSlots = 10) => new FormationTemplate
        {
            Shape = FormationShape.Line,
            Spacing = spacing,
            MaxSlots = maxSlots,
            Heading = new float3(0, 0, 1)
        };

        public static FormationTemplate Circle(float spacing = 15f, byte maxSlots = 8) => new FormationTemplate
        {
            Shape = FormationShape.Circle,
            Spacing = spacing,
            MaxSlots = maxSlots,
            Heading = new float3(0, 0, 1)
        };
    }

    /// <summary>
    /// Buffer of slot definitions for a formation template.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct FormationSlotDefinition : IBufferElementData
    {
        public FormationSlot Slot;
    }

    /// <summary>
    /// Utilities for calculating formation positions.
    /// </summary>
    public static class FormationUtility
    {
        /// <summary>
        /// Calculates world position for a formation slot.
        /// </summary>
        public static float3 CalculateSlotWorldPosition(
            float3 leaderPosition,
            quaternion leaderRotation,
            float3 slotOffset)
        {
            // Transform slot offset by leader's rotation
            var rotatedOffset = math.mul(leaderRotation, slotOffset);
            return leaderPosition + rotatedOffset;
        }

        /// <summary>
        /// Generates slot offsets for a wedge formation.
        /// </summary>
        public static void GenerateWedgeOffsets(float spacing, int slotCount, ref NativeArray<float3> offsets)
        {
            if (slotCount <= 0 || !offsets.IsCreated)
            {
                return;
            }

            // Leader at center
            offsets[0] = float3.zero;

            // Alternate left/right behind leader
            int row = 1;
            int col = 0;
            for (int i = 1; i < math.min(slotCount, offsets.Length); i++)
            {
                float side = (i % 2 == 1) ? -1f : 1f;
                float x = side * spacing * ((col / 2) + 1);
                float z = -spacing * row;

                offsets[i] = new float3(x, 0, z);

                col++;
                if (col % 2 == 0)
                {
                    row++;
                }
            }
        }

        /// <summary>
        /// Generates slot offsets for a line formation.
        /// </summary>
        public static void GenerateLineOffsets(float spacing, int slotCount, ref NativeArray<float3> offsets)
        {
            if (slotCount <= 0 || !offsets.IsCreated)
            {
                return;
            }

            // Leader at center
            float halfWidth = (slotCount - 1) * spacing * 0.5f;

            for (int i = 0; i < math.min(slotCount, offsets.Length); i++)
            {
                float x = (i * spacing) - halfWidth;
                offsets[i] = new float3(x, 0, 0);
            }
        }

        /// <summary>
        /// Generates slot offsets for a circle formation.
        /// </summary>
        public static void GenerateCircleOffsets(float radius, int slotCount, ref NativeArray<float3> offsets)
        {
            if (slotCount <= 0 || !offsets.IsCreated)
            {
                return;
            }

            // Leader at center
            offsets[0] = float3.zero;

            // Others around the circle
            float angleStep = (2f * math.PI) / (slotCount - 1);
            for (int i = 1; i < math.min(slotCount, offsets.Length); i++)
            {
                float angle = (i - 1) * angleStep;
                float x = math.cos(angle) * radius;
                float z = math.sin(angle) * radius;
                offsets[i] = new float3(x, 0, z);
            }
        }
    }
}


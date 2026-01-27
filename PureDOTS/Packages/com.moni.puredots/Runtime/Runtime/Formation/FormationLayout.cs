using Unity.Mathematics;
using Unity.Burst;

namespace PureDOTS.Runtime.Formation
{
    /// <summary>
    /// Static helpers for calculating formation layouts.
    /// All local offsets are computed in a local coordinate system where:
    /// - X = right
    /// - Y = up (perpendicular to formation plane)
    /// - Z = forward
    /// 
    /// For ground formations, Y=0 in local space. For 3D formations (space, flying),
    /// use the 3D overloads that accept a vertical offset parameter.
    /// 
    /// Use LocalToWorld() to transform local offsets to world positions using
    /// the anchor's position and rotation.
    /// </summary>
    [BurstCompile]
    public static class FormationLayout
    {
        /// <summary>
        /// Gets the local offset for a slot in a line formation.
        /// Line extends along the local X axis (right).
        /// </summary>
        /// <param name="slotIndex">Index of the slot (0 = center-left).</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="spacing">Distance between adjacent slots.</param>
        /// <param name="verticalOffset">Optional vertical offset for 3D formations.</param>
        public static float3 GetLineOffset(int slotIndex, int totalSlots, float spacing, float verticalOffset = 0f)
        {
            float halfWidth = (totalSlots - 1) * spacing * 0.5f;
            return new float3(slotIndex * spacing - halfWidth, verticalOffset, 0);
        }

        /// <summary>
        /// Gets the local offset for a slot in a column formation.
        /// Column extends along the negative local Z axis (backward).
        /// </summary>
        /// <param name="slotIndex">Index of the slot (0 = front).</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="spacing">Distance between adjacent slots.</param>
        /// <param name="verticalOffset">Optional vertical offset for 3D formations.</param>
        public static float3 GetColumnOffset(int slotIndex, int totalSlots, float spacing, float verticalOffset = 0f)
        {
            return new float3(0, verticalOffset, -slotIndex * spacing);
        }

        /// <summary>
        /// Gets the local offset for a slot in a wedge/V formation.
        /// Leader at point (front), others fan out behind.
        /// </summary>
        /// <param name="slotIndex">Index of the slot (0 = leader at point).</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="spacing">Distance between adjacent slots.</param>
        /// <param name="verticalOffset">Optional vertical offset for 3D formations.</param>
        public static float3 GetWedgeOffset(int slotIndex, int totalSlots, float spacing, float verticalOffset = 0f)
        {
            if (slotIndex == 0)
                return new float3(0, verticalOffset, 0); // Leader at point

            int row = (int)math.ceil(math.sqrt(slotIndex));
            int posInRow = slotIndex - (row * row - row) / 2;
            
            float x = (posInRow - row * 0.5f) * spacing;
            float z = -row * spacing;
            
            return new float3(x, verticalOffset, z);
        }

        /// <summary>
        /// Gets the local offset for a slot in a circle formation.
        /// Units arranged in a ring on the XZ plane.
        /// </summary>
        /// <param name="slotIndex">Index of the slot.</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="spacing">Approximate spacing (determines radius).</param>
        /// <param name="verticalOffset">Optional vertical offset for 3D formations.</param>
        public static float3 GetCircleOffset(int slotIndex, int totalSlots, float spacing, float verticalOffset = 0f)
        {
            if (totalSlots <= 1)
                return new float3(0, verticalOffset, 0);

            float radius = (totalSlots * spacing) / (2f * math.PI);
            float angle = (slotIndex / (float)totalSlots) * 2f * math.PI;
            
            return new float3(math.cos(angle) * radius, verticalOffset, math.sin(angle) * radius);
        }

        /// <summary>
        /// Gets the local offset for a slot in a square formation.
        /// Units arranged in a grid on the XZ plane.
        /// </summary>
        /// <param name="slotIndex">Index of the slot.</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="spacing">Distance between adjacent slots.</param>
        /// <param name="verticalOffset">Optional vertical offset for 3D formations.</param>
        public static float3 GetSquareOffset(int slotIndex, int totalSlots, float spacing, float verticalOffset = 0f)
        {
            int side = (int)math.ceil(math.sqrt(totalSlots));
            int row = slotIndex / side;
            int col = slotIndex % side;
            
            float halfSide = (side - 1) * spacing * 0.5f;
            
            return new float3(col * spacing - halfSide, verticalOffset, -row * spacing + halfSide);
        }

        /// <summary>
        /// Gets the local offset for a slot in an echelon formation.
        /// Diagonal line extending back and to one side.
        /// </summary>
        /// <param name="slotIndex">Index of the slot (0 = leader).</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="spacing">Distance between adjacent slots.</param>
        /// <param name="leftEchelon">True for left echelon, false for right.</param>
        /// <param name="verticalOffset">Optional vertical offset for 3D formations.</param>
        public static float3 GetEchelonOffset(int slotIndex, int totalSlots, float spacing, bool leftEchelon = true, float verticalOffset = 0f)
        {
            float direction = leftEchelon ? -1f : 1f;
            return new float3(slotIndex * spacing * direction, verticalOffset, -slotIndex * spacing);
        }

        /// <summary>
        /// Gets the local offset for a slot in a diamond formation.
        /// Units arranged in concentric diamond rings.
        /// </summary>
        /// <param name="slotIndex">Index of the slot (0 = point).</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="spacing">Distance between adjacent slots.</param>
        /// <param name="verticalOffset">Optional vertical offset for 3D formations.</param>
        public static float3 GetDiamondOffset(int slotIndex, int totalSlots, float spacing, float verticalOffset = 0f)
        {
            if (slotIndex == 0)
                return new float3(0, verticalOffset, spacing); // Point

            int remaining = slotIndex - 1;
            int ring = 1;
            int ringStart = 1;
            
            while (remaining >= ring * 4)
            {
                remaining -= ring * 4;
                ringStart += ring * 4;
                ring++;
            }

            int side = remaining / ring;
            int posOnSide = remaining % ring;
            
            float3 offset = new float3(0, verticalOffset, 0);
            float ringSpacing = ring * spacing;
            
            switch (side)
            {
                case 0: // Right
                    offset = new float3(ringSpacing, verticalOffset, ringSpacing - posOnSide * spacing);
                    break;
                case 1: // Bottom
                    offset = new float3(ringSpacing - posOnSide * spacing, verticalOffset, -ringSpacing);
                    break;
                case 2: // Left
                    offset = new float3(-ringSpacing, verticalOffset, -ringSpacing + posOnSide * spacing);
                    break;
                case 3: // Top
                    offset = new float3(-ringSpacing + posOnSide * spacing, verticalOffset, ringSpacing);
                    break;
            }
            
            return offset;
        }

        // ============================================================================
        // 3D Formation Helpers - For space/flying units
        // ============================================================================

        /// <summary>
        /// Gets the local offset for a slot in a sphere formation.
        /// Units distributed on a sphere surface using Fibonacci lattice.
        /// </summary>
        /// <param name="slotIndex">Index of the slot.</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="radius">Radius of the sphere.</param>
        public static float3 GetSphereOffset(int slotIndex, int totalSlots, float radius)
        {
            if (totalSlots <= 1)
                return float3.zero;

            // Fibonacci lattice for even distribution on sphere
            float goldenRatio = (1f + math.sqrt(5f)) / 2f;
            float i = slotIndex + 0.5f;
            
            float phi = math.acos(1f - 2f * i / totalSlots);
            float theta = 2f * math.PI * i / goldenRatio;
            
            return new float3(
                radius * math.sin(phi) * math.cos(theta),
                radius * math.cos(phi),
                radius * math.sin(phi) * math.sin(theta)
            );
        }

        /// <summary>
        /// Gets the local offset for a slot in a 3D box/cube formation.
        /// Units arranged in a 3D grid.
        /// </summary>
        /// <param name="slotIndex">Index of the slot.</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="spacing">Distance between adjacent slots.</param>
        public static float3 GetBoxOffset(int slotIndex, int totalSlots, float spacing)
        {
            int side = (int)math.ceil(math.pow(totalSlots, 1f / 3f));
            int layer = slotIndex / (side * side);
            int remainder = slotIndex % (side * side);
            int row = remainder / side;
            int col = remainder % side;
            
            float halfSide = (side - 1) * spacing * 0.5f;
            
            return new float3(
                col * spacing - halfSide,
                layer * spacing - halfSide,
                -row * spacing + halfSide
            );
        }

        /// <summary>
        /// Gets the local offset for a slot in a cylinder formation.
        /// Units arranged in stacked rings.
        /// </summary>
        /// <param name="slotIndex">Index of the slot.</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="radius">Radius of the cylinder.</param>
        /// <param name="layerSpacing">Vertical spacing between layers.</param>
        /// <param name="slotsPerLayer">Number of slots per layer (ring).</param>
        public static float3 GetCylinderOffset(int slotIndex, int totalSlots, float radius, float layerSpacing, int slotsPerLayer)
        {
            if (slotsPerLayer <= 0) slotsPerLayer = 8;
            
            int layer = slotIndex / slotsPerLayer;
            int posInLayer = slotIndex % slotsPerLayer;
            
            float angle = (posInLayer / (float)slotsPerLayer) * 2f * math.PI;
            int totalLayers = (totalSlots + slotsPerLayer - 1) / slotsPerLayer;
            float halfHeight = (totalLayers - 1) * layerSpacing * 0.5f;
            
            return new float3(
                math.cos(angle) * radius,
                layer * layerSpacing - halfHeight,
                math.sin(angle) * radius
            );
        }

        /// <summary>
        /// Gets the slot offset for any formation type.
        /// </summary>
        /// <param name="type">The formation type.</param>
        /// <param name="slotIndex">Index of the slot.</param>
        /// <param name="totalSlots">Total number of slots in formation.</param>
        /// <param name="spacing">Distance between adjacent slots.</param>
        /// <param name="verticalOffset">Optional vertical offset for 3D formations.</param>
        public static float3 GetSlotOffset(FormationType type, int slotIndex, int totalSlots, float spacing, float verticalOffset = 0f)
        {
            return type switch
            {
                FormationType.Line => GetLineOffset(slotIndex, totalSlots, spacing, verticalOffset),
                FormationType.Column => GetColumnOffset(slotIndex, totalSlots, spacing, verticalOffset),
                FormationType.Wedge => GetWedgeOffset(slotIndex, totalSlots, spacing, verticalOffset),
                FormationType.Circle => GetCircleOffset(slotIndex, totalSlots, spacing, verticalOffset),
                FormationType.Square => GetSquareOffset(slotIndex, totalSlots, spacing, verticalOffset),
                FormationType.Echelon => GetEchelonOffset(slotIndex, totalSlots, spacing, true, verticalOffset),
                FormationType.Diamond => GetDiamondOffset(slotIndex, totalSlots, spacing, verticalOffset),
                FormationType.Phalanx => GetSquareOffset(slotIndex, totalSlots, spacing * 0.7f, verticalOffset), // Tighter
                FormationType.Skirmish => GetCircleOffset(slotIndex, totalSlots, spacing * 1.5f, verticalOffset), // Looser
                FormationType.Defensive => GetCircleOffset(slotIndex, totalSlots, spacing * 0.8f, verticalOffset),
                FormationType.Offensive => GetWedgeOffset(slotIndex, totalSlots, spacing, verticalOffset),
                FormationType.Vanguard => GetWedgeOffset(slotIndex, totalSlots, spacing, verticalOffset),
                FormationType.Rearguard => GetWedgeOffset(slotIndex, totalSlots, spacing, verticalOffset), // Inverted
                FormationType.Screen => GetLineOffset(slotIndex, totalSlots, spacing * 2f, verticalOffset), // Wide
                FormationType.Scatter => GetCircleOffset(slotIndex, totalSlots, spacing * 3f, verticalOffset), // Very loose
                _ => GetSquareOffset(slotIndex, totalSlots, spacing, verticalOffset)
            };
        }

        /// <summary>
        /// Gets the slot role for a position in a formation.
        /// </summary>
        public static FormationSlotRole GetSlotRole(FormationType type, int slotIndex, int totalSlots)
        {
            if (slotIndex == 0)
                return FormationSlotRole.Leader;

            return type switch
            {
                FormationType.Line => slotIndex < totalSlots / 2 
                    ? FormationSlotRole.Flank 
                    : FormationSlotRole.Flank,
                FormationType.Column => slotIndex < 3 
                    ? FormationSlotRole.Front 
                    : slotIndex >= totalSlots - 2 
                        ? FormationSlotRole.Rear 
                        : FormationSlotRole.Center,
                FormationType.Wedge => slotIndex < 3 
                    ? FormationSlotRole.Front 
                    : FormationSlotRole.Flank,
                FormationType.Circle => FormationSlotRole.Center,
                FormationType.Square => GetSquareRole(slotIndex, totalSlots),
                FormationType.Phalanx => slotIndex < totalSlots / 3 
                    ? FormationSlotRole.Front 
                    : FormationSlotRole.Support,
                FormationType.Skirmish => FormationSlotRole.Scout,
                FormationType.Defensive => slotIndex < totalSlots / 2 
                    ? FormationSlotRole.Front 
                    : FormationSlotRole.Support,
                FormationType.Offensive => slotIndex < 4 
                    ? FormationSlotRole.Front 
                    : FormationSlotRole.Support,
                _ => FormationSlotRole.Any
            };
        }

        private static FormationSlotRole GetSquareRole(int slotIndex, int totalSlots)
        {
            int side = (int)math.ceil(math.sqrt(totalSlots));
            int row = slotIndex / side;
            int col = slotIndex % side;
            
            if (row == 0)
                return FormationSlotRole.Front;
            if (row == side - 1)
                return FormationSlotRole.Rear;
            if (col == 0 || col == side - 1)
                return FormationSlotRole.Flank;
            return FormationSlotRole.Center;
        }

        /// <summary>
        /// Transforms a local offset to world position.
        /// </summary>
        public static float3 LocalToWorld(float3 localOffset, float3 anchorPosition, quaternion anchorRotation, float scale)
        {
            return anchorPosition + math.mul(anchorRotation, localOffset * scale);
        }

        /// <summary>
        /// Gets the recommended slot count for a formation type.
        /// </summary>
        public static int GetRecommendedSlotCount(FormationType type)
        {
            return type switch
            {
                FormationType.Line => 10,
                FormationType.Column => 10,
                FormationType.Wedge => 15,
                FormationType.Circle => 12,
                FormationType.Square => 16,
                FormationType.Phalanx => 25,
                FormationType.Skirmish => 8,
                FormationType.Diamond => 13,
                FormationType.Echelon => 10,
                FormationType.Vanguard => 7,
                FormationType.Rearguard => 7,
                FormationType.Screen => 5,
                FormationType.Scatter => 10,
                _ => 10
            };
        }

        /// <summary>
        /// Gets combat bonuses for a formation type.
        /// </summary>
        public static void GetFormationBonuses(
            FormationType type,
            out float attackBonus,
            out float defenseBonus,
            out float speedBonus)
        {
            attackBonus = 0f;
            defenseBonus = 0f;
            speedBonus = 0f;

            switch (type)
            {
                case FormationType.Line:
                    attackBonus = 0.1f;
                    break;
                case FormationType.Wedge:
                    attackBonus = 0.2f;
                    speedBonus = 0.1f;
                    break;
                case FormationType.Phalanx:
                    defenseBonus = 0.3f;
                    speedBonus = -0.2f;
                    break;
                case FormationType.Skirmish:
                    speedBonus = 0.2f;
                    defenseBonus = -0.1f;
                    break;
                case FormationType.Defensive:
                    defenseBonus = 0.25f;
                    attackBonus = -0.1f;
                    break;
                case FormationType.Offensive:
                    attackBonus = 0.25f;
                    defenseBonus = -0.1f;
                    break;
                case FormationType.Circle:
                    defenseBonus = 0.15f;
                    break;
                case FormationType.Scatter:
                    speedBonus = 0.15f;
                    defenseBonus = -0.2f;
                    break;
            }
        }
    }
}


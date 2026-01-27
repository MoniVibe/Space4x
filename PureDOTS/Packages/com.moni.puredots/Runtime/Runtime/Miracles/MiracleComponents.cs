using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Miracles
{
    // NOTE: MiracleId is defined in Runtime/Miracles/MiracleCatalogComponents.cs (as ushort enum)
    // Duplicate definition removed to avoid conflicts

    /// <summary>
    /// Component tracking which miracle is currently selected.
    /// </summary>
    public struct MiracleSelection : IComponentData
    {
        public int SelectedMiracleId; // -1 = none, otherwise MiracleId enum value
    }

    /// <summary>
    /// Event buffer element for casting miracles.
    /// </summary>
    public struct MiracleCastEvent : IBufferElementData
    {
        public int MiracleId;
        public float3 TargetPosition;
        public Entity TargetEntity;
    }

    /// <summary>
    /// God pool singleton component for storing siphoned essence/resources.
    /// </summary>
    public struct GodPool : IComponentData
    {
        public float Essence; // Generic essence/energy for miracles
    }
}


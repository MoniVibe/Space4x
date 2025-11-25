using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for expertise vectors (CarrierCommand, Espionage, Logistics, Psionic, Beastmastery).
    /// Adds ExpertiseEntry buffer to entity.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Expertise")]
    public sealed class ExpertiseAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct ExpertiseEntryData
        {
            public ExpertiseType Type;
            [Range(0, 255)]
            [Tooltip("Expertise tier (0-255)")]
            public byte Tier;
        }

        [Tooltip("List of expertise entries")]
        public ExpertiseEntryData[] ExpertiseEntries = new ExpertiseEntryData[0];

        public sealed class Baker : Unity.Entities.Baker<ExpertiseAuthoring>
        {
            public override void Bake(ExpertiseAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<ExpertiseEntry>(entity);

                if (authoring.ExpertiseEntries != null)
                {
                    foreach (var entryData in authoring.ExpertiseEntries)
                    {
                        buffer.Add(new ExpertiseEntry
                        {
                            Type = entryData.Type,
                            Tier = entryData.Tier
                        });
                    }
                }
            }
        }
    }
}

using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    /// <summary>
    /// Authoring component for composed aggregates with profile references.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Composed Aggregate")]
    public sealed class ComposedAggregateAuthoring : MonoBehaviour
    {
        [Tooltip("Template ID (references AggregateTemplateCatalog)")]
        public string templateId = string.Empty;

        [Tooltip("Outlook ID (references OutlookProfileCatalog)")]
        public string outlookId = string.Empty;

        [Tooltip("Alignment ID (references AlignmentProfileCatalog)")]
        public string alignmentId = string.Empty;

        [Tooltip("Personality ID (references PersonalityArchetypeCatalog)")]
        public string personalityId = string.Empty;

        [Tooltip("Theme ID (references ThemeProfileCatalog)")]
        public string themeId = string.Empty;

        public sealed class Baker : Unity.Entities.Baker<ComposedAggregateAuthoring>
        {
            public override void Bake(ComposedAggregateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Registry.ComposedAggregateProfile
                {
                    TemplateId = new FixedString32Bytes(authoring.templateId ?? string.Empty),
                    OutlookId = new FixedString32Bytes(authoring.outlookId ?? string.Empty),
                    AlignmentId = new FixedString32Bytes(authoring.alignmentId ?? string.Empty),
                    PersonalityId = new FixedString32Bytes(authoring.personalityId ?? string.Empty),
                    ThemeId = new FixedString32Bytes(authoring.themeId ?? string.Empty)
                });
            }
        }
    }
}



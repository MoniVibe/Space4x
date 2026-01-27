#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Meta
{
    [DisallowMultipleComponent]
    public sealed class CultureAuthoring : MonoBehaviour
    {
        [SerializeField]
        private CultureProfileAsset profile;

        [SerializeField]
        private bool applyProfileOnValidate = true;

        [Header("Identity")]
        [SerializeField] private ushort cultureId = 1;
        [SerializeField] private string cultureName = "Culture";
        [SerializeField] private CultureType cultureType = CultureType.Tribal;

        [Header("Alignment")]
        [SerializeField, Range(-1f, 1f)] private float currentAlignment;
        [SerializeField, Range(-1f, 1f)] private float baseAlignment;
        [SerializeField, Range(-1f, 1f)] private float alignmentVelocity;
        [SerializeField] private CultureAlignmentFlags alignmentFlags = CultureAlignmentFlags.None;

        [Header("Membership")]
        [SerializeField, Min(0)] private int memberCount;

        [Header("Description")]
        [SerializeField, TextArea] private string description;

        [Header("Options")]
        [Tooltip("Add SpatialIndexedTag so cultures participate in spatial lookups (e.g. for alignment overlays).")]
        public bool addSpatialIndexedTag = false;

        public ushort CultureId
        {
            get => cultureId;
            set => cultureId = value;
        }

        public string CultureName
        {
            get => cultureName;
            set => cultureName = value;
        }

        public CultureType CultureType
        {
            get => cultureType;
            set => cultureType = value;
        }

        public float CurrentAlignment
        {
            get => currentAlignment;
            set => currentAlignment = Mathf.Clamp(value, -1f, 1f);
        }

        public float BaseAlignment
        {
            get => baseAlignment;
            set => baseAlignment = Mathf.Clamp(value, -1f, 1f);
        }

        public float AlignmentVelocity
        {
            get => alignmentVelocity;
            set => alignmentVelocity = Mathf.Clamp(value, -1f, 1f);
        }

        public CultureAlignmentFlags AlignmentFlags
        {
            get => alignmentFlags;
            set => alignmentFlags = value;
        }

        public int MemberCount
        {
            get => memberCount;
            set => memberCount = Mathf.Max(0, value);
        }

        public string Description
        {
            get => description;
            set => description = value;
        }

        private void OnValidate()
        {
            if (!applyProfileOnValidate || profile == null)
            {
                return;
            }

            profile.CopyTo(this);
        }

        internal CultureState BuildComponent()
        {
            return new CultureState
            {
                CultureId = cultureId,
                CultureName = new FixedString64Bytes(cultureName ?? string.Empty),
                CultureType = cultureType,
                MemberCount = memberCount,
                CurrentAlignment = Mathf.Clamp(currentAlignment, -1f, 1f),
                AlignmentVelocity = Mathf.Clamp(alignmentVelocity, -1f, 1f),
                BaseAlignment = Mathf.Clamp(baseAlignment, -1f, 1f),
                AlignmentFlags = alignmentFlags,
                Description = new FixedString128Bytes(description ?? string.Empty)
            };
        }
    }

    public sealed class CultureAuthoringBaker : Baker<CultureAuthoring>
    {
        public override void Bake(CultureAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            var state = authoring.BuildComponent();

            AddComponent(entity, state);

            if (authoring.addSpatialIndexedTag)
            {
                AddComponent<SpatialIndexedTag>(entity);
            }
        }
    }
}
#endif



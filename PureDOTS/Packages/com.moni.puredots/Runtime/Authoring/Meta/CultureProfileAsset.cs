#if UNITY_EDITOR
using PureDOTS.Runtime.Components;
using UnityEngine;

namespace PureDOTS.Authoring.Meta
{
    [CreateAssetMenu(menuName = "PureDOTS/Meta Registries/Culture Profile", fileName = "CultureProfile")]
    public sealed class CultureProfileAsset : ScriptableObject
    {
        [Header("Identity")]
        [Min(0)] public ushort cultureId = 1;
        public string cultureName = "Culture";
        public CultureType cultureType = CultureType.Tribal;

        [Header("Alignment")]
        [Range(-1f, 1f)] public float currentAlignment = 0f;
        [Range(-1f, 1f)] public float baseAlignment = 0f;
        [Range(-1f, 1f)] public float alignmentVelocity = 0f;
        public CultureAlignmentFlags alignmentFlags = CultureAlignmentFlags.None;

        [Header("Membership")]
        [Min(0)] public int memberCount = 0;

        [Header("Description")]
        [TextArea] public string description;

        public void CopyTo(CultureAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            authoring.CultureId = cultureId;
            authoring.CultureName = cultureName;
            authoring.CultureType = cultureType;
            authoring.CurrentAlignment = currentAlignment;
            authoring.BaseAlignment = baseAlignment;
            authoring.AlignmentVelocity = alignmentVelocity;
            authoring.AlignmentFlags = alignmentFlags;
            authoring.MemberCount = memberCount;
            authoring.Description = description;
        }
    }
}
#endif



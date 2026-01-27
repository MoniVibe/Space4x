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
    public sealed class AreaEffectAuthoring : MonoBehaviour
    {
        [SerializeField]
        private AreaEffectProfileAsset profile;

        [SerializeField]
        private bool applyProfileOnValidate = true;

        [Header("Identity")]
        [SerializeField] private string effectName = "Aura";
        [SerializeField] private AreaEffectType effectType = AreaEffectType.Buff;

        [Header("Behaviour")]
        [SerializeField] private float currentStrength = 1f;
        [SerializeField] private float maxStrength = 1f;
        [SerializeField] private float radius = 12f;
        [SerializeField] private uint expirationTick;
        [SerializeField] private ushort effectId;
        [SerializeField] private AreaEffectTargetMask affectedTargets = AreaEffectTargetMask.Villagers | AreaEffectTargetMask.Structures;
        [SerializeField] private GameObject owner;

        [Header("Options")]
        [Tooltip("Add SpatialIndexedTag so effects participate in spatial queries.")]
        public bool addSpatialIndexedTag = true;

        public string EffectName
        {
            get => effectName;
            set => effectName = value;
        }

        public AreaEffectType EffectType
        {
            get => effectType;
            set => effectType = value;
        }

        public float CurrentStrength
        {
            get => currentStrength;
            set => currentStrength = Mathf.Max(0f, value);
        }

        public float MaxStrength
        {
            get => maxStrength;
            set => maxStrength = Mathf.Max(0f, value);
        }

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0f, value);
        }

        public uint ExpirationTick
        {
            get => expirationTick;
            set => expirationTick = value;
        }

        public ushort EffectId
        {
            get => effectId;
            set => effectId = value;
        }

        public AreaEffectTargetMask AffectedTargets
        {
            get => affectedTargets;
            set => affectedTargets = value;
        }

        public GameObject Owner
        {
            get => owner;
            set => owner = value;
        }

        private void OnValidate()
        {
            if (!applyProfileOnValidate || profile == null)
            {
                return;
            }

            profile.CopyTo(this);
        }

        internal AreaEffectState BuildComponent(Entity ownerEntity)
        {
            return new AreaEffectState
            {
                EffectType = effectType,
                CurrentStrength = currentStrength,
                Radius = radius,
                MaxStrength = Mathf.Max(currentStrength, maxStrength),
                OwnerEntity = ownerEntity,
                EffectId = effectId,
                AffectedArchetypes = affectedTargets,
                EffectName = new FixedString64Bytes(effectName ?? string.Empty),
                ExpirationTick = expirationTick
            };
        }
    }

    public sealed class AreaEffectAuthoringBaker : Baker<AreaEffectAuthoring>
    {
        public override void Bake(AreaEffectAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            var ownerEntity = authoring.Owner != null
                ? GetEntity(authoring.Owner, TransformUsageFlags.Dynamic)
                : Entity.Null;

            var state = authoring.BuildComponent(ownerEntity);
            AddComponent(entity, state);

            if (authoring.addSpatialIndexedTag)
            {
                AddComponent<SpatialIndexedTag>(entity);
            }
        }
    }
}
#endif


